using BeanScene.Web.Data;
using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BeanScene.Web.Controllers;

/// <summary>The booking page. Guests and members book online (source = Online) and see a confirmation page.
/// Staff and managers choose the source and stay on the page, so they can book one after another.</summary>
[AllowAnonymous]
public class BookingController : Controller
{
    private readonly IReservationService _reservations;
    private readonly ISittingService _sittings;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;

    public BookingController(IReservationService reservations, ISittingService sittings,
        UserManager<ApplicationUser> users, ApplicationDbContext db)
    {
        _reservations = reservations;
        _sittings = sittings;
        _users = users;
        _db = db;
    }

    private bool IsStaff => User.IsInRole(RoleNames.Staff) || User.IsInRole(RoleNames.Manager);

    [HttpGet]
    public async Task<IActionResult> Index(int? sittingId)
    {
        var input = new ReservationInput
        {
            Source = IsStaff ? ReservationSource.Phone : ReservationSource.Online,
            SittingId = sittingId ?? 0
        };
        await PrefillFromMemberAsync(input);
        await LoadSittingsAsync(input.SittingId);
        return View(input);
    }

    [HttpPost]
    public async Task<IActionResult> Index(ReservationInput input)
    {
        var staff = IsStaff;
        if (!staff) input.Source = ReservationSource.Online;

        var memberId = CurrentMemberId();
        if (memberId != null)
        {
            // A logged-in member's details always come from their account.
            ModelState.Clear();
            await PrefillFromMemberAsync(input);
            TryValidateModel(input);
        }

        // The staff page saves in the background and asks for a JSON answer.
        var background = Request.Headers["X-Requested-With"] == "fetch";

        if (ModelState.IsValid)
        {
            var result = await _reservations.CreateAsync(input, memberId);
            if (result.Succeeded)
            {
                if (!staff)
                    return RedirectToAction(nameof(Confirmation), new { id = result.Value!.ReservationId });

                var message = $"Booked: {input.FirstName} {input.LastName}, {input.NumberOfGuests} guests at {input.StartTime:h:mm tt} (Pending).";
                if (background) return Json(new { ok = true, message });
                TempData["Success"] = message;
                return RedirectToAction(nameof(Index), new { sittingId = input.SittingId });
            }

            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
        }

        if (background)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Please check the form." : e.ErrorMessage)
                .Distinct().ToList();
            return Json(new { ok = false, errors });
        }

        await LoadSittingsAsync(input.SittingId);
        return View(input);
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(int id)
    {
        var reservation = await _reservations.GetAsync(id);
        if (reservation == null) return NotFound();
        return View(reservation);
    }

    private string? CurrentMemberId()
    {
        if (User.Identity?.IsAuthenticated != true || !User.IsInRole(RoleNames.Member)) return null;
        return _users.GetUserId(User);
    }

    private async Task PrefillFromMemberAsync(ReservationInput input)
    {
        var userId = CurrentMemberId();
        if (userId == null) return;

        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);
        if (customer == null) return;
        input.FirstName = customer.FirstName;
        input.LastName = customer.LastName;
        input.Email = customer.Email;
        input.PhoneNumber = customer.PhoneNumber;
    }

    private async Task LoadSittingsAsync(int selected)
    {
        var open = await _sittings.GetOpenUpcomingAsync();
        ViewBag.Sittings = new SelectList(open, nameof(Sitting.SittingId), nameof(Sitting.DisplayName), selected);
        ViewBag.IsMember = User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Member);
    }
}