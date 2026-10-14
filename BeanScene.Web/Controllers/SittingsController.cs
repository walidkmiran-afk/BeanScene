using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeanScene.Web.Controllers;

/// <summary>Only the Manager can schedule sittings.</summary>
[Authorize(Roles = RoleNames.Manager)]
public class SittingsController : Controller
{
    private readonly ISittingService _sittings;

    public SittingsController(ISittingService sittings) => _sittings = sittings;

    // GET /Sittings
    public async Task<IActionResult> Index(bool showPast = false)
    {
        var from = showPast ? (DateOnly?)null : RestaurantClock.Today;
        ViewBag.ShowPast = showPast;
        return View(await _sittings.ListAsync(from));
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
            SittingId = s.SittingId, SittingType = s.SittingType, SittingDate = s.SittingDate,
            StartTime = s.StartTime, EndTime = s.EndTime, Capacity = s.Capacity,
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
