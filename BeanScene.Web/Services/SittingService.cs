using BeanScene.Web.Data;
using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BeanScene.Web.Services;

public interface ISittingService
{
    Task<List<SittingListItem>> ListAsync(DateOnly? from = null);
    Task<Sitting?> GetAsync(int id);
    Task<List<Sitting>> GetOpenUpcomingAsync();
    Task<OperationResult> CreateAsync(SittingFormViewModel form);
    Task<OperationResult> UpdateAsync(SittingFormViewModel form);
    Task<OperationResult> SetClosedAsync(int id, bool closed, ClosedReason reason);
    Task<OperationResult> DeleteAsync(int id);
}

public class SittingService : ISittingService
{
    private readonly ApplicationDbContext _db;

    public SittingService(ApplicationDbContext db) => _db = db;

    public async Task<List<SittingListItem>> ListAsync(DateOnly? from = null)
    {
        var query = _db.Sittings.AsNoTracking().AsQueryable();
        if (from.HasValue) query = query.Where(s => s.SittingDate >= from.Value);

        return await query
            .OrderBy(s => s.SittingDate).ThenBy(s => s.StartTime)
            .Select(s => new SittingListItem
            {
                Sitting = s,
                GuestsBooked = s.Reservations
                    .Where(r => r.Status != ReservationStatus.Cancelled)
                    .Sum(r => (int?)r.NumberOfGuests) ?? 0
            })
            .ToListAsync();
    }

    public Task<Sitting?> GetAsync(int id) => _db.Sittings.FirstOrDefaultAsync(s => s.SittingId == id);

    public async Task<List<Sitting>> GetOpenUpcomingAsync()
    {
        var now = RestaurantClock.Now;
        var today = DateOnly.FromDateTime(now);
        var timeNow = TimeOnly.FromDateTime(now);
        // Sittings that have already finished today are not offered any more.
        return await _db.Sittings.AsNoTracking()
            .Where(s => !s.IsClosed && (s.SittingDate > today || (s.SittingDate == today && s.EndTime > timeNow)))
            .OrderBy(s => s.SittingDate).ThenBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<OperationResult> CreateAsync(SittingFormViewModel form)
    {
        if (form.SittingType != SittingType.Special &&
            await _db.Sittings.AnyAsync(s => s.SittingDate == form.SittingDate && s.SittingType == form.SittingType))
            return OperationResult.Fail($"A {form.SittingType} sitting already exists on {form.SittingDate:d MMM yyyy}.");

        _db.Sittings.Add(new Sitting
        {
            SittingType = form.SittingType,
            SittingDate = form.SittingDate,
            StartTime = form.StartTime,
            EndTime = form.EndTime,
            Capacity = form.Capacity,
            IsClosed = form.IsClosed,
            ClosedReason = form.IsClosed ? form.ClosedReason : ClosedReason.None
        });
        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> UpdateAsync(SittingFormViewModel form)
    {
        var sitting = await _db.Sittings.FirstOrDefaultAsync(s => s.SittingId == form.SittingId);
        if (sitting == null) return OperationResult.Fail("Sitting not found.");

        var booked = await _db.Reservations
            .Where(r => r.SittingId == sitting.SittingId && r.Status != ReservationStatus.Cancelled)
            .SumAsync(r => (int?)r.NumberOfGuests) ?? 0;
        if (form.Capacity < booked)
            return OperationResult.Fail($"The capacity cannot be lower than the {booked} guests already booked.");

        _db.Entry(sitting).Property(s => s.RowVersion).OriginalValue = form.RowVersion;

        sitting.SittingType = form.SittingType;
        sitting.SittingDate = form.SittingDate;
        sitting.StartTime = form.StartTime;
        sitting.EndTime = form.EndTime;
        sitting.Capacity = form.Capacity;
        sitting.IsClosed = form.IsClosed;
        sitting.ClosedReason = form.IsClosed ? form.ClosedReason : ClosedReason.None;

        try
        {
            await _db.SaveChangesAsync();
            return OperationResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return OperationResult.Fail("This sitting was changed by someone else. Reload it and try again.");
        }
    }

    public async Task<OperationResult> SetClosedAsync(int id, bool closed, ClosedReason reason)
    {
        var sitting = await GetAsync(id);
        if (sitting == null) return OperationResult.Fail("Sitting not found.");
        sitting.IsClosed = closed;
        sitting.ClosedReason = closed ? (reason == ClosedReason.None ? ClosedReason.BookedOut : reason) : ClosedReason.None;
        try
        {
            await _db.SaveChangesAsync();
            return OperationResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return OperationResult.Fail("This sitting was changed by someone else. Reload it and try again.");
        }
    }

    public async Task<OperationResult> DeleteAsync(int id)
    {
        var sitting = await GetAsync(id);
        if (sitting == null) return OperationResult.Fail("Sitting not found.");
        if (await _db.Reservations.AnyAsync(r => r.SittingId == id))
            return OperationResult.Fail("This sitting has reservations. Close it instead of deleting it.");
        _db.Sittings.Remove(sitting);
        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }
}
