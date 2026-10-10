using BeanScene.Web.DataStructures;
using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BeanScene.Web.Controllers;

/// <summary>Staff and managers capture, search and update reservations.</summary>
[Authorize(Roles = RoleNames.StaffOrManager)]
public class ReservationsController : Controller
{
    private readonly IReservationService _reservations;
    private readonly ISittingService _sittings;

    public ReservationsController(IReservationService reservations, ISittingService sittings)
    {
        _reservations = reservations;
        _sittings = sittings;
    }

    // GET /Reservations?Name=...&Status=...   (search)
    public async Task<IActionResult> Index(ReservationSearchCriteria criteria)
    {
        // Default view: reservations from today onwards.
        if (!criteria.From.HasValue && !criteria.To.HasValue && string.IsNullOrWhiteSpace(criteria.Name)
            && !criteria.Status.HasValue && !criteria.Source.HasValue && string.IsNullOrWhiteSpace(criteria.TableName)
            && !criteria.SittingId.HasValue)
            criteria.From = DateOnly.FromDateTime(DateTime.Today);

        ViewBag.Criteria = criteria;
        return View(await _reservations.SearchAsync(criteria));
    }

    // GET /Reservations/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var r = await _reservations.GetAsync(id);
        return r == null ? NotFound() : View(r);
    }

    // GET /Reservations/Create
    [HttpGet]
    public async Task<IActionResult> Create(int? sittingId)
    {
        var input = new ReservationInput { SittingId = sittingId ?? 0 };
        await LoadSittingsAsync(input.SittingId, includePast: false);
        return View(input);
    }

    // POST /Reservations/Create
    [HttpPost]
    public async Task<IActionResult> Create(ReservationInput input)
    {
        if (ModelState.IsValid)
        {
            var result = await _reservations.CreateAsync(input);
            if (result.Succeeded)
            {
                TempData["Success"] = "Reservation created (Pending). Assign tables when ready.";
                return RedirectToAction(nameof(Details), new { id = result.Value!.ReservationId });
            }
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e);
        }
        await LoadSittingsAsync(input.SittingId, includePast: false);
        return View(input);
    }

    // GET /Reservations/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var r = await _reservations.GetAsync(id);
        if (r == null) return NotFound();

        var input = new ReservationInput
        {
            ReservationId = r.ReservationId, SittingId = r.SittingId, StartTime = r.StartTime,
            DurationMinutes = r.DurationMinutes, NumberOfGuests = r.NumberOfGuests, Source = r.Source,
            FirstName = r.Customer.FirstName, LastName = r.Customer.LastName, Email = r.Customer.Email,
            PhoneNumber = r.Customer.PhoneNumber, SpecialRequests = r.SpecialRequests, RowVersion = r.RowVersion
        };
        await LoadSittingsAsync(input.SittingId, includePast: true);
        return View(input);
    }

    // POST /Reservations/Edit/5
    [HttpPost]
    public async Task<IActionResult> Edit(int id, ReservationInput input)
    {
        if (id != input.ReservationId) return BadRequest();

        if (ModelState.IsValid)
        {
            var result = await _reservations.UpdateAsync(input);
            if (result.Succeeded)
            {
                TempData["Success"] = "Reservation updated.";
                return RedirectToAction(nameof(Details), new { id });
            }
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e);
        }
        await LoadSittingsAsync(input.SittingId, includePast: true);
        return View(input);
    }

    [HttpPost] public Task<IActionResult> Confirm(int id, string? returnTo) => ChangeStatus(id, ReservationStatus.Confirmed, returnTo);
    [HttpPost] public Task<IActionResult> Seat(int id, string? returnTo) => ChangeStatus(id, ReservationStatus.Seated, returnTo);
    [HttpPost] public Task<IActionResult> Complete(int id, string? returnTo) => ChangeStatus(id, ReservationStatus.Completed, returnTo);
    [HttpPost] public Task<IActionResult> Cancel(int id, string? returnTo) => ChangeStatus(id, ReservationStatus.Cancelled, returnTo);

    private async Task<IActionResult> ChangeStatus(int id, ReservationStatus target, string? returnTo)
    {
        var result = await _reservations.ChangeStatusAsync(id, target);
        TempData[result.Succeeded ? "Success" : "Error"] =
            result.Succeeded ? $"Reservation is now {target}." : string.Join(" ", result.Errors);

        if (!string.IsNullOrEmpty(returnTo) && Url.IsLocalUrl(returnTo)) return LocalRedirect(returnTo);
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /Reservations/Delete/5 (Manager only)
    [HttpPost, Authorize(Roles = RoleNames.Manager)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _reservations.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Reservation deleted." : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }

    // GET /Reservations/FollowUp  - Pending reservations, soonest sitting first (linked list + merge sort)
    public async Task<IActionResult> FollowUp(string? lastName)
    {
        var queue = new PendingFollowUpQueue(await _reservations.GetPendingAsync());
        ViewBag.LastName = lastName;
        ViewBag.Found = string.IsNullOrWhiteSpace(lastName) ? null : queue.FindByLastName(lastName.Trim());
        return View(queue);
    }

    private async Task LoadSittingsAsync(int selected, bool includePast)
    {
        var from = includePast ? DateOnly.FromDateTime(DateTime.Today).AddDays(-30) : DateOnly.FromDateTime(DateTime.Today);
        var items = await _sittings.ListAsync(from);
        ViewBag.Sittings = new SelectList(items.Select(i => i.Sitting).Where(s => !s.IsClosed || s.SittingId == selected),
            nameof(Sitting.SittingId), nameof(Sitting.DisplayName), selected);
    }
}
