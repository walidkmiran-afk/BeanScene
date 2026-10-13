namespace BeanScene.Web.Models;

/// <summary>One row of the weekly store hours: whether a sitting runs on a weekday, and its times and capacity.</summary>
public class StoreHour : BaseEntity
{
    public int StoreHourId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public SittingType SittingType { get; set; }
    public bool IsOpen { get; set; } = true;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int Capacity { get; set; } = 40;
}