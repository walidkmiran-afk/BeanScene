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
    private readonly ApplicationDbContext _db;
    private readonly IReservationService _reservations;

    public SittingsController(ApplicationDbContext db, IReservationService reservations)
    {
        _db = db;
        _reservations = reservations;
    }

    // GET /Sittings?year=2026&month=10&selected=2026-10-14
    public async Task<IActionResult> Index(int? year, int? month, DateOnly? selected)
    {
        // No day chosen: open one straight away. Today for the current month, otherwise the 1st of the month shown.
        if (selected == null)
        {
            var openNow = RestaurantClock.Today;
            var openYear = Math.Clamp(year ?? openNow.Year, 1, 9998);
            var openMonth = month is >= 1 and <= 12 ? month.Value : openNow.Month;
            selected = (openYear == openNow.Year && openMonth == openNow.Month) ? openNow : new DateOnly(openYear, openMonth, 1);
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var y = year ?? selected?.Year ?? today.Year;
        var m = month ?? selected?.Month ?? today.Month;
        if (m < 1 || m > 12 || y < 2000 || y > 2100) { y = today.Year; m = today.Month; }

        var first = new DateOnly(y, m, 1);
        var gridStart = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));   // the Monday on or before the 1st
        var gridEnd = gridStart.AddDays(41);                                  // 6 weeks

        var hours = await _db.StoreHours.AsNoTracking().ToListAsync();
        hours = hours.OrderBy(h => ((int)h.DayOfWeek + 6) % 7).ThenBy(h => h.SittingType).ToList();

        var sittings = await _db.Sittings.AsNoTracking()
            .Where(s => s.SittingDate >= gridStart && s.SittingDate <= gridEnd)
            .ToListAsync();
        var byDate = sittings.ToLookup(s => s.SittingDate);

        var days = new List<CalendarDay>();
        for (var i = 0; i < 42; i++)
        {
            var date = gridStart.AddDays(i);
            var day = new CalendarDay
            {
                Date = date,
                InMonth = date.Month == m,
                Sittings = byDate[date].OrderBy(s => s.StartTime).ToList()
            };
            // Future days with no sittings yet (more than 90 days ahead) show the weekly hours.
            if (day.Sittings.Count == 0 && date >= today)
            {
                day.Projected = true;
                day.Sittings = hours
                    .Where(h => h.DayOfWeek == date.DayOfWeek && h.IsOpen)
                    .OrderBy(h => h.StartTime)
                    .Select(h => new Sitting
                    {
                        SittingType = h.SittingType,
                        SittingDate = date,
                        StartTime = h.StartTime,
                        EndTime = h.EndTime,
                        Capacity = h.Capacity
                    })
                    .ToList();
            }
            days.Add(day);
        }

        var selectedItems = new List<SittingListItem>();
        var dayBookings = new List<DayBooking>();
        var areas = new List<Area>();
        if (selected.HasValue)
        {
            selectedItems = await _db.Sittings.AsNoTracking()
                .Where(s => s.SittingDate == selected.Value)
                .OrderBy(s => s.StartTime)
                .Select(s => new SittingListItem
                {
                    Sitting = s,
                    GuestsBooked = s.Reservations
                        .Where(r => r.Status != ReservationStatus.Cancelled)
                        .Sum(r => (int?)r.NumberOfGuests) ?? 0
                })
                .ToListAsync();

            dayBookings = await _db.Reservations.AsNoTracking()
                .Where(r => r.Sitting.SittingDate == selected.Value)
                .OrderBy(r => r.StartTime)
                .Select(r => new DayBooking
                {
                    ReservationId = r.ReservationId,
                    OriginalLabel = r.OriginalLabel,
                    FirstName = r.Customer.FirstName,
                    LastName = r.Customer.LastName,
                    Email = r.Customer.Email,
                    RowVersion = r.RowVersion,
                    SittingId = r.SittingId,
                    Name = r.Customer.FirstName + " " + r.Customer.LastName,
                    Phone = r.Customer.PhoneNumber,
                    Start = r.StartTime,
                    Minutes = r.DurationMinutes,
                    Guests = r.NumberOfGuests,
                    Status = r.Status,
                    Source = r.Source,
                    Requests = r.SpecialRequests,
                    TableIds = r.ReservationTables.Select(rt => rt.TableId).ToList(),
                    TableNames = r.ReservationTables.Select(rt => rt.Table.TableName).OrderBy(n => n).ToList()
                })
                .ToListAsync();

            // Tables for the Assign tables form (only needed when the day has bookings).
            if (dayBookings.Count > 0)
            {
                areas = await _db.Areas.AsNoTracking().Include(a => a.Tables).OrderBy(a => a.AreaId).ToListAsync();
                foreach (var area in areas)
                    area.Tables = area.Tables.OrderBy(t => t.TableId).ToList();

                foreach (var b in dayBookings.Where(b => b.Status is ReservationStatus.Pending or ReservationStatus.Confirmed or ReservationStatus.Seated))
                    b.UnavailableTableIds = await _reservations.GetUnavailableTableIdsAsync(b.ReservationId);
            }
        }

        return View(new ScheduleViewModel
        {
            Year = y,
            Month = m,
            Today = today,
            Selected = selected,
            Hours = hours,
            Days = days,
            SelectedSittings = selectedItems,
            DayBookings = dayBookings,
            Areas = areas
        });
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

        // Open sittings on the same day must not overlap.
        foreach (var dayGroup in rows.Where(r => r.IsOpen).GroupBy(r => r.DayOfWeek))
        {
            var ordered = dayGroup.OrderBy(r => r.StartTime).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].StartTime < ordered[i - 1].EndTime)
                {
                    TempData["Error"] = $"{dayGroup.Key}: {ordered[i - 1].SittingType} and {ordered[i].SittingType} overlap. Change the times so they do not overlap.";
                    return RedirectToAction(nameof(Index));
                }
            }
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

    // POST /Sittings/SaveDay  (the day editor under the calendar)
    [HttpPost]
    public async Task<IActionResult> SaveDay(DateOnly date, List<DaySittingInput> sittings)
    {
        IActionResult Back() => RedirectToAction(nameof(Index), new { selected = date.ToString("yyyy-MM-dd") });
        sittings ??= new List<DaySittingInput>();

        foreach (var s in sittings)
        {
            if (s.Capacity < 1 || s.Capacity > 500)
            {
                TempData["Error"] = "Seats must be between 1 and 500.";
                return Back();
            }
            if (s.IsOpen && s.EndTime <= s.StartTime)
            {
                TempData["Error"] = "Each open sitting must end after it starts.";
                return Back();
            }
        }

        var openInputs = sittings.Where(s => s.IsOpen).OrderBy(s => s.StartTime).ToList();
        for (var i = 1; i < openInputs.Count; i++)
        {
            if (openInputs[i].StartTime < openInputs[i - 1].EndTime)
            {
                TempData["Error"] = "Two open sittings overlap. Change the times so they do not overlap.";
                return Back();
            }
        }

        var existing = await _db.Sittings.Where(x => x.SittingDate == date).ToListAsync();
        var hours = await _db.StoreHours.AsNoTracking().Where(h => h.DayOfWeek == date.DayOfWeek).ToListAsync();

        await using var tx = await _db.Database.BeginTransactionAsync();

        var day = new List<Sitting>();                 // every sitting that stays on this day
        var postedIds = new HashSet<int>();

        foreach (var input in sittings)
        {
            var label = CleanLabel(input.Label);

            if (input.SittingId == 0)
            {
                var added = new Sitting
                {
                    SittingType = SittingType.Special,
                    Label = label,
                    SittingDate = date,
                    StartTime = input.StartTime,
                    EndTime = input.EndTime,
                    Capacity = input.Capacity,
                    IsClosed = !input.IsOpen,
                    IsCustomised = true
                };
                _db.Sittings.Add(added);
                day.Add(added);
                continue;
            }

            var s = existing.FirstOrDefault(x => x.SittingId == input.SittingId);
            if (s == null) continue;
            postedIds.Add(s.SittingId);

            s.StartTime = input.StartTime;
            s.EndTime = input.EndTime;
            s.Capacity = input.Capacity;
            s.IsClosed = !input.IsOpen;

            if (s.SittingType == SittingType.Special)
            {
                s.Label = label;
                s.IsCustomised = true;
            }
            else
            {
                // A label that is just the sitting's own name is not a custom label.
                s.Label = string.Equals(label, s.SittingType.ToString(), StringComparison.OrdinalIgnoreCase) ? null : label;

                // A regular sitting is "changed" only if it differs from the weekly hours.
                var h = hours.FirstOrDefault(x => x.SittingType == s.SittingType);
                var same = h != null && h.IsOpen == input.IsOpen
                           && (!h.IsOpen || (h.StartTime == input.StartTime && h.EndTime == input.EndTime && h.Capacity == input.Capacity));
                s.IsCustomised = !same || s.Label != null;
            }
            day.Add(s);
        }

        // Regular sittings are never removed here (they are closed instead), so keep any that were not posted.
        day.AddRange(existing.Where(x => x.SittingType != SittingType.Special && !postedIds.Contains(x.SittingId)));

        // Special sittings that were removed from the page.
        var removed = existing.Where(x => x.SittingType == SittingType.Special && !postedIds.Contains(x.SittingId)).ToList();

        // Move or cancel the bookings on this day to match the new sittings.
        var (moved, cancelled, error) = await ReassignBookingsAsync(date, day);
        if (error != null)
        {
            TempData["Error"] = error;
            return Back();
        }

        await _db.SaveChangesAsync();

        // A removed sitting is deleted, unless bookings (even cancelled ones) still belong to it. Then it stays closed.
        foreach (var s in removed)
        {
            var hasBookings = await _db.Reservations.AnyAsync(r => r.SittingId == s.SittingId);
            if (hasBookings) { s.IsClosed = true; s.IsCustomised = true; }
            else _db.Sittings.Remove(s);
        }
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Success"] = DayMessage("Day saved.", moved, cancelled);
        return Back();
    }

    // POST /Sittings/ResetDay  (puts the day back on the weekly hours: regular sittings reset, Special sittings removed)
    [HttpPost]
    public async Task<IActionResult> ResetDay(DateOnly date)
    {
        IActionResult Back() => RedirectToAction(nameof(Index), new { selected = date.ToString("yyyy-MM-dd") });

        var existing = await _db.Sittings.Where(x => x.SittingDate == date).ToListAsync();
        var hours = await _db.StoreHours.AsNoTracking().Where(h => h.DayOfWeek == date.DayOfWeek).ToListAsync();

        await using var tx = await _db.Database.BeginTransactionAsync();

        // Special sittings are not part of the weekly hours, so they are closed first. Their bookings then move to the
        // regular sitting that covers their time, or are cancelled if none does.
        var specials = existing.Where(x => x.SittingType == SittingType.Special).ToList();
        foreach (var s in specials) s.IsClosed = true;

        var day = new List<Sitting>(existing);

        foreach (var h in hours)
        {
            var s = existing.FirstOrDefault(x => x.SittingType == h.SittingType);
            if (s == null)
            {
                if (h.IsOpen)
                {
                    var added = new Sitting
                    {
                        SittingType = h.SittingType,
                        SittingDate = date,
                        StartTime = h.StartTime,
                        EndTime = h.EndTime,
                        Capacity = h.Capacity
                    };
                    _db.Sittings.Add(added);
                    day.Add(added);
                }
                continue;
            }
            s.Label = null;
            s.StartTime = h.StartTime;
            s.EndTime = h.EndTime;
            s.Capacity = h.Capacity;
            s.IsClosed = !h.IsOpen;
            s.IsCustomised = false;
        }

        var (moved, cancelled, error) = await ReassignBookingsAsync(date, day);
        if (error != null)
        {
            TempData["Error"] = error;
            return Back();
        }

        await _db.SaveChangesAsync();

        // A Special sitting is deleted, unless bookings (even cancelled ones) still belong to it. Then it stays closed.
        foreach (var s in specials)
        {
            var hasBookings = await _db.Reservations.AnyAsync(r => r.SittingId == s.SittingId);
            if (hasBookings) s.IsCustomised = true;
            else _db.Sittings.Remove(s);
        }
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Success"] = DayMessage("Day reset to the weekly hours.", moved, cancelled);
        return Back();
    }

    /// <summary>
    /// The booking's time never changes. A booking whose time is inside an open sitting belongs to that sitting
    /// (only its label changes). A Pending or Confirmed booking whose time is not inside any open sitting is cancelled.
    /// </summary>
    private async Task<(int Moved, int Cancelled, string? Error)> ReassignBookingsAsync(DateOnly date, List<Sitting> daySittings)
    {
        var open = daySittings.Where(s => !s.IsClosed).ToList();

        var bookings = await _db.Reservations
            .Include(r => r.Sitting)
            .Where(r => r.Sitting.SittingDate == date && r.Status != ReservationStatus.Cancelled)
            .ToListAsync();

        // A sitting that was renamed: its bookings remember the old name (stored once, never overwritten).
        foreach (var s in daySittings.Where(x => x.SittingId != 0))
        {
            var entry = _db.Entry(s);
            if (entry.State != EntityState.Modified) continue;
            var oldLabel = entry.Property(x => x.Label).OriginalValue;
            var oldName = string.IsNullOrWhiteSpace(oldLabel) ? s.SittingType.ToString() : oldLabel;
            if (oldName == s.Name) continue;
            foreach (var r in bookings.Where(r => ReferenceEquals(r.Sitting, s)))
                r.OriginalLabel ??= oldName;
        }

        int moved = 0, cancelled = 0;
        foreach (var r in bookings)
        {
            var target = open.FirstOrDefault(s => s.StartTime <= r.StartTime && r.StartTime < s.EndTime);
            if (target == null)
            {
                if (r.Status is ReservationStatus.Pending or ReservationStatus.Confirmed)
                {
                    r.Cancel();
                    cancelled++;
                }
                continue;
            }
            if (!ReferenceEquals(target, r.Sitting))
            {
                r.OriginalLabel ??= r.Sitting.Name;   // stored once, never overwritten
                r.Sitting = target;
                moved++;
            }
        }

        foreach (var s in open)
        {
            var guests = bookings
                .Where(r => ReferenceEquals(r.Sitting, s) && r.Status != ReservationStatus.Cancelled)
                .Sum(r => r.NumberOfGuests);
            if (guests > s.Capacity)
                return (0, 0, $"{s.Name} would have {guests} guests booked but only {s.Capacity} seats. Increase the seats or change the times.");
        }

        return (moved, cancelled, null);
    }

    private static string? CleanLabel(string? label)
    {
        var l = (label ?? "").Trim();
        if (l.Length > 40) l = l.Substring(0, 40);
        return l == "" ? null : l;
    }

    private static string DayMessage(string start, int moved, int cancelled)
    {
        var parts = new List<string>();
        if (moved > 0) parts.Add($"{moved} booking(s) moved to another sitting");
        if (cancelled > 0) parts.Add($"{cancelled} booking(s) cancelled");
        return parts.Count == 0 ? start : start + " " + string.Join(", ", parts) + ".";
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

public class DaySittingInput
{
    public int SittingId { get; set; }   // 0 = a new sitting
    public string? Label { get; set; }
    public bool IsOpen { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int Capacity { get; set; }
}

public class DayBooking
{
    public int ReservationId { get; set; }
    public string? OriginalLabel { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
    public Guid RowVersion { get; set; }
    public int SittingId { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public TimeOnly Start { get; set; }
    public int Minutes { get; set; }
    public int Guests { get; set; }
    public ReservationStatus Status { get; set; }
    public ReservationSource Source { get; set; }
    public string? Requests { get; set; }
    public List<int> TableIds { get; set; } = new();
    public List<string> TableNames { get; set; } = new();
    public HashSet<int> UnavailableTableIds { get; set; } = new();
}

public class CalendarDay
{
    public DateOnly Date { get; set; }
    public bool InMonth { get; set; }
    public bool Projected { get; set; }
    public List<Sitting> Sittings { get; set; } = new();
}

public class ScheduleViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly Today { get; set; }
    public DateOnly? Selected { get; set; }
    public List<StoreHour> Hours { get; set; } = new();
    public List<CalendarDay> Days { get; set; } = new();
    public List<SittingListItem> SelectedSittings { get; set; } = new();
    public List<DayBooking> DayBookings { get; set; } = new();
    public List<Area> Areas { get; set; } = new();
}