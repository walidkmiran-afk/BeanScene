using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeanScene.Web.Models;

public class Sitting : BaseEntity
{
    public int SittingId { get; set; }

    public SittingType SittingType { get; set; }

    /// <summary>Optional custom name, e.g. "Mother's Day brunch". Empty means the sitting type is used.</summary>
    [StringLength(40)]
    public string? Label { get; set; }

    public DateOnly SittingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    [Range(1, 500)]
    public int Capacity { get; set; }

    public bool IsClosed { get; set; }

    /// <summary>True when this day's sitting was changed away from the weekly hours.</summary>
    public bool IsCustomised { get; set; }

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    [NotMapped]
    public string Name => string.IsNullOrWhiteSpace(Label) ? SittingType.ToString() : Label;

    [NotMapped]
    public string DisplayName =>
        $"{Name} - {SittingDate:ddd d MMM yyyy}, {StartTime:h:mm tt} to {EndTime:h:mm tt}";
}