namespace BeanScene.Web;

/// <summary>
/// The one place the app asks "what time is it?". It always answers in the restaurant's time zone (Sydney),
/// so the result does not depend on the server's clock settings or on the guest's browser.
/// </summary>
public static class RestaurantClock
{
    private static readonly TimeZoneInfo Zone = FindZone();

    private static TimeZoneInfo FindZone()
    {
        foreach (var id in new[] { "Australia/Sydney", "AUS Eastern Standard Time" })   // IANA name, then the Windows name
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Local;
    }

    public static DateTime Now => TimeZoneInfo.ConvertTime(DateTime.UtcNow, Zone);

    public static DateOnly Today => DateOnly.FromDateTime(Now);
}