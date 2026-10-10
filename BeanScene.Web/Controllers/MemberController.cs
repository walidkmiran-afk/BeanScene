using BeanScene.Web.Models;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BeanScene.Web.Controllers;

/// <summary>Members see (and can cancel) only their own reservations.</summary>
[Authorize(Roles = RoleNames.Member)]
public class MemberController : Controller
{
    private readonly IReservationService _reservations;
    private readonly UserManager<ApplicationUser> _users;

    public MemberController(IReservationService reservations, UserManager<ApplicationUser> users)
    {
        _reservations = reservations;
        _users = users;
    }

    public async Task<IActionResult> Index()
    {
        var mine = await _reservations.GetForMemberAsync(_users.GetUserId(User)!);
        return View(mine);
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var mine = await _reservations.GetForMemberAsync(_users.GetUserId(User)!);
        if (mine.All(r => r.ReservationId != id)) return Forbid();   // not their reservation

        var result = await _reservations.ChangeStatusAsync(id, ReservationStatus.Cancelled);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Reservation cancelled." : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }
}
