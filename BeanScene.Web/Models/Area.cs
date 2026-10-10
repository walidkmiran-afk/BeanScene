using System.ComponentModel.DataAnnotations;

namespace BeanScene.Web.Models;

public class Area : BaseEntity
{
    public int AreaId { get; set; }

    [Required, StringLength(50)]
    public string AreaName { get; set; } = string.Empty;

    // One Area has many Tables (one-to-many)
    public virtual ICollection<Table> Tables { get; set; } = new List<Table>();
}
