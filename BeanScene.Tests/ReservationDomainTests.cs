using BeanScene.Web.Models;
using Xunit;

namespace BeanScene.Tests;

public class ReservationDomainTests
{
    [Fact]
    public void NewReservation_StartsPending()
    {
        Assert.Equal(ReservationStatus.Pending, new Reservation().Status);
    }

    [Fact]
    public void NormalWorkflow_PendingToCompleted()
    {
        var r = new Reservation();
        r.Confirm();
        Assert.Equal(ReservationStatus.Confirmed, r.Status);
        r.Seat();
        Assert.Equal(ReservationStatus.Seated, r.Status);
        r.Complete();
        Assert.Equal(ReservationStatus.Completed, r.Status);
    }

    [Fact]
    public void Cancel_AfterSeated_IsRejected()
    {
        var r = new Reservation();
        r.Seat();
        Assert.Throws<InvalidOperationException>(() => r.Cancel());
    }

    [Fact]
    public void Complete_BeforeSeated_IsRejected()
    {
        var r = new Reservation();
        Assert.Throws<InvalidOperationException>(() => r.Complete());
    }

    [Fact]
    public void OverlapsInTime_DetectsOverlapOnlyInSameSitting()
    {
        var a = new Reservation { SittingId = 1, StartTime = new TimeOnly(19, 0), DurationMinutes = 90 };
        var b = new Reservation { SittingId = 1, StartTime = new TimeOnly(20, 0), DurationMinutes = 60 };
        var c = new Reservation { SittingId = 1, StartTime = new TimeOnly(20, 30), DurationMinutes = 60 };
        var d = new Reservation { SittingId = 2, StartTime = new TimeOnly(19, 0), DurationMinutes = 90 };

        Assert.True(a.OverlapsInTime(b));
        Assert.False(a.OverlapsInTime(c));   // a ends exactly when c starts
        Assert.False(a.OverlapsInTime(d));   // different sitting
    }
}
