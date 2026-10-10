using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeanScene.Web.Models;

public class Sitting : BaseEntity
{
    public int SittingId { get; set; }

    public SittingType SittingType { get; set; }

    public DateOnly SittingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    [Range(1, 500)]
    public int Capacity { get; set; }

    public bool IsClosed { get; set; }
    public ClosedReason ClosedReason { get; set; } = ClosedReason.None;

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    [NotMapped]
    public string DisplayName =>
        $"{SittingType} - {SittingDate:ddd d MMM yyyy}, {StartTime:h:mm tt} to {EndTime:h:mm tt}";
}
