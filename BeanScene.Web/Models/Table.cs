using System.ComponentModel.DataAnnotations;

namespace BeanScene.Web.Models;

public class Table : BaseEntity
{
    public int TableId { get; set; }

    [Required, StringLength(10)]
    public string TableName { get; set; } = string.Empty;   // e.g. M1, O5, B10

    public int AreaId { get; set; }
    public virtual Area Area { get; set; } = null!;

    [Range(1, 50)]
    public int Capacity { get; set; } = 4;

    // Many-to-many with Reservation through ReservationTable
    public virtual ICollection<ReservationTable> ReservationTables { get; set; } = new List<ReservationTable>();
}
