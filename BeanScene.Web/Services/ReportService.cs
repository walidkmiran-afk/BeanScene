using BeanScene.Web.Data;
using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using BeanScene.Web.Reports;
using Microsoft.EntityFrameworkCore;

namespace BeanScene.Web.Services;

public interface IReportService
{
    Task<ReportsViewModel> RunAsync(DateOnly from, DateOnly to);
}

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;

    // Polymorphism: a list of the interface type holding different report classes.
    private readonly IReadOnlyList<IReport> _reports = new List<IReport>
    {
        new StatusSummaryReport(),
        new SourceSummaryReport(),
        new SittingSummaryReport()
    };

    public ReportService(ApplicationDbContext db) => _db = db;

    public async Task<ReportsViewModel> RunAsync(DateOnly from, DateOnly to)
    {
        var reservations = await _db.Reservations.AsNoTracking()
            .Include(r => r.Sitting)
            .Where(r => r.Sitting.SittingDate >= from && r.Sitting.SittingDate <= to)
            .ToListAsync();

        var live = reservations.Where(r => r.Status != ReservationStatus.Cancelled).ToList();
        var sittings = await _db.Sittings.AsNoTracking()
            .Where(s => s.SittingDate >= from && s.SittingDate <= to && !s.IsClosed)
            .ToListAsync();
        var capacity = sittings.Sum(s => s.Capacity);
        var guests = live.Sum(r => r.NumberOfGuests);

        return new ReportsViewModel
        {
            From = from,
            To = to,
            TotalReservations = reservations.Count,
            TotalGuests = guests,
            CancelledReservations = reservations.Count - live.Count,
            UtilisationPercent = capacity == 0 ? 0 : Math.Round(guests * 100.0 / capacity, 1),
            Reports = _reports.Select(r => r.Generate(reservations)).ToList()
        };
    }
}
