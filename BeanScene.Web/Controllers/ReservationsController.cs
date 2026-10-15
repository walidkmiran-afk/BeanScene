using BeanScene.Web.DataStructures;
using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeanScene.Web.Controllers;

/// <summary>Staff and managers search and update reservations. New bookings are made on the Book a table page.</summary>
[Authorize(Roles = RoleNames.StaffOrManager)]
public class ReservationsController : Controller
{
    private readonly IReservationService _reservations;

    public ReservationsController(IReservationService reservations)
    {
        _reservations = reservations;
    }

    // GET /Reservations?Name=...&Status=...   (search)
    public async Task<IActionResult> Index(ReservationSearchCriteria criteria)
    {
        // Default view: reservations from today onwards.
        if (!criteria.From.HasValue && !criteria.To.HasValue && string.IsNullOrWhiteSpace(criteria.Name)
            && !criteria.Status.HasValue && !criteria.Source.HasValue && string.IsNullOrWhiteSpace(criteria.TableName)
            && !criteria.SittingId.HasValue)
            criteria.From = RestaurantClock.Today;

        var list = await _reservations.SearchAsync(criteria);

        ViewBag.Criteria = criteria;
        ViewBag.Unavailable = await UnavailableTablesAsync(list);
        return View(list);
    }

    // POST /Reservations/EditFromSchedule  (the Edit form under a booking)
    [HttpPost]
    public async Task<IActionResult> EditFromSchedule(ReservationInput input, string? returnTo)
    {
        var back = !string.IsNullOrEmpty(returnTo) && Url.IsLocalUrl(returnTo) ? returnTo : Url.Action("Index", "Sittings")!;
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return LocalRedirect(back);
        }
        var result = await _reservations.UpdateAsync(input);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Booking updated." : string.Join(" ", result.Errors);
        return LocalRedirect(back);
    }

    // POST /Reservations/AddFromSchedule  (the + Add booking form on the Sittings page)
    [HttpPost]
    public async Task<IActionResult> AddFromSchedule(ReservationInput input, string? returnTo)
    {
        var back = !string.IsNullOrEmpty(returnTo) && Url.IsLocalUrl(returnTo) ? returnTo : Url.Action("Index", "Sittings")!;
        input.ReservationId = 0;   // always a new booking
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return LocalRedirect(back);
        }
        var note = await _reservations.DescribeGuestAsync(input);   // looked up before saving
        var result = await _reservations.CreateAsync(input);
        TempData[result.Succeeded ? "Success" : "Error"] =
            result.Succeeded ? $"Booking added for {input.Name}. {note}" : string.Join(" ", result.Errors);
        return LocalRedirect(back);
    }

    // POST /Reservations/AssignFromSchedule  (the Tables form under a booking)
    [HttpPost]
    public async Task<IActionResult> AssignFromSchedule(int id, List<int> selectedTableIds, string? returnTo)
    {
        var result = await _reservations.AssignTablesAsync(id, selectedTableIds ?? new List<int>());
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Tables assigned." : string.Join(" ", result.Errors);
        if (!string.IsNullOrEmpty(returnTo) && Url.IsLocalUrl(returnTo)) return LocalRedirect(returnTo);
        return RedirectToAction(nameof(Index));
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
        return RedirectToAction(nameof(Index));
    }

    // POST /Reservations/Delete/5 (Manager only)
    [HttpPost, Authorize(Roles = RoleNames.Manager)]
    public async Task<IActionResult> Delete(int id, string? returnTo)
    {
        var result = await _reservations.DeleteAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Reservation deleted." : string.Join(" ", result.Errors);
        if (!string.IsNullOrEmpty(returnTo) && Url.IsLocalUrl(returnTo)) return LocalRedirect(returnTo);
        return RedirectToAction(nameof(Index));
    }

    // GET /Reservations/FollowUp  - Pending reservations, soonest sitting first (linked list + merge sort),
    // searched with the same fields as the Reservations page.
    public async Task<IActionResult> FollowUp(ReservationSearchCriteria criteria)
    {
        criteria.Status = ReservationStatus.Pending;   // this list is always the pending ones

        var searching = !string.IsNullOrWhiteSpace(criteria.Name) || criteria.From.HasValue || criteria.To.HasValue
            || criteria.Source.HasValue || !string.IsNullOrWhiteSpace(criteria.TableName);

        criteria.From ??= RestaurantClock.Today;   // past bookings are not listed unless an earlier date is searched

        var pending = await _reservations.SearchAsync(criteria);
        var queue = new PendingFollowUpQueue(pending);

        ViewBag.Criteria = criteria;
        ViewBag.Searching = searching;
        ViewBag.TotalPending = searching ? (await _reservations.GetPendingAsync()).Count : pending.Count;
        ViewBag.Unavailable = await UnavailableTablesAsync(pending);
        return View(queue);
    }

    // Tables held by another booking at an overlapping time (greyed out in the table picker),
    // worked out for each booking that can still be given tables.
    private async Task<Dictionary<int, HashSet<int>>> UnavailableTablesAsync(IEnumerable<Reservation> reservations)
    {
        var result = new Dictionary<int, HashSet<int>>();
        foreach (var r in reservations.Where(r => r.Status != ReservationStatus.Cancelled && r.Status != ReservationStatus.Completed))
            result[r.ReservationId] = await _reservations.GetUnavailableTableIdsAsync(r.ReservationId);
        return result;
    }
}