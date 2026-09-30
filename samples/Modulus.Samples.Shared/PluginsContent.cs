using System.Globalization;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Charts;
using Modulus.Ui.Abstractions.Forms;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.Samples.Shared;

/// <summary>
/// Content and page models of the Plugins pages (charts advanced, colour picker, date picker, dropzone, lightbox, tour),
/// shared by the Blazor, MVC and Razor Pages samples. Query-driven parts (chart zoom and drill-down) are built here so
/// the three flavours read the same query string.
/// </summary>
public static partial class PluginsContent
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Charts advanced: zoom ------------------------------------------------------------------------------------

    /// <summary>The zoom levels of the daily visitors chart: (days, label).</summary>
    public static readonly IReadOnlyList<(int Days, string Label)> ZoomLevels = [(90, "90 days"), (30, "30 days"), (14, "14 days"), (7, "7 days")];

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>90 days of visitors and sign-ups, ending today; a weekly rhythm, a slow rise and some noise.</summary>
    private static readonly (DateOnly Day, double Visitors, double Signups)[] Daily = Enumerable.Range(0, 90).Select(i =>
    {
        var day = Today.AddDays(i - 89);
        var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var visitors = 1800 + i * 9 + 260 * Math.Sin(i / 4.3) + (weekend ? -520 : 0) + (i * 37 % 11) * 18;
        return (day, Math.Round(visitors), Math.Round(visitors * (0.031 + (i * 13 % 7) * 0.002)));
    }).ToArray();

    /// <summary>A known zoom level (days); anything else is the widest.</summary>
    public static int Zoom(string? days) => int.TryParse(days, NumberStyles.None, Inv, out var d) && ZoomLevels.Any(z => z.Days == d) ? d : ZoomLevels[0].Days;

    public static ZoomView ZoomChart(string? days)
    {
        var n = Zoom(days);
        var rows = Daily[^n..];
        var labels = rows.Select(r => r.Day.ToString(n > 30 ? "MMM d" : "ddd d", Inv)).ToList();
        return new ZoomView(n, labels,
            [new("Visitors", rows.Select(r => r.Visitors).ToList()), new("Sign-ups", rows.Select(r => r.Signups).ToList())],
            rows.Sum(r => r.Visitors), rows[0].Day, rows[^1].Day);
    }

    /// <summary>
    /// The charts page URL with its zoom (<paramref name="days"/>, 90 is the default and left out) and drill-down
    /// (<paramref name="quarter"/>, 0 is the year) state, scrolled to <paramref name="anchor"/>.
    /// </summary>
    public static string ChartsUrl(int days, int quarter, string anchor)
    {
        var query = new List<string>();
        if (days != ZoomLevels[0].Days) query.Add("days=" + days.ToString(Inv));
        if (quarter > 0) query.Add("quarter=" + quarter.ToString(Inv));
        return "/plugins/charts-advanced" + (query.Count == 0 ? "" : "?" + string.Join("&", query)) + "#" + anchor;
    }

    // Charts advanced: drill-down ------------------------------------------------------------------------------

    private static readonly string[] MonthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    /// <summary>Revenue per month by channel (online, retail, partners), in thousands.</summary>
    private static readonly double[][] MonthlyRevenue =
    [
        [42, 45, 51, 48, 55, 58, 61, 57, 64, 69, 74, 88],
        [31, 29, 34, 36, 33, 38, 41, 39, 37, 42, 46, 57],
        [12, 14, 13, 16, 18, 17, 19, 22, 21, 24, 23, 29],
    ];

    private static readonly string[] Channels = ["Online", "Retail", "Partners"];

    /// <summary>
    /// The drill-down chart: quarters for the year, or the months of quarter <paramref name="quarter"/> (1–4).
    /// <paramref name="href"/> turns a quarter number into the page URL that opens it.
    /// </summary>
    public static DrillView Drill(string? quarter, int days) => Drill(quarter, q => ChartsUrl(days, q, "drill"));

    public static DrillView Drill(string? quarter, Func<int, string> href)
    {
        var q = int.TryParse(quarter, NumberStyles.None, Inv, out var v) && v is >= 1 and <= 4 ? v : 0;
        if (q == 0)
        {
            var labels = Enumerable.Range(1, 4).Select(i => $"Q{i}").ToList();
            var series = Channels.Select((c, s) => new ChartSeries(c, Enumerable.Range(0, 4).Select(i => MonthlyRevenue[s].Skip(i * 3).Take(3).Sum()).ToList())).ToList();
            return new DrillView(0, "Revenue by quarter", labels, series, Enumerable.Range(1, 4).Select(i => (string?)href(i)).ToList());
        }
        var months = Enumerable.Range((q - 1) * 3, 3).ToList();
        return new DrillView(q, $"Q{q}: revenue by month", months.Select(m => MonthNames[m]).ToList(),
            Channels.Select((c, s) => new ChartSeries(c, months.Select(m => MonthlyRevenue[s][m]).ToList())).ToList(), null);
    }

    // Date picker ---------------------------------------------------------------------------------------------

    /// <summary>Range presets ending today.</summary>
    public static IReadOnlyList<DateRangePreset> RangePresets => DatePeriods.Presets(Today);

    /// <summary>The delivery date window: from tomorrow for 60 days, weekends excluded in Blazor (native inputs only take min / max).</summary>
    public static DateOnly DeliveryFrom => Today.AddDays(1);
    public static DateOnly DeliveryTo => Today.AddDays(60);

    public static bool DeliveryDisabled(DateTime date)
    {
        var d = DateOnly.FromDateTime(date);
        return d < DeliveryFrom || d > DeliveryTo || d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    public static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", Inv);

    // Dropzone ------------------------------------------------------------------------------------------------

    public const int MaxUploadMb = 5;
    public const int MaxUploadFiles = 6;
    public const string UploadAccept = ".pdf,.png,.jpg,.jpeg,.gif,.svg,.webp";

    private static readonly string[] UploadExtensions = UploadAccept.Split(',');

    /// <summary>
    /// The server-side check of the dropzone demo endpoint (<c>POST /plugins/dropzone/upload</c>): null when the file
    /// is accepted, else the message the upload list shows. The demo stores nothing.
    /// </summary>
    public static string? CheckUpload(string? fileName, long length)
    {
        if (string.IsNullOrWhiteSpace(fileName) || length == 0) return "The file is empty.";
        if (!UploadExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant())) return "Only PDF and image files are allowed.";
        if (length > MaxUploadMb * 1024L * 1024) return $"Larger than {MaxUploadMb} MB.";
        return null;
    }

    // Lightbox ------------------------------------------------------------------------------------------------

    /// <summary>Square tiles of every gallery photo.</summary>
    public static IReadOnlyList<LightboxItem> Photos => ExtraContent.Lightbox(ExtraContent.Photos);

    /// <summary>Three wide photos for the "no captions" strip.</summary>
    public static IReadOnlyList<LightboxItem> Strip => ExtraContent.Lightbox(ExtraContent.Photos.Where(p => p.Category == "coast"));

    /// <summary>The single cover image.</summary>
    public static IReadOnlyList<LightboxItem> Cover => [GalleryData.LightboxPhotos[0]];

    // Tour ----------------------------------------------------------------------------------------------------

    /// <summary>Steps over the mock dashboard on /plugins/tour, one per placement, then a centred wrap-up.</summary>
    public static readonly TourStep[] TourSteps =
    [
        new("Welcome to your workspace", "A 30-second walkthrough of the main areas. Use the arrow keys or the buttons to move.", Cover: GalleryData.Photos[0].Src),
        new("Key numbers", "Revenue, orders and conversion for the selected period. Each card links to its report.", "#tour-kpis", Placement: "bottom"),
        new("Filters", "Change the period or the sales channel; every card on the page follows.", "#tour-filters", Placement: "left"),
        new("Recent orders", "The newest orders first. Open one to see its lines and shipping status.", "#tour-orders", Placement: "top"),
        new("Quick actions", "Create an order, invite a teammate or export a report from here.", "#tour-actions", Placement: "right"),
        new("You're ready", "Restart the tour any time with the Restart tour button at the top of the page."),
    ];

    public static readonly (string Label, string Value, string Delta, bool Up)[] TourKpis =
    [
        ("Revenue", "$48,210", "+12.4%", true),
        ("Orders", "1,284", "+6.1%", true),
        ("Conversion", "3.42%", "-0.3%", false),
    ];

    public static readonly (string Id, string Customer, string Total, string Status, string Tone)[] TourOrders =
    [
        ("#10482", "Northwind Traders", "$1,240.00", "Paid", "green"),
        ("#10481", "Contoso Ltd", "$318.50", "Pending", "gold"),
        ("#10480", "Fabrikam Inc", "$2,015.75", "Shipped", "blue"),
        ("#10479", "Tailspin Toys", "$96.20", "Refunded", "red"),
    ];

    // Color picker --------------------------------------------------------------------------------------------

    /// <summary>Brand settings of the colour picker form; values the browser posts are checked with <see cref="HsvColor"/>.</summary>
    public static BrandColors Brand(string? primary, string? accent) =>
        new(Valid(primary) ?? "#066fd1", Valid(accent) ?? "#f76707");

    private static string? Valid(string? color) => HsvColor.TryParse(color, out _) ? color : null;
}

public sealed record ZoomView(int Days, IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series, double Total, DateOnly From, DateOnly To)
{
    /// <summary>"Jul 1 to Sep 28".</summary>
    public string Period => From.ToString("MMM d", CultureInfo.InvariantCulture) + " to " + To.ToString("MMM d", CultureInfo.InvariantCulture);
}

/// <summary>One level of the drill-down chart: <see cref="Quarter"/> 0 is the year, 1–4 a quarter; <see cref="Links"/> open the next level.</summary>
public sealed record DrillView(int Quarter, string Title, IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series, IReadOnlyList<string?>? Links);

public sealed record BrandColors(string Primary, string Accent);
