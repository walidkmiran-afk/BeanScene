using BeanScene.Web.Data;
using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BeanScene.Tests;

/// <summary>
/// A throw-away SQLite in-memory database shared by every context a test creates.
/// Each test creates its own TestDb, so tests do not affect each other.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public DbContextOptions<ApplicationDbContext> Options { get; }
    public DateOnly TestDate { get; } = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
    public int OpenSittingId { get; private set; }     // dinner 18:00-23:00, capacity 10
    public int ClosedSittingId { get; private set; }
    public int TableM1 { get; private set; }
    public int TableM2 { get; private set; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseLazyLoadingProxies()
            .UseSqlite(_connection)
            .Options;

        using var db = NewContext();
        db.Database.EnsureCreated();

        var area = new Area { AreaName = "Main" };
        area.Tables.Add(new Table { TableName = "M1", Capacity = 4 });
        area.Tables.Add(new Table { TableName = "M2", Capacity = 4 });
        db.Areas.Add(area);

        var open = new Sitting
        {
            SittingType = SittingType.Dinner, SittingDate = TestDate,
            StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 0), Capacity = 10
        };
        var closed = new Sitting
        {
            SittingType = SittingType.Special, SittingDate = TestDate,
            StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(15, 0), Capacity = 10,
            IsClosed = true, ClosedReason = ClosedReason.PrivateEvent
        };
        db.Sittings.AddRange(open, closed);
        db.SaveChanges();

        OpenSittingId = open.SittingId;
        ClosedSittingId = closed.SittingId;
        TableM1 = area.Tables.First(t => t.TableName == "M1").TableId;
        TableM2 = area.Tables.First(t => t.TableName == "M2").TableId;
    }

    public ApplicationDbContext NewContext() => new(Options);

    public ReservationInput Input(int? sittingId = null, int guests = 2, int startHour = 19, int startMinute = 0,
        string last = "Smith", string email = "smith@example.com") => new()
    {
        SittingId = sittingId ?? OpenSittingId,
        StartTime = new TimeOnly(startHour, startMinute),
        DurationMinutes = 90,
        NumberOfGuests = guests,
        Source = ReservationSource.Phone,
        FirstName = "Alex",
        LastName = last,
        Email = email,
        PhoneNumber = "0400111222"
    };

    public void Dispose() => _connection.Dispose();
}
