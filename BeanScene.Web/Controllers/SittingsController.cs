using BeanScene.Web.Data;
using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeanScene.Web.Controllers;

/// <summary>Only the Manager can schedule sittings.</summary>
[Authorize(Roles = RoleNames.Manager)]
public class SittingsController : Controller
{
    private readonly ISittingService _sittings;
    private readonly ApplicationDbContext _db;

    public SittingsController(ISittingService sittings, ApplicationDbContext db)
    {
        _sittings = sittings;
        _db = db;
    }

    // GET /Sittings
    public async Task<IActionResult> Index(bool showPast = false)
    {
        var from = showPast ? (DateOnly?)null : DateOnly.FromDateTime(DateTime.Today);
        ViewBag.ShowPast = showPast;
        var hours = await _db.StoreHours.AsNoTracking().ToListAsync();
        ViewBag.Hours = hours.OrderBy(h => ((int)h.DayOfWeek + 6) % 7).ThenBy(h => h.SittingType).ToList();
        return View(await _sittings.ListAsync(from));
    }

    // POST /Sittings/SaveWeekly
    [HttpPost]
    public async Task<IActionResult> SaveWeekly(List<StoreHourInput> hours)
    {
        foreach (var h in hours)
        {
            if (h.Capacity < 1 || h.Capacity > 500)
            {
                TempData["Error"] = "Capacity must be between 1 and 500.";
                return RedirectToAction(nameof(Index));
            }
            if (h.IsOpen && h.EndTime <= h.StartTime)
            {
                TempData["Error"] = "Each open sitting must end after it starts.";
                return RedirectToAction(nameof(Index));
            }
        }

        // 1. Save the weekly hours.
        var rows = await _db.StoreHours.ToListAsync();
        foreach (var input in hours)
        {
            var row = rows.FirstOrDefault(r => r.StoreHourId == input.StoreHourId);
            if (row == null) continue;
            row.IsOpen = input.IsOpen;
            row.StartTime = input.StartTime;
            row.EndTime = input.EndTime;
            row.Capacity = input.Capacity;
        }
        await _db.SaveChangesAsync();

        // 2. Update the upcoming days that follow the weekly hours (not customised, not Special).
        const int daysAhead = 90;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var last = today.AddDays(daysAhead - 1);

        var booked = await _db.Reservations
            .Where(r => r.Status != ReservationStatus.Cancelled && r.Sitting.SittingDate >= today)
            .GroupBy(r => r.SittingId)
            .Select(g => new { g.Key, Guests = g.Sum(r => r.NumberOfGuests) })
            .ToDictionaryAsync(x => x.Key, x => x.Guests);

        var sittings = await _db.Sittings
            .Where(s => s.SittingDate >= today && s.SittingDate <= last
                        && !s.IsCustomised && s.SittingType != SittingType.Special)
            .ToListAsync();

        var updated = 0;
        foreach (var s in sittings)
        {
            var row = rows.FirstOrDefault(r => r.DayOfWeek == s.SittingDate.DayOfWeek && r.SittingType == s.SittingType);
            if (row == null) continue;
            s.StartTime = row.StartTime;
            s.EndTime = row.EndTime;
            s.Capacity = Math.Max(row.Capacity, booked.GetValueOrDefault(s.SittingId));
            s.IsClosed = !row.IsOpen;
            updated++;
        }

        // 3. Create any sittings that are missing for open hours (for example a day that used to be closed).
        var existing = (await _db.Sittings
                .Where(s => s.SittingDate >= today && s.SittingDate <= last)
                .Select(s => new { s.SittingDate, s.SittingType })
                .ToListAsync())
            .Select(s => (s.SittingDate, s.SittingType))
            .ToHashSet();

        for (var d = 0; d < daysAhead; d++)
        {
            var date = today.AddDays(d);
            foreach (var row in rows.Where(r => r.DayOfWeek == date.DayOfWeek && r.IsOpen))
            {
                if (existing.Contains((date, row.SittingType))) continue;
                _db.Sittings.Add(new Sitting
                {
                    SittingType = row.SittingType,
                    SittingDate = date,
                    StartTime = row.StartTime,
                    EndTime = row.EndTime,
                    Capacity = row.Capacity
                });
            }
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Weekly hours saved. {updated} upcoming sittings updated.";
        return RedirectToAction(nameof(Index));
    }

    // GET /Sittings/Create
    [HttpGet]
    public IActionResult Create() => View(new SittingFormViewModel());

    // POST /Sittings/Create
    [HttpPost]
    public async Task<IActionResult> Create(SittingFormViewModel form)
    {
        if (!ModelState.IsValid) return View(form);
        var result = await _sittings.CreateAsync(form);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e);
            return View(form);
        }
        TempData["Success"] = "Sitting created.";
        return RedirectToAction(nameof(Index));
    }

    // GET /Sittings/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await _sittings.GetAsync(id);
        if (s == null) return NotFound();
        return View(new SittingFormViewModel
        {
            SittingId = s.SittingId,
            SittingType = s.SittingType,
            SittingDate = s.SittingDate,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            Capacity = s.Capacity,
            IsClosed = s.IsClosed,
            RowVersion = s.RowVersion
        });
    }

    // POST /Sittings/Edit/5
    [HttpPost]
    public async Task<IActionResult> Edit(int id, SittingFormViewModel form)
    {
        if (id != form.SittingId) return BadRequest();
        if (!ModelState.IsValid) return View(form);

        var result = await _sittings.UpdateAsync(form);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e);
            return View(form);
        }
        TempData["Success"] = "Sitting updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Close(int id)
    {
        var result = await _sittings.SetClosedAsync(id, true);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Sitting closed." : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Reopen(int id)
    {
        var result = await _sittings.SetClosedAsync(id, false);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Sitting reopened." : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }

    // GET /Sittings/Delete/5 (confirmation page)
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await _sittings.GetAsync(id);
        return s == null ? NotFound() : View(s);
    }

    // POST /Sittings/Delete/5
    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var result = await _sittings.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Sitting deleted." : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }
}

public class StoreHourInput
{
    public int StoreHourId { get; set; }
    public bool IsOpen { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int Capacity { get; set; }
}