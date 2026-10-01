using System.Diagnostics;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.Samples.Shared;

/// <summary>
/// The pivot page (Tables → Pivot) over the 100,000 shipments, the same in all three samples: the filters (status, city,
/// keyword) narrow the shipments, the pivot's own query keys (rows, cols, measures, …) lay out the table.
/// </summary>
public sealed record PivotTablePage(ShipmentFilter Filter, PivotResult Result, double QueryMs)
{
    public const string Path = "/tables/pivot";

    public const string DownloadPath = "/tables/pivot/download";

    /// <summary>The fields the shipments group by and the measures they add up; nothing else reaches the query.</summary>
    public static readonly PivotQuery<Shipment> Pivot = PivotQuery<Shipment>.Create()
        .Dimension("customer", "Customer", s => s.Customer)
        .Dimension("city", "City", s => s.City)
        .Dimension("status", "Status", s => s.Status)
        .Date("date", "Date", s => s.Date)
        .Measure("amount", "Amount", s => s.Amount)
        .Measure("average", "Average amount", s => s.Amount, PivotAggregate.Average)
        .Measure("largest", "Largest amount", s => s.Amount, PivotAggregate.Max)
        .Default(rows: ["city"], columns: ["date:year"], measures: ["count", "amount"]);

    /// <summary>Runs the pivot of the URL's state over the filtered shipments.</summary>
    public static PivotTablePage Load(ShipmentFilter filter, IEnumerable<KeyValuePair<string, string?>> query)
    {
        var state = Pivot.State(query);
        var watch = Stopwatch.StartNew();
        var result = Pivot.Run(Shipments.Where(filter), state);
        return new PivotTablePage(filter, result, watch.Elapsed.TotalMilliseconds);
    }

    /// <summary>The table of the URL's state as an Excel workbook.</summary>
    public static byte[] Download(ShipmentFilter filter, IEnumerable<KeyValuePair<string, string?>> query) =>
        PivotXlsx.Write(Load(filter, query).Result, "Shipments");

    public const string DownloadName = "shipments-pivot.xlsx";

    /// <summary>The download link of the current page: its query, on the download URL.</summary>
    public static string DownloadHref(IEnumerable<KeyValuePair<string, string?>> query) =>
        DownloadPath + "?" + PivotState.Encode(query.Where(q => !string.IsNullOrEmpty(q.Value)).Select(q => new KeyValuePair<string, string>(q.Key, q.Value!)));

    /// <summary>
    /// A cell's drill-down: the large-data list filtered to the cell's shipments, when the large-data page can filter by
    /// every group the cell lies in (status and city); other cells have no link.
    /// </summary>
    public string? CellHref(PivotCell cell)
    {
        var groups = cell.Groups;
        if (groups.Any(g => g.GroupBy is not ("status" or "city"))) return null;
        var status = groups.FirstOrDefault(g => g.GroupBy == "status")?.Label ?? Filter.Status?.ToString();
        var city = groups.FirstOrDefault(g => g.GroupBy == "city")?.Key ?? Filter.City;
        if (city?.Length == 0) return null; // "None" has no filter value
        var query = new List<KeyValuePair<string, string>>();
        if (status is not null) query.Add(new("status", status));
        if (!string.IsNullOrWhiteSpace(city)) query.Add(new("city", city));
        if (!string.IsNullOrWhiteSpace(Filter.Keyword)) query.Add(new("keyword", Filter.Keyword));
        return query.Count == 0 ? "/tables/large" : "/tables/large?" + PivotState.Encode(query);
    }
}
