using BeanScene.Web.Data;
using BeanScene.Web.Models;
using BeanScene.Web.Reports;
using BeanScene.Web.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BeanScene.Tests;

/// <summary>Functional and database tests. Each one uses its own in-memory database.</summary>
public class ReservationServiceTests
{
    // ---------- Functional ----------

    [Fact]
    public async Task Create_ValidInput_SavesPendingReservationAndCustomer()
    {
        using var t = new TestDb();
        await using var db = t.NewContext();

        var result = await new ReservationService(db).CreateAsync(t.Input(guests: 4));

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        await using var check = t.NewContext();
        var saved = await check.Reservations.Include(r => r.Customer).SingleAsync();
        Assert.Equal(ReservationStatus.Pending, saved.Status);
        Assert.Equal("Smith", saved.Customer.LastName);
        Assert.Equal(4, saved.NumberOfGuests);
    }

    [Fact]
    public async Task Create_WhenSittingWouldBeOverCapacity_IsRejected()
    {
        using var t = new TestDb();
        await using (var db = t.NewContext())
            Assert.True((await new ReservationService(db).CreateAsync(t.Input(guests: 8))).Succeeded);

        await using var db2 = t.NewContext();
        var result = await new ReservationService(db2).CreateAsync(t.Input(guests: 3, last: "Jones", email: "jones@example.com"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("capacity"));
        await using var check = t.NewContext();
        Assert.Equal(1, await check.Reservations.CountAsync());
    }

    [Fact]
    public async Task Create_ForClosedSitting_IsRejected()
    {
        using var t = new TestDb();
        await using var db = t.NewContext();

        var result = await new ReservationService(db).CreateAsync(t.Input(sittingId: t.ClosedSittingId, startHour: 13));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("closed"));
    }

    [Fact]
    public async Task Create_StartTimeOutsideTheSitting_IsRejected()
    {
        using var t = new TestDb();
        await using var db = t.NewContext();

        var result = await new ReservationService(db).CreateAsync(t.Input(startHour: 9));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("start time"));
    }

    [Fact]
    public async Task ChangeStatus_SeatWithoutTables_IsRejected_ThenAllowedOnceTablesAreAssigned()
    {
        using var t = new TestDb();
        int id;
        await using (var db = t.NewContext())
            id = (await new ReservationService(db).CreateAsync(t.Input())).Value!.ReservationId;

        await using (var db = t.NewContext())
        {
            var seat = await new ReservationService(db).ChangeStatusAsync(id, ReservationStatus.Seated);
            Assert.False(seat.Succeeded);
        }

        await using (var db = t.NewContext())
            Assert.True((await new ReservationService(db).AssignTablesAsync(id, new[] { t.TableM1 })).Succeeded);

        await using (var db = t.NewContext())
            Assert.True((await new ReservationService(db).ChangeStatusAsync(id, ReservationStatus.Seated)).Succeeded);

        await using var check = t.NewContext();
        Assert.Equal(ReservationStatus.Seated, (await check.Reservations.FindAsync(id))!.Status);
    }

    // ---------- Database / ORM ----------

    [Fact]
    public async Task AssignTables_Valid_PersistsManyToManyLinks()
    {
        using var t = new TestDb();
        int id;
        await using (var db = t.NewContext())
            id = (await new ReservationService(db).CreateAsync(t.Input(guests: 6))).Value!.ReservationId;

        await using (var db = t.NewContext())
        {
            var result = await new ReservationService(db).AssignTablesAsync(id, new[] { t.TableM1, t.TableM2 });
            Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        }

        await using var check = t.NewContext();
        var tables = await check.ReservationTables.Where(rt => rt.ReservationId == id).Select(rt => rt.TableId).ToListAsync();
        Assert.Equal(2, tables.Count);
        Assert.Contains(t.TableM1, tables);
        Assert.Contains(t.TableM2, tables);
    }

    [Fact]
    public async Task AssignTables_TableUsedByOverlappingReservation_IsRejected()
    {
        using var t = new TestDb();
        int a, b;
        await using (var db = t.NewContext())
            a = (await new ReservationService(db).CreateAsync(t.Input(startHour: 19, last: "A", email: "a@example.com"))).Value!.ReservationId;
        await using (var db = t.NewContext())
            b = (await new ReservationService(db).CreateAsync(t.Input(startHour: 19, startMinute: 30, last: "B", email: "b@example.com"))).Value!.ReservationId;

        await using (var db = t.NewContext())
            Assert.True((await new ReservationService(db).AssignTablesAsync(a, new[] { t.TableM1 })).Succeeded);

        await using (var db = t.NewContext())
        {
            var clash = await new ReservationService(db).AssignTablesAsync(b, new[] { t.TableM1 });
            Assert.False(clash.Succeeded);
            Assert.Contains(clash.Errors, e => e.Contains("M1"));
        }

        await using (var db = t.NewContext())
            Assert.True((await new ReservationService(db).AssignTablesAsync(b, new[] { t.TableM2 })).Succeeded);
    }

    [Fact]
    public async Task AssignTables_WithUnknownTable_RollsBackAndSavesNothing()
    {
        using var t = new TestDb();
        int id;
        await using (var db = t.NewContext())
            id = (await new ReservationService(db).CreateAsync(t.Input())).Value!.ReservationId;

        await using (var db = t.NewContext())
        {
            var result = await new ReservationService(db).AssignTablesAsync(id, new[] { t.TableM1, 99999 });
            Assert.False(result.Succeeded);
        }

        await using var check = t.NewContext();
        Assert.Equal(0, await check.ReservationTables.CountAsync());   // transaction rolled back
    }

    [Fact]
    public async Task Update_WithStaleRowVersion_IsRejectedAsConcurrencyConflict()
    {
        using var t = new TestDb();
        int id; Guid originalVersion;
        await using (var db = t.NewContext())
        {
            var created = (await new ReservationService(db).CreateAsync(t.Input(guests: 4))).Value!;
            id = created.ReservationId;
            originalVersion = created.RowVersion;
        }

        // First user saves with the version they loaded.
        await using (var db = t.NewContext())
        {
            var input = t.Input(guests: 3);
            input.ReservationId = id;
            input.RowVersion = originalVersion;
            Assert.True((await new ReservationService(db).UpdateAsync(input)).Succeeded);
        }

        // Second user still holds the old version.
        await using (var db = t.NewContext())
        {
            var stale = t.Input(guests: 2);
            stale.ReservationId = id;
            stale.RowVersion = originalVersion;
            var result = await new ReservationService(db).UpdateAsync(stale);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.Contains("changed by someone else"));
        }

        await using var check = t.NewContext();
        Assert.Equal(3, (await check.Reservations.FindAsync(id))!.NumberOfGuests);   // first user's change survived
    }

    [Fact]
    public async Task TwoContexts_EditingTheSameRow_RaiseDbUpdateConcurrencyException()
    {
        using var t = new TestDb();
        await using var first = t.NewContext();
        await using var second = t.NewContext();

        var a = await first.Sittings.FirstAsync(s => s.SittingId == t.OpenSittingId);
        var b = await second.Sittings.FirstAsync(s => s.SittingId == t.OpenSittingId);

        a.Capacity = 20;
        await first.SaveChangesAsync();

        b.Capacity = 30;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task LazyLoading_LoadsNavigationPropertyOnFirstAccess()
    {
        using var t = new TestDb();
        await using (var db = t.NewContext())
            await new ReservationService(db).CreateAsync(t.Input());

        await using var ctx = t.NewContext();
        var reservation = await ctx.Reservations.FirstAsync();             // no Include
        var entry = ctx.Entry(reservation).Reference(r => r.Customer);

        Assert.False(entry.IsLoaded);
        Assert.Equal("Smith", reservation.Customer.LastName);              // triggers the lazy load
        Assert.True(entry.IsLoaded);
    }

    [Fact]
    public async Task EagerLoading_IncludeLoadsRelatedDataInTheSameQuery()
    {
        using var t = new TestDb();
        await using (var db = t.NewContext())
            await new ReservationService(db).CreateAsync(t.Input());

        await using var ctx = t.NewContext();
        var reservation = await ctx.Reservations.Include(r => r.Customer).Include(r => r.Sitting).FirstAsync();

        Assert.True(ctx.Entry(reservation).Reference(r => r.Customer).IsLoaded);
        Assert.True(ctx.Entry(reservation).Reference(r => r.Sitting).IsLoaded);
    }

    [Fact]
    public async Task DetachedEntity_CanBeReattachedAndSaved()
    {
        using var t = new TestDb();
        Area detached;
        await using (var ctx = t.NewContext())
            detached = await ctx.Areas.AsNoTracking().FirstAsync();
        Assert.NotNull(detached);

        detached.AreaName = "Main Dining";
        await using (var ctx = t.NewContext())
        {
            Assert.Equal(EntityState.Detached, ctx.Entry(detached).State);
            ctx.Attach(detached);
            ctx.Entry(detached).State = EntityState.Modified;
            await ctx.SaveChangesAsync();
        }

        await using var check = t.NewContext();
        Assert.Equal("Main Dining", (await check.Areas.FirstAsync()).AreaName);
    }

    // ---------- Reporting (polymorphism) ----------

    [Fact]
    public async Task StatusSummaryReport_CountsReservationsAndGuestsPerStatus()
    {
        using var t = new TestDb();
        int firstId;
        await using (var db = t.NewContext())
            firstId = (await new ReservationService(db).CreateAsync(t.Input(guests: 2, last: "A", email: "a@example.com"))).Value!.ReservationId;
        await using (var db = t.NewContext())
            await new ReservationService(db).CreateAsync(t.Input(guests: 3, last: "B", email: "b@example.com"));
        await using (var db = t.NewContext())
            await new ReservationService(db).ChangeStatusAsync(firstId, ReservationStatus.Confirmed);

        await using var ctx = t.NewContext();
        var view = await new ReportService(ctx).RunAsync(t.TestDate, t.TestDate);

        Assert.Equal(2, view.TotalReservations);
        Assert.Equal(5, view.TotalGuests);

        IReport report = new StatusSummaryReport();   // used through the interface
        var rows = report.Generate(await ctx.Reservations.Include(r => r.Sitting).ToListAsync()).Rows;
        Assert.Equal(1, rows.Single(r => r.Label == "Confirmed").Reservations);
        Assert.Equal(3, rows.Single(r => r.Label == "Pending").Guests);
    }
}
