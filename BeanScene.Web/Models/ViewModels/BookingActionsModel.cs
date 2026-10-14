namespace BeanScene.Web.Models.ViewModels;

/// <summary>Everything the shared booking buttons (Edit, Tables, Confirm, Seat, Complete, Cancel, Delete) need for one booking.</summary>
public class BookingActionsModel
{
    public int ReservationId { get; set; }
    public int SittingId { get; set; }
    public ReservationStatus Status { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public TimeOnly Start { get; set; }
    public int Minutes { get; set; }
    public int Guests { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Requests { get; set; }
    public Guid RowVersion { get; set; }

    public ICollection<int> TableIds { get; set; } = new List<int>();
    public ICollection<int> UnavailableTableIds { get; set; } = new List<int>();
    public IEnumerable<Area> Areas { get; set; } = new List<Area>();

    /// <summary>Where to come back to after a button is pressed.</summary>
    public string ReturnUrl { get; set; } = string.Empty;

    /// <summary>How many columns the table that holds the buttons has.</summary>
    public int ColSpan { get; set; } = 6;

    /// <summary>Only managers can delete.</summary>
    public bool CanDelete { get; set; } = true;

    /// <summary>The booking's day has passed and it is still Pending or Confirmed. Worked out from the date, never stored.</summary>
    public bool IsPast { get; set; }
}