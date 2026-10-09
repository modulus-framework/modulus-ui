using Modulus.Ui.Abstractions.Dashboard;

namespace Modulus.Samples.Shared;

/// <summary>A number tile's content: its label, its value, and a line under it.</summary>
public sealed record DashboardKpi(string Id, string Label, string Value, string Note);

/// <summary>
/// The dashboard layout sample: four number tiles and an activity feed, each tile moved, widened, narrowed, heightened or
/// shortened by its buttons, and the layout kept per signed-in user. The numbers are fixed sample figures.
/// </summary>
public static class DashboardPage
{
    /// <summary>The dashboard's id: its layout is saved under it.</summary>
    public const string Id = "home";

    /// <summary>The page's path (the MVC and Razor Pages forms come back to it).</summary>
    public const string Path = "/dashboards/tiles";

    /// <summary>The tiles and their default sizes (columns of 12, rows).</summary>
    public static readonly IReadOnlyList<DashboardTileSpec> Tiles =
    [
        new("revenue", "Revenue", 3, 2),
        new("orders", "Orders", 3, 2),
        new("customers", "Customers", 3, 2),
        new("margin", "Margin", 3, 2),
        new("activity", "Recent activity", 12, 3),
    ];

    /// <summary>The number tiles' content, by tile id.</summary>
    public static readonly IReadOnlyList<DashboardKpi> Kpis =
    [
        new("revenue", "Revenue this month", "$128,400", "Up 12% on last month"),
        new("orders", "Orders", "342", "28 waiting to ship"),
        new("customers", "Customers", "1,208", "41 signed up this week"),
        new("margin", "Gross margin", "38.5%", "Steady for three months"),
    ];

    /// <summary>The activity feed's lines.</summary>
    public static readonly IReadOnlyList<string> Activity =
    [
        "Northwind placed order #4821",
        "Lee Ann Chen joined the Enterprise plan",
        "Stock for SKU 1043 is below its reorder level",
        "Invoice INV-2026-0187 was paid",
    ];

    /// <summary>The number tile for an id, or null for the activity tile.</summary>
    public static DashboardKpi? Kpi(string id) => Kpis.FirstOrDefault(k => k.Id == id);
}
