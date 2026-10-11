using System.ComponentModel.DataAnnotations;

namespace BeanScene.Web.Models.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required, StringLength(60), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(60), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^(?:\+61|0)[\s-]?[2-478](?:[\s-]?\d){8}$", ErrorMessage = "Enter an Australian phone number, e.g. 0412 345 678"), Display(Name = "Phone number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Create / edit form for a Sitting.</summary>
public class SittingFormViewModel : IValidatableObject
{
    public int SittingId { get; set; }

    [Display(Name = "Sitting type")]
    public SittingType SittingType { get; set; } = SittingType.Dinner;

    [Required, DataType(DataType.Date), Display(Name = "Date")]
    public DateOnly SittingDate { get; set; } = RestaurantClock.Today;

    [Required, Display(Name = "Start time")]
    public TimeOnly StartTime { get; set; } = new TimeOnly(18, 0);

    [Required, Display(Name = "End time")]
    public TimeOnly EndTime { get; set; } = new TimeOnly(23, 0);

    [Range(1, 500)]
    public int Capacity { get; set; } = 40;

    [Display(Name = "Closed to reservations")]
    public bool IsClosed { get; set; }

    [Display(Name = "Closed reason")]
    public ClosedReason ClosedReason { get; set; } = ClosedReason.None;

    public Guid RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndTime <= StartTime)
            yield return new ValidationResult("The end time must be after the start time.", new[] { nameof(EndTime) });
        if (IsClosed && ClosedReason == ClosedReason.None)
            yield return new ValidationResult("Choose why the sitting is closed.", new[] { nameof(ClosedReason) });
    }
}

/// <summary>Input for creating or updating a reservation (used by MVC forms and the REST API).</summary>
public class ReservationInput
{
    public int ReservationId { get; set; }

    [Required, Display(Name = "Sitting")]
    public int SittingId { get; set; }

    [Required, Display(Name = "Start time")]
    public TimeOnly StartTime { get; set; } = new TimeOnly(19, 0);

    [Range(30, 180), Display(Name = "Duration (minutes)")]
    public int DurationMinutes { get; set; } = 45;

    [Range(1, 100), Display(Name = "Number of guests")]
    public int NumberOfGuests { get; set; } = 2;

    public ReservationSource Source { get; set; } = ReservationSource.Phone;

    [Required, StringLength(60), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(60), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^(?:\+61|0)[\s-]?[2-478](?:[\s-]?\d){8}$", ErrorMessage = "Enter an Australian phone number, e.g. 0412 345 678"), Display(Name = "Phone")]
    public string PhoneNumber { get; set; } = string.Empty;

    [StringLength(500), Display(Name = "Notes / special requests")]
    public string? SpecialRequests { get; set; }

    /// <summary>The concurrency token the form was loaded with (used on edit).</summary>
    public Guid RowVersion { get; set; }
}

public class ReservationSearchCriteria
{
    public string? Name { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? SittingId { get; set; }
    public ReservationStatus? Status { get; set; }
    public ReservationSource? Source { get; set; }
    public string? TableName { get; set; }
}

public class SittingListItem
{
    public Sitting Sitting { get; set; } = null!;
    public int GuestsBooked { get; set; }
}

public class AllocationViewModel
{
    public Reservation Reservation { get; set; } = null!;
    public List<Area> Areas { get; set; } = new();
    public HashSet<int> SelectedTableIds { get; set; } = new();
    public HashSet<int> UnavailableTableIds { get; set; } = new();
}

public class ReportsViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int TotalReservations { get; set; }
    public int TotalGuests { get; set; }
    public int CancelledReservations { get; set; }
    public double UtilisationPercent { get; set; }
    public List<Reports.ReportResult> Reports { get; set; } = new();
}

public class OrmDemoStep
{
    public string Title { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public List<string> Lines { get; set; } = new();
    public bool Succeeded { get; set; } = true;
}

public static class UiHelpers
{
    public static string StatusBadge(ReservationStatus status) => status switch
    {
        ReservationStatus.Pending => "text-bg-warning",
        ReservationStatus.Confirmed => "text-bg-primary",
        ReservationStatus.Seated => "text-bg-info",
        ReservationStatus.Completed => "text-bg-success",
        ReservationStatus.Cancelled => "text-bg-secondary",
        _ => "text-bg-light"
    };
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}


/// <summary>Member account page: the details a member can change.</summary>
public class AccountDetailsViewModel
{
    [Required, StringLength(60), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(60), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required, RegularExpression(@"^(?:\+61|0)[\s-]?[2-478](?:[\s-]?\d){8}$", ErrorMessage = "Enter an Australian phone number, e.g. 0412 345 678"), Display(Name = "Phone number")]
    public string PhoneNumber { get; set; } = string.Empty;
}

/// <summary>Changing the login email needs the current password.</summary>
public class ChangeEmailViewModel
{
    [Required, EmailAddress, StringLength(256), Display(Name = "New email address")]
    public string NewEmail { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;
}


public class DeleteAccountViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;
}


/// <summary>One row on the manager's Accounts page.</summary>
public class AccountRowViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
}

public class CreateStaffViewModel
{
    [Required, StringLength(60), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(60), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256), Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [RegularExpression(@"^(?:\+61|0)[\s-]?[2-478](?:[\s-]?\d){8}$", ErrorMessage = "Enter an Australian phone number, e.g. 0412 345 678"), Display(Name = "Phone number (optional)")]
    public string? PhoneNumber { get; set; }

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}

public class EditAccountViewModel
{
    public string UserId { get; set; } = string.Empty;

    [Required, StringLength(60), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(60), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256), Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [RegularExpression(@"^(?:\+61|0)[\s-]?[2-478](?:[\s-]?\d){8}$", ErrorMessage = "Enter an Australian phone number, e.g. 0412 345 678"), Display(Name = "Phone number (optional)")]
    public string? PhoneNumber { get; set; }
}

public class ResetPasswordViewModel
{
    public string UserId { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8), Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}