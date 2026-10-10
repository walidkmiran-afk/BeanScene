using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeanScene.Web.Models;

/// <summary>A guest. A Customer who has registered is a member (UserId is set).</summary>
public class Customer : BaseEntity
{
    public int CustomerId { get; set; }

    [Required, StringLength(60)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, Phone, StringLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    // One-to-one (optional) with the member's login account
    public string? UserId { get; set; }
    public virtual ApplicationUser? User { get; set; }

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";
}
