using BeanScene.Web.Data;
using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BeanScene.Web.Services;

public class ReservationService : IReservationService
{
    private readonly ApplicationDbContext _db;

    public ReservationService(ApplicationDbContext db) => _db = db;

    // ------------------------------------------------------------------ queries

    public Task<Reservation?> GetAsync(int reservationId) =>
        BaseQuery().FirstOrDefaultAsync(r => r.ReservationId == reservationId);

    private IQueryable<Reservation> BaseQuery() =>
        _db.Reservations
            .Include(r => r.Sitting)
            .Include(r => r.Customer)
            .Include(r => r.ReservationTables).ThenInclude(rt => rt.Table).ThenInclude(t => t.Area);

    public async Task<List<Reservation>> SearchAsync(ReservationSearchCriteria c)
    {
        var query = BaseQuery().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(c.Name))
        {
            var name = c.Name.Trim();
            query = query.Where(r => r.Customer.FirstName.Contains(name) || r.Customer.LastName.Contains(name)
                                     || r.Customer.Email.Contains(name));
        }
        if (c.From.HasValue) query = query.Where(r => r.Sitting.SittingDate >= c.From.Value);
        if (c.To.HasValue) query = query.Where(r => r.Sitting.SittingDate <= c.To.Value);
        if (c.SittingId.HasValue) query = query.Where(r => r.SittingId == c.SittingId.Value);
        if (c.Status.HasValue) query = query.Where(r => r.Status == c.Status.Value);
        if (c.Source.HasValue) query = query.Where(r => r.Source == c.Source.Value);
        if (!string.IsNullOrWhiteSpace(c.TableName))
        {
            var table = c.TableName.Trim();
            query = query.Where(r => r.ReservationTables.Any(rt => rt.Table.TableName == table));
        }

        return await query.OrderBy(r => r.Sitting.SittingDate).ThenBy(r => r.StartTime).ToListAsync();
    }

    public Task<List<Reservation>> GetPendingAsync() =>
        BaseQuery().AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Pending)
            .ToListAsync();

    public Task<List<Reservation>> GetForMemberAsync(string userId) =>
        BaseQuery().AsNoTracking()
            .Where(r => r.Customer.UserId == userId)
            .OrderByDescending(r => r.Sitting.SittingDate).ThenBy(r => r.StartTime)
            .ToListAsync();

    public async Task<int> GuestsBookedAsync(int sittingId, int? excludeReservationId = null)
    {
        var total = await _db.Reservations
            .Where(r => r.SittingId == sittingId && r.Status != ReservationStatus.Cancelled
                        && (excludeReservationId == null || r.ReservationId != excludeReservationId))
            .SumAsync(r => (int?)r.NumberOfGuests);
        return total ?? 0;
    }

    // ------------------------------------------------------------------ create

    public async Task<OperationResult<Reservation>> CreateAsync(ReservationInput input, string? memberUserId = null)
    {
        var sitting = await _db.Sittings.FirstOrDefaultAsync(s => s.SittingId == input.SittingId);
        var errors = await ValidateSlotAsync(sitting, input, null);
        if (errors.Count > 0) return OperationResult<Reservation>.Fail(errors.ToArray());

        // Explicit transaction: the customer and the reservation are saved together or not at all.
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var customer = await FindOrCreateCustomerAsync(input, memberUserId);
            var reservation = new Reservation
            {
                Sitting = sitting!,
                Customer = customer,
                StartTime = input.StartTime,
                DurationMinutes = input.DurationMinutes,
                NumberOfGuests = input.NumberOfGuests,
                Source = input.Source,
                SpecialRequests = input.SpecialRequests
            };
            _db.Reservations.Add(reservation);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return OperationResult<Reservation>.Ok(reservation);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _db.ChangeTracker.Clear();
            return OperationResult<Reservation>.Fail("The reservation could not be saved: " + ex.Message);
        }
    }

    private async Task<Customer> FindOrCreateCustomerAsync(ReservationInput input, string? memberUserId)
    {
        Customer? customer = null;
        if (memberUserId != null)
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == memberUserId);

        if (customer == null)
        {
            var email = input.Email.Trim().ToLower();
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == email);
        }

        if (customer == null)
        {
            customer = new Customer { Email = input.Email.Trim() };
            _db.Customers.Add(customer);
        }

        customer.FirstName = input.FirstName.Trim();
        customer.LastName = input.LastName.Trim();
        customer.PhoneNumber = input.PhoneNumber.Trim();
        return customer;
    }

    // ------------------------------------------------------------------ update

    public async Task<OperationResult> UpdateAsync(ReservationInput input)
    {
        var reservation = await _db.Reservations.Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.ReservationId == input.ReservationId);
        if (reservation == null) return OperationResult.Fail("Reservation not found.");

        if (reservation.Status is ReservationStatus.Seated or ReservationStatus.Completed or ReservationStatus.Cancelled)
            return OperationResult.Fail($"A {reservation.Status} reservation can no longer be edited.");

        var sitting = await _db.Sittings.FirstOrDefaultAsync(s => s.SittingId == input.SittingId);
        var errors = await ValidateSlotAsync(sitting, input, reservation.ReservationId);
        if (errors.Count > 0) return OperationResult.Fail(errors.ToArray());

        // Tell EF which version the user's form was based on. If someone else saved first,
        // the UPDATE ... WHERE RowVersion = @original affects 0 rows and EF throws DbUpdateConcurrencyException.
        _db.Entry(reservation).Property(r => r.RowVersion).OriginalValue = input.RowVersion;

        reservation.SittingId = input.SittingId;
        reservation.StartTime = input.StartTime;
        reservation.DurationMinutes = input.DurationMinutes;
        reservation.NumberOfGuests = input.NumberOfGuests;
        reservation.Source = input.Source;
        reservation.SpecialRequests = input.SpecialRequests;
        reservation.Customer.FirstName = input.FirstName.Trim();
        reservation.Customer.LastName = input.LastName.Trim();
        reservation.Customer.PhoneNumber = input.PhoneNumber.Trim();

        try
        {
            await _db.SaveChangesAsync();
            return OperationResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return OperationResult.Fail("This reservation was changed by someone else. Reload it and try again.");
        }
    }

    // ------------------------------------------------------------------ status

    public async Task<OperationResult> ChangeStatusAsync(int reservationId, ReservationStatus target)
    {
        var reservation = await _db.Reservations.Include(r => r.ReservationTables)
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId);
        if (reservation == null) return OperationResult.Fail("Reservation not found.");

        try
        {
            switch (target)
            {
                case ReservationStatus.Confirmed: reservation.Confirm(); break;
                case ReservationStatus.Cancelled: reservation.Cancel(); break;
                case ReservationStatus.Completed: reservation.Complete(); break;
                case ReservationStatus.Seated:
                    if (reservation.ReservationTables.Count == 0)
                        return OperationResult.Fail("Assign at least one table before seating the guests.");
                    reservation.Seat();
                    break;
                default:
                    return OperationResult.Fail("A reservation cannot be moved back to Pending.");
            }
            await _db.SaveChangesAsync();
            return OperationResult.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult.Fail(ex.Message);   // illegal status change
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return OperationResult.Fail("This reservation was changed by someone else. Reload it and try again.");
        }
    }

    // ------------------------------------------------------------------ tables

    public async Task<HashSet<int>> GetUnavailableTableIdsAsync(int reservationId)
    {
        var reservation = await _db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.ReservationId == reservationId);
        return reservation == null ? new HashSet<int>() : await UnavailableTableIdsAsync(reservation);
    }

    /// <summary>Tables already held by another live reservation in the same sitting whose time overlaps.</summary>
    private async Task<HashSet<int>> UnavailableTableIdsAsync(Reservation reservation)
    {
        var others = await _db.ReservationTables
            .Where(rt => rt.ReservationId != reservation.ReservationId
                         && rt.Reservation.SittingId == reservation.SittingId
                         && rt.Reservation.Status != ReservationStatus.Cancelled
                         && rt.Reservation.Status != ReservationStatus.Completed)
            .Select(rt => new { rt.TableId, rt.Reservation.StartTime, rt.Reservation.DurationMinutes })
            .ToListAsync();

        var start = reservation.StartMinutes;
        var end = reservation.EndMinutes;
        return others
            .Where(o =>
            {
                var otherStart = o.StartTime.Hour * 60 + o.StartTime.Minute;
                var otherEnd = otherStart + o.DurationMinutes;
                return start < otherEnd && otherStart < end;
            })
            .Select(o => o.TableId)
            .ToHashSet();
    }

    public async Task<OperationResult> AssignTablesAsync(int reservationId, IEnumerable<int> tableIds)
    {
        var wanted = tableIds.Distinct().ToList();
        if (wanted.Count == 0) return OperationResult.Fail("Select at least one table.");

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var reservation = await _db.Reservations.Include(r => r.ReservationTables)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId);
            if (reservation == null)
            {
                await transaction.RollbackAsync();
                return OperationResult.Fail("Reservation not found.");
            }
            if (reservation.Status is ReservationStatus.Cancelled or ReservationStatus.Completed)
            {
                await transaction.RollbackAsync();
                return OperationResult.Fail($"Tables cannot be assigned to a {reservation.Status} reservation.");
            }

            var tables = await _db.Tables.Where(t => wanted.Contains(t.TableId)).ToListAsync();
            if (tables.Count != wanted.Count)
            {
                await transaction.RollbackAsync();
                return OperationResult.Fail("One or more selected tables do not exist.");
            }

            var unavailable = await UnavailableTableIdsAsync(reservation);
            var clashes = tables.Where(t => unavailable.Contains(t.TableId)).Select(t => t.TableName).ToList();
            if (clashes.Count > 0)
            {
                await transaction.RollbackAsync();
                return OperationResult.Fail("Already allocated to an overlapping reservation: " + string.Join(", ", clashes));
            }

            // Work out the difference so no row is removed and re-added with the same key.
            var current = reservation.ReservationTables.ToList();
            foreach (var link in current.Where(l => !wanted.Contains(l.TableId)))
                _db.ReservationTables.Remove(link);
            foreach (var tableId in wanted.Where(id => current.All(l => l.TableId != id)))
                reservation.ReservationTables.Add(new ReservationTable { ReservationId = reservationId, TableId = tableId });

            // Make sure the reservation row itself is version-checked and its RowVersion changes.
            _db.Entry(reservation).Property(r => r.UpdatedAt).IsModified = true;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();                         // commit: everything saved together
            return OperationResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();                       // rollback: nothing saved
            _db.ChangeTracker.Clear();
            return OperationResult.Fail("This reservation was changed by someone else. Reload it and try again.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _db.ChangeTracker.Clear();
            return OperationResult.Fail("The tables could not be assigned: " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ delete

    public async Task<OperationResult> DeleteAsync(int reservationId)
    {
        var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.ReservationId == reservationId);
        if (reservation == null) return OperationResult.Fail("Reservation not found.");
        if (reservation.Status is ReservationStatus.Seated or ReservationStatus.Completed)
            return OperationResult.Fail("A seated or completed reservation is kept for reporting and cannot be deleted.");

        _db.Reservations.Remove(reservation);   // ReservationTable rows are removed by cascade
        await _db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    // ------------------------------------------------------------------ validation

    private async Task<List<string>> ValidateSlotAsync(Sitting? sitting, ReservationInput input, int? excludeReservationId)
    {
        var errors = new List<string>();
        if (sitting == null)
        {
            errors.Add("The selected sitting does not exist.");
            return errors;
        }

        if (sitting.IsClosed)
            errors.Add($"This sitting is closed to reservations ({sitting.ClosedReason}).");

        if (input.StartTime < sitting.StartTime || input.StartTime >= sitting.EndTime)
            errors.Add($"The start time must be between {sitting.StartTime:h:mm tt} and {sitting.EndTime:h:mm tt}.");

        // The whole reservation must fit inside the sitting (compare in minutes so a late finish cannot wrap past midnight).
        var finishesAt = input.StartTime.Hour * 60 + input.StartTime.Minute + input.DurationMinutes;
        var sittingEnds = sitting.EndTime.Hour * 60 + sitting.EndTime.Minute;
        if (finishesAt > sittingEnds)
            errors.Add($"The reservation would finish after the sitting ends at {sitting.EndTime:h:mm tt}. Choose an earlier start time or a shorter duration.");

        // New reservations cannot be made for a time that has already passed.
        if (excludeReservationId == null && sitting.SittingDate.ToDateTime(input.StartTime) < RestaurantClock.Now)
            errors.Add("That start time has already passed. Please choose a later time.");

        var booked = await GuestsBookedAsync(sitting.SittingId, excludeReservationId);
        if (booked + input.NumberOfGuests > sitting.Capacity)
            errors.Add($"Not enough capacity: {booked} of {sitting.Capacity} guests are already booked for this sitting.");


        // The same guest (matched by email) cannot hold two active reservations that overlap in time on one sitting.
        var email = input.Email.Trim().ToLower();
        var theirs = await _db.Reservations
            .Where(r => r.SittingId == sitting.SittingId && r.Status != ReservationStatus.Cancelled
                        && r.Customer.Email.ToLower() == email
                        && (excludeReservationId == null || r.ReservationId != excludeReservationId))
            .Select(r => new { r.StartTime, r.DurationMinutes })
            .ToListAsync();
        var newStart = finishesAt - input.DurationMinutes;
        if (theirs.Any(o => { var s = o.StartTime.Hour * 60 + o.StartTime.Minute; return s < finishesAt && newStart < s + o.DurationMinutes; }))
            errors.Add("This account already has a reservation for the selected time. Choose a different time or sitting, or change the existing reservation.");

        return errors;
    }
}
