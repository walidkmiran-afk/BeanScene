using BeanScene.Web.Data;
using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BeanScene.Web.Services;

/// <summary>
/// Runs small, self-cleaning experiments that show each ORM feature in the assessment brief.
/// The Manager runs it from the "ORM Demo" page, so the output can be used as screenshot evidence.
/// Every experiment that writes data creates its own temporary "Demo-..." rows and deletes them afterwards.
/// </summary>
public class OrmDemoService
{
    private readonly ApplicationDbContext _db;
    private readonly IServiceScopeFactory _scopes;

    public OrmDemoService(ApplicationDbContext db, IServiceScopeFactory scopes)
    {
        _db = db;
        _scopes = scopes;
    }

    public async Task<List<OrmDemoStep>> RunAllAsync()
    {
        var steps = new List<OrmDemoStep>();
        steps.Add(await RunStepAsync("1. Eager loading",
            "Include() loads the related Sitting, Customer and Tables in the same SQL query (a JOIN).", EagerLoadingAsync));
        steps.Add(await RunStepAsync("2. Lazy loading",
            "With lazy-loading proxies, a navigation property is loaded the first time it is read (an extra query).", LazyLoadingAsync));
        steps.Add(await RunStepAsync("3. Explicit loading",
            "Entry(...).Collection(...).Load() loads related data on request, without proxies.", ExplicitLoadingAsync));
        steps.Add(await RunStepAsync("4. Entity states: attached, detached, reattached",
            "The change tracker follows each entity: Unchanged, Modified, Detached. A detached entity can be reattached and saved.", EntityStatesAsync));
        steps.Add(await RunStepAsync("5. Create and persist single objects and collections",
            "One Area with a collection of Tables is added and saved inside a transaction that is committed.", TransactionCommitAsync));
        steps.Add(await RunStepAsync("6. Transaction rollback",
            "A failure after the first save rolls the whole transaction back, so nothing is stored.", TransactionRollbackAsync));
        steps.Add(await RunStepAsync("7. Concurrency conflict",
            "Two contexts edit the same row. The second save fails because the RowVersion changed (optimistic concurrency).", ConcurrencyAsync));
        steps.Add(await RunStepAsync("8. Exception handling",
            "Breaking a unique index raises DbUpdateException. The code catches it, clears the tracker and carries on.", ExceptionHandlingAsync));
        _db.ChangeTracker.Clear();
        return steps;
    }

    private async Task<OrmDemoStep> RunStepAsync(string title, string explanation, Func<List<string>, Task> body)
    {
        var step = new OrmDemoStep { Title = title, Explanation = explanation };
        try
        {
            _db.ChangeTracker.Clear();
            await body(step.Lines);
        }
        catch (Exception ex)
        {
            step.Succeeded = false;
            step.Lines.Add("FAILED: " + ex.GetType().Name + " - " + ex.Message);
            _db.ChangeTracker.Clear();
        }
        return step;
    }

    private async Task EagerLoadingAsync(List<string> log)
    {
        var r = await _db.Reservations.AsNoTracking()
            .Include(x => x.Sitting)
            .Include(x => x.Customer)
            .Include(x => x.ReservationTables).ThenInclude(rt => rt.Table)
            .OrderBy(x => x.ReservationId)
            .FirstOrDefaultAsync();
        if (r == null) { log.Add("There are no reservations yet. Create one and run the demo again."); return; }

        log.Add($"Loaded reservation #{r.ReservationId} with ONE query using Include.");
        log.Add($"Sitting: {r.Sitting.DisplayName}");
        log.Add($"Guest: {r.Customer.FullName}");
        log.Add($"Tables assigned: {r.ReservationTables.Count}");
        log.Add("Check the Output window (Microsoft.EntityFrameworkCore.Database.Command) to see the single JOIN query.");
    }

    private async Task LazyLoadingAsync(List<string> log)
    {
        var r = await _db.Reservations.OrderBy(x => x.ReservationId).FirstOrDefaultAsync();
        if (r == null) { log.Add("There are no reservations yet."); return; }

        var customerEntry = _db.Entry(r).Reference(x => x.Customer);
        log.Add($"Reservation #{r.ReservationId} loaded WITHOUT Include. Customer loaded yet? {customerEntry.IsLoaded}");
        var name = r.Customer.FullName;        // first read of the navigation property triggers a query
        log.Add($"Read r.Customer.FullName = \"{name}\" (lazy-load query ran).");
        log.Add($"Customer loaded now? {customerEntry.IsLoaded}");
        log.Add($"Proxy type: {r.GetType().Name}");
    }

    private async Task ExplicitLoadingAsync(List<string> log)
    {
        var sitting = await _db.Sittings.OrderBy(s => s.SittingDate).ThenBy(s => s.StartTime).FirstAsync();
        var collection = _db.Entry(sitting).Collection(s => s.Reservations);
        log.Add($"Sitting {sitting.SittingId}: Reservations loaded yet? {collection.IsLoaded}");
        await collection.LoadAsync();
        log.Add($"After LoadAsync: loaded = {collection.IsLoaded}, count = {sitting.Reservations.Count}");
    }

    private async Task EntityStatesAsync(List<string> log)
    {
        var name = "Demo-" + Guid.NewGuid().ToString("N")[..8];
        var demo = new Area { AreaName = name };

        log.Add($"New object, not yet added: state = {_db.Entry(demo).State}");
        _db.Areas.Add(demo);
        log.Add($"After Add: state = {_db.Entry(demo).State}");
        await _db.SaveChangesAsync();
        log.Add($"After SaveChanges: state = {_db.Entry(demo).State} (attached and tracked)");

        demo.AreaName = name + "-edited";
        _db.ChangeTracker.DetectChanges();
        log.Add($"After changing a property: state = {_db.Entry(demo).State}");
        await _db.SaveChangesAsync();

        _db.Entry(demo).State = EntityState.Detached;
        log.Add($"After detaching: state = {_db.Entry(demo).State}");

        demo.AreaName = name + "-reattached";            // edit while detached (nothing is tracked)
        _db.Attach(demo);                                // reattach
        log.Add($"After Attach: state = {_db.Entry(demo).State}");
        _db.Entry(demo).State = EntityState.Modified;    // tell EF the detached edits must be saved
        await _db.SaveChangesAsync();
        log.Add("Saved the edit made while the entity was detached.");

        var stored = await _db.Areas.AsNoTracking().FirstAsync(a => a.AreaId == demo.AreaId);
        log.Add($"Database now holds AreaName = \"{stored.AreaName}\"");

        _db.Areas.Remove(demo);
        await _db.SaveChangesAsync();
        log.Add("Cleaned up the demo row.");
    }

    private async Task TransactionCommitAsync(List<string> log)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var area = new Area { AreaName = "Demo-Commit-" + suffix };
        // A collection of objects is persisted together with its parent.
        area.Tables.Add(new Table { TableName = "D1" + suffix, Capacity = 2 });
        area.Tables.Add(new Table { TableName = "D2" + suffix, Capacity = 4 });

        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            _db.Areas.Add(area);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        log.Add($"Committed Area \"{area.AreaName}\" with {area.Tables.Count} tables (one object plus a collection).");

        _db.ChangeTracker.Clear();
        var count = await _db.Tables.CountAsync(t => t.AreaId == area.AreaId);
        log.Add($"Retrieved from the database: {count} tables for this area.");

        // clean up (delete operation)
        var toRemove = await _db.Areas.Include(a => a.Tables).FirstAsync(a => a.AreaId == area.AreaId);
        _db.Tables.RemoveRange(toRemove.Tables);
        _db.Areas.Remove(toRemove);
        await _db.SaveChangesAsync();
        log.Add("Deleted the demo rows.");
    }

    private async Task TransactionRollbackAsync(List<string> log)
    {
        var name = "Demo-Rollback-" + Guid.NewGuid().ToString("N")[..6];
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Areas.Add(new Area { AreaName = name });
            await _db.SaveChangesAsync();
            var visible = await _db.Areas.AnyAsync(a => a.AreaName == name);
            log.Add($"Inside the transaction the row is visible: {visible}");
            throw new InvalidOperationException("Simulated failure after the first save.");
        }
        catch (InvalidOperationException ex)
        {
            log.Add("Caught: " + ex.Message);
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            log.Add("Transaction rolled back.");
        }

        var exists = await _db.Areas.AnyAsync(a => a.AreaName == name);
        log.Add($"After rollback the row exists: {exists}");
    }

    private async Task ConcurrencyAsync(List<string> log)
    {
        var demo = new Area { AreaName = "Demo-Concurrency-" + Guid.NewGuid().ToString("N")[..6] };
        _db.Areas.Add(demo);
        await _db.SaveChangesAsync();
        var id = demo.AreaId;
        _db.ChangeTracker.Clear();

        using var scope = _scopes.CreateScope();
        var other = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();   // a second user's context

        var mine = await _db.Areas.FirstAsync(a => a.AreaId == id);
        var theirs = await other.Areas.FirstAsync(a => a.AreaId == id);
        log.Add($"Both users loaded the row. RowVersion = {mine.RowVersion}");

        theirs.AreaName += "-edited-by-other-user";
        await other.SaveChangesAsync();
        log.Add("Other user saved first. RowVersion in the database changed.");

        mine.AreaName += "-edited-by-me";
        try
        {
            await _db.SaveChangesAsync();
            log.Add("Unexpected: my save succeeded.");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            log.Add("My save FAILED with DbUpdateConcurrencyException - the conflict was detected.");

            var entry = ex.Entries.Single();
            var databaseValues = await entry.GetDatabaseValuesAsync();
            log.Add($"Value in the database: \"{databaseValues!["AreaName"]}\"");

            // Recovery: keep my change but base it on the latest version, then retry.
            entry.OriginalValues.SetValues(databaseValues);
            await _db.SaveChangesAsync();
            log.Add("Recovered: refreshed the original values and saved again (client wins).");
        }

        _db.ChangeTracker.Clear();
        var final = await _db.Areas.FirstAsync(a => a.AreaId == id);
        log.Add($"Final value: \"{final.AreaName}\"");
        _db.Areas.Remove(final);
        await _db.SaveChangesAsync();
        log.Add("Cleaned up the demo row.");
    }

    private async Task ExceptionHandlingAsync(List<string> log)
    {
        var existing = await _db.Areas.AsNoTracking().OrderBy(a => a.AreaId).FirstOrDefaultAsync();
        if (existing == null) { log.Add("There are no areas to duplicate."); return; }

        try
        {
            _db.Areas.Add(new Area { AreaName = existing.AreaName });   // violates the unique index on AreaName
            await _db.SaveChangesAsync();
            log.Add("Unexpected: the duplicate was saved.");
        }
        catch (DbUpdateException ex)
        {
            log.Add($"Caught {ex.GetType().Name}: duplicate area name \"{existing.AreaName}\" was rejected by the database.");
            log.Add("Inner message: " + (ex.InnerException?.Message ?? ex.Message));
            _db.ChangeTracker.Clear();
            log.Add("Change tracker cleared. The application keeps running.");
        }
    }
}
