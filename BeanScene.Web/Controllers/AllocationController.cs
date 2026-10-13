using BeanScene.Web.Data;
using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeanScene.Web.Controllers;

/// <summary>Assigns one or more tables (from the areas) to a reservation.</summary>
[Authorize(Roles = RoleNames.StaffOrManager)]
public class AllocationController : Controller
{
    private readonly IReservationService _reservations;
    private readonly ApplicationDbContext _db;

    public AllocationController(IReservationService reservations, ApplicationDbContext db)
    {
        _reservations = reservations;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Allocate(int id)
    {
        var model = await BuildModelAsync(id);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Allocate(int id, List<int> selectedTableIds)
    {
        var result = await _reservations.AssignTablesAsync(id, selectedTableIds ?? new List<int>());
        if (result.Succeeded)
        {
            TempData["Success"] = "Tables assigned.";
            return RedirectToAction("Index", "Reservations");
        }

        foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e);
        var model = await BuildModelAsync(id);
        if (model == null) return NotFound();
        model.SelectedTableIds = (selectedTableIds ?? new List<int>()).ToHashSet();
        return View(model);
    }

    private async Task<AllocationViewModel?> BuildModelAsync(int reservationId)
    {
        var reservation = await _reservations.GetAsync(reservationId);
        if (reservation == null) return null;

        var areas = await _db.Areas.AsNoTracking().Include(a => a.Tables).OrderBy(a => a.AreaId).ToListAsync();
        foreach (var area in areas)
            area.Tables = area.Tables.OrderBy(t => t.TableId).ToList();

        return new AllocationViewModel
        {
            Reservation = reservation,
            Areas = areas,
            SelectedTableIds = reservation.ReservationTables.Select(rt => rt.TableId).ToHashSet(),
            UnavailableTableIds = await _reservations.GetUnavailableTableIdsAsync(reservationId)
        };
    }
}
