using BeanScene.Web.Models;

namespace BeanScene.Web.Reports;

public record ReportRow(string Label, int Reservations, int Guests);

public class ReportResult
{
    public string Title { get; set; } = string.Empty;
    public string LabelHeading { get; set; } = string.Empty;
    public List<ReportRow> Rows { get; set; } = new();
}

/// <summary>Interface: every report can generate a result from reservations.</summary>
public interface IReport
{
    string Title { get; }
    ReportResult Generate(IEnumerable<Reservation> reservations);
}

/// <summary>
/// Abstract base class using the template-method pattern. Subclasses override the group key and heading;
/// the shared Generate method does the counting. Calling Generate through IReport/ReportBase is polymorphism.
/// </summary>
public abstract class ReportBase : IReport
{
    public abstract string Title { get; }
    protected abstract string LabelHeading { get; }
    protected abstract string GroupKey(Reservation reservation);
    protected virtual IEnumerable<IGrouping<string, Reservation>> Order(IEnumerable<IGrouping<string, Reservation>> groups)
        => groups.OrderBy(g => g.Key);

    public ReportResult Generate(IEnumerable<Reservation> reservations)
    {
        var groups = Order(reservations.GroupBy(GroupKey));
        return new ReportResult
        {
            Title = Title,
            LabelHeading = LabelHeading,
            Rows = groups.Select(g => new ReportRow(g.Key, g.Count(), g.Sum(r => r.NumberOfGuests))).ToList()
        };
    }
}

public class StatusSummaryReport : ReportBase
{
    public override string Title => "Reservations by status";
    protected override string LabelHeading => "Status";
    protected override string GroupKey(Reservation r) => r.Status.ToString();
}

public class SourceSummaryReport : ReportBase
{
    public override string Title => "Reservations by source";
    protected override string LabelHeading => "Source";
    protected override string GroupKey(Reservation r) => r.Source.ToString();
}

public class SittingSummaryReport : ReportBase
{
    public override string Title => "Reservations by sitting";
    protected override string LabelHeading => "Sitting";
    // The label starts with the ISO date so alphabetical order is also date order.
    protected override string GroupKey(Reservation r) =>
        $"{r.Sitting.SittingDate:yyyy-MM-dd} {r.Sitting.SittingType}";
}
