using BeanScene.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BeanScene.Web.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();

        // With migrations (the assessment way) apply them; before the first migration exists, just create the schema.
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();

        await SeedRolesAsync(sp.GetRequiredService<RoleManager<ApplicationRole>>());
        await SeedAreasAndTablesAsync(db);
        await SeedStoreHoursAsync(db);

        var password = config["Seed:Password"] ?? "BeanScene!2026";
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(userManager, db, "manager@beanscene.test", "Maria", "Manager", "0400000001", RoleNames.Manager, password);
        await EnsureUserAsync(userManager, db, "staff@beanscene.test", "Sam", "Staff", "0400000002", RoleNames.Staff, password);
        await EnsureUserAsync(userManager, db, "member@beanscene.test", "Mia", "Member", "0400000003", RoleNames.Member, password);

        await SeedSittingsAndReservationsAsync(db);
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roles)
    {
        foreach (var name in new[] { RoleNames.Manager, RoleNames.Staff, RoleNames.Member })
            if (!await roles.RoleExistsAsync(name))
                await roles.CreateAsync(new ApplicationRole(name));
    }

    private static async Task SeedAreasAndTablesAsync(ApplicationDbContext db)
    {
        if (await db.Areas.AnyAsync()) return;

        var layout = new (string Area, string Prefix, int Capacity)[]
        {
            ("Main", "M", 4), ("Outside", "O", 4), ("Balcony", "B", 2)
        };
        foreach (var (areaName, prefix, capacity) in layout)
        {
            var area = new Area { AreaName = areaName };
            for (var i = 1; i <= 10; i++)
                area.Tables.Add(new Table { TableName = $"{prefix}{i}", Capacity = capacity });
            db.Areas.Add(area);
        }
        await db.SaveChangesAsync();
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> users, ApplicationDbContext db,
        string email, string first, string last, string phone, string role, string password)
    {
        var user = await users.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, PhoneNumber = phone };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException("Could not seed user " + email + ": " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(user, role);
        }

        if (role == RoleNames.Member && !await db.Customers.AnyAsync(c => c.UserId == user.Id))
        {
            db.Customers.Add(new Customer
            {
                FirstName = first,
                LastName = last,
                Email = email,
                PhoneNumber = phone,
                UserId = user.Id
            });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Starting weekly store hours. Only runs when the table is empty. After that the manager edits them.</summary>
    private static async Task SeedStoreHoursAsync(ApplicationDbContext db)
    {
        if (await db.StoreHours.AnyAsync()) return;

        var sittings = new (SittingType Type, TimeOnly Start, TimeOnly End)[]
        {
            (SittingType.Breakfast, new TimeOnly(7, 0), new TimeOnly(11, 0)),
            (SittingType.Lunch, new TimeOnly(11, 0), new TimeOnly(15, 0)),
            (SittingType.Dinner, new TimeOnly(15, 0), new TimeOnly(23, 0))
        };

        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
            foreach (var (type, start, end) in sittings)
                db.StoreHours.Add(new StoreHour
                {
                    DayOfWeek = day,
                    SittingType = type,
                    IsOpen = true,
                    StartTime = start,
                    EndTime = end,
                    Capacity = 40
                });

        await db.SaveChangesAsync();
    }
    private static async Task SeedSittingsAndReservationsAsync(ApplicationDbContext db)
    {
        var firstRun = !await db.Sittings.AnyAsync();

        // Schedule roughly one quarter ahead (the brief says sittings are scheduled quarterly).
        // Safe to run on every start: it only adds sittings that are missing, so the schedule keeps rolling forward.
        const int daysAhead = 90;
        var today = RestaurantClock.Today;
        var last = today.AddDays(daysAhead - 1);
        var existing = (await db.Sittings
                .Where(s => s.SittingDate >= today && s.SittingDate <= last)
                .Select(s => new { s.SittingDate, s.SittingType })
                .ToListAsync())
            .Select(s => (s.SittingDate, s.SittingType))
            .ToHashSet();

        var hours = await db.StoreHours.AsNoTracking().ToListAsync();

        for (var d = 0; d < daysAhead; d++)
        {
            var date = today.AddDays(d);
            foreach (var hour in hours.Where(h => h.DayOfWeek == date.DayOfWeek && h.IsOpen))
            {
                if (existing.Contains((date, hour.SittingType))) continue;
                db.Sittings.Add(new Sitting
                {
                    SittingType = hour.SittingType,
                    SittingDate = date,
                    StartTime = hour.StartTime,
                    EndTime = hour.EndTime,
                    Capacity = hour.Capacity
                });
            }
        }
        await db.SaveChangesAsync();

        if (!firstRun) return;

        var firstDinner = await db.Sittings.OrderBy(s => s.SittingDate).FirstAsync(s => s.SittingType == SittingType.Dinner);
        var member = await db.Customers.FirstAsync(c => c.UserId != null);
        var walkIn = new Customer { FirstName = "Sam", LastName = "Taylor", Email = "sam.taylor@example.com", PhoneNumber = "0411111111" };
        db.Customers.Add(walkIn);

        db.Reservations.Add(new Reservation { Sitting = firstDinner, Customer = member, StartTime = new TimeOnly(19, 0), DurationMinutes = 90, NumberOfGuests = 3, Source = ReservationSource.Online, SpecialRequests = "Highchair required" });
        db.Reservations.Add(new Reservation { Sitting = firstDinner, Customer = walkIn, StartTime = new TimeOnly(19, 30), DurationMinutes = 90, NumberOfGuests = 6, Source = ReservationSource.Phone, SpecialRequests = "Table by the window" });
        await db.SaveChangesAsync();
    }
}