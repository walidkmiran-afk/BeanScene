using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeanScene.Web.Models;

public class Reservation : BaseEntity
{
    public int ReservationId { get; set; }

    public int SittingId { get; set; }
    public virtual Sitting Sitting { get; set; } = null!;

    public int CustomerId { get; set; }
    public virtual Customer Customer { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    [Range(15, 480)]
    public int DurationMinutes { get; set; } = 90;

    [Range(1, 100)]
    public int NumberOfGuests { get; set; }

    public ReservationSource Source { get; set; }

    /// <summary>
    /// Encapsulation: the status can only be changed through Confirm, Cancel, Seat and Complete,
    /// which enforce the workflow (Pending -> Confirmed -> Seated -> Completed, or Cancelled).
    /// </summary>
    public ReservationStatus Status { get; private set; } = ReservationStatus.Pending;

    [StringLength(500)]
    public string? SpecialRequests { get; set; }

    /// <summary>The sitting name this booking was first made under. Stored once, never overwritten.</summary>
    [StringLength(40)]
    public string? OriginalLabel { get; set; }

    public virtual ICollection<ReservationTable> ReservationTables { get; set; } = new List<ReservationTable>();

    [NotMapped] public int StartMinutes => StartTime.Hour * 60 + StartTime.Minute;
    [NotMapped] public int EndMinutes => StartMinutes + DurationMinutes;
    [NotMapped] public TimeOnly EndTime => StartTime.AddMinutes(DurationMinutes);

    public bool OverlapsInTime(Reservation other) =>
        SittingId == other.SittingId && StartMinutes < other.EndMinutes && other.StartMinutes < EndMinutes;

    public void Confirm()
    {
        Require(Status == ReservationStatus.Pending, "Only a Pending reservation can be confirmed.");
        Status = ReservationStatus.Confirmed;
    }

    public void Cancel()
    {
        Require(Status is ReservationStatus.Pending or ReservationStatus.Confirmed,
            "Only a Pending or Confirmed reservation can be cancelled.");
        Status = ReservationStatus.Cancelled;
    }

    public void Seat()
    {
        Require(Status is ReservationStatus.Pending or ReservationStatus.Confirmed,
            "Only a Pending or Confirmed reservation can be seated.");
        Status = ReservationStatus.Seated;
    }

    public void Complete()
    {
        Require(Status == ReservationStatus.Seated, "Only a Seated reservation can be completed.");
        Status = ReservationStatus.Completed;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
