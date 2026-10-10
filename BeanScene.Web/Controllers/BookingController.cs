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

/// <summary>The public "book online" page used by guests and members (source = Online).</summary>
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

    [HttpGet]
    public async Task<IActionResult> Index(int? sittingId)
    {
        var input = new ReservationInput { Source = ReservationSource.Online, SittingId = sittingId ?? 0 };
        await PrefillFromMemberAsync(input);
        await LoadSittingsAsync(input.SittingId);
        return View(input);
    }

    [HttpPost]
    public async Task<IActionResult> Index(ReservationInput input)
    {
        input.Source = ReservationSource.Online;
        var memberId = CurrentMemberId();
        if (memberId != null)
        {
            // A logged-in member's details always come from their account.
            ModelState.Clear();
            await PrefillFromMemberAsync(input);
            TryValidateModel(input);
        }

        if (ModelState.IsValid)
        {
            var result = await _reservations.CreateAsync(input, memberId);
            if (result.Succeeded)
                return RedirectToAction(nameof(Confirmation), new { id = result.Value!.ReservationId });

            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
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
