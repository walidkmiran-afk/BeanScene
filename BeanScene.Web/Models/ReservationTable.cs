namespace BeanScene.Web.Models;

/// <summary>Join entity that creates the many-to-many relationship between Reservation and Table.</summary>
public class ReservationTable : BaseEntity
{
    public int ReservationId { get; set; }
    public virtual Reservation Reservation { get; set; } = null!;

    public int TableId { get; set; }
    public virtual Table Table { get; set; } = null!;
}
