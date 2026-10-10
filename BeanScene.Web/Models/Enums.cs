namespace BeanScene.Web.Models;

public enum SittingType { Breakfast, Lunch, Dinner, Special }

public enum ClosedReason { None, BookedOut, PrivateEvent }

public enum ReservationStatus { Pending, Confirmed, Cancelled, Seated, Completed }

public enum ReservationSource { Online, Mobile, InPerson, Email, Phone }

/// <summary>Role names used by ASP.NET Core Identity and [Authorize(Roles = ...)].</summary>
public static class RoleNames
{
    public const string Manager = "Manager";
    public const string Staff = "Staff";
    public const string Member = "Member";
    public const string StaffOrManager = Staff + "," + Manager;
}
