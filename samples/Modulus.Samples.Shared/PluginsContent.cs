using System.Globalization;
using System.Text.Json;
using Modulus.Documents;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Charts;
using Modulus.Ui.Abstractions.Forms;
using Modulus.Ui.Abstractions.Plugins;
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

    // Spreadsheet ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The quarterly budget of the spreadsheet demo: a frozen header row, numbers and =SUM formulas, an orders sheet to
    /// filter, sort and format, and a notes sheet.
    /// </summary>
    public static readonly IReadOnlyList<SpreadsheetSheet> Budget =
    [
        new("Budget",
        [
            ["Item", "Q1", "Q2", "Q3", "Q4", "Year"],
            ["Licences", 1200, 1200, 1350, 1350, "=SUM(B2:E2)"],
            ["Hosting", 480, 480, 520, 560, "=SUM(B3:E3)"],
            ["Marketing", 900, 1250, 1100, 1600, "=SUM(B4:E4)"],
            ["Travel", 600, 400, 750, 500, "=SUM(B5:E5)"],
            ["Total", "=SUM(B2:B5)", "=SUM(C2:C5)", "=SUM(D2:D5)", "=SUM(E2:E5)", "=SUM(F2:F5)"],
        ], FrozenRows: 1, ColumnWidths: [140]),
        new("Orders", OrderRows(), FrozenRows: 1, ColumnWidths: [96, 110, 140, 72, 80, 96]),
        new("Notes",
        [
            ["Everything on the Budget sheet recalculates."],
            ["The Year column sums each row; the Total row sums each column."],
            ["On Orders, try Data > Filter, sort a column, or Format > Conditional formatting on the Total column."],
        ], ColumnWidths: [560]),
    ];

    // Forty orders with a =Units*Price total each and a grand total; the same every run.
    private static IReadOnlyList<IReadOnlyList<object?>> OrderRows()
    {
        string[] regions = ["North", "South", "East", "West"];
        (string Name, double Price)[] products = [("Keyboard", 49), ("Monitor", 219), ("Headset", 89), ("Webcam", 69), ("Dock", 159)];
        var rows = new List<IReadOnlyList<object?>> { new object?[] { "Order", "Region", "Product", "Units", "Price", "Total" } };
        for (var i = 0; i < 40; i++)
        {
            var product = products[(i * 7 + 3) % products.Length];
            var r = i + 2;
            rows.Add(new object?[] { "SO-" + (1040 + i), regions[(i * 3 + 1) % regions.Length], product.Name, 1 + (i * 13 + 5) % 12, product.Price, $"=D{r}*E{r}" });
        }
        rows.Add(new object?[] { "Total", null, null, "=SUM(D2:D41)", null, "=SUM(F2:F41)" });
        return rows;
    }

    private static readonly JsonSerializerOptions SheetsJson = new(JsonSerializerDefaults.Web);

    /// <summary>The sheets the spreadsheet posts as JSON; null when the form value is missing or not valid.</summary>
    public static IReadOnlyList<SpreadsheetSheet>? PostedSheets(string? json)
    {
        try { return JsonSerializer.Deserialize<IReadOnlyList<SpreadsheetSheet>>(json ?? "", SheetsJson); }
        catch (JsonException) { return null; }
    }

    /// <summary>Where the spreadsheet page posts its sheets to download them as an xlsx workbook.</summary>
    public const string SpreadsheetDownloadUrl = "/plugins/spreadsheet/download";

    /// <summary>The file name of the workbook the spreadsheet page downloads.</summary>
    public const string SpreadsheetDownloadName = "budget.xlsx";

    /// <summary>The spreadsheet page demos: the budget (the default), a blank workbook and a read-only grid.</summary>
    public static readonly (string Key, string Label)[] SpreadsheetViews =
        [("budget", "Budget"), ("blank", "Blank sheet"), ("readonly", "Read-only")];

    /// <summary>The demo the spreadsheet page shows; anything else reads as "budget".</summary>
    public static string SpreadsheetView(string? view) => view is "blank" or "readonly" ? view : "budget";

    /// <summary>The spreadsheet page URL of one of <see cref="SpreadsheetViews"/> (the default demo has no query string).</summary>
    public static string SpreadsheetUrl(string view) => view == "budget" ? "/plugins/spreadsheet" : $"/plugins/spreadsheet?view={view}";

    /// <summary>An empty workbook: the spreadsheet's blank-sheet demo.</summary>
    public static IReadOnlyList<SpreadsheetSheet> BlankSheets { get; } = [new("Sheet1", [])];

    /// <summary>The server-side check of an xlsx import: null when the file is accepted, else the message the page shows.</summary>
    public static string? CheckSpreadsheetUpload(string? fileName, long length)
    {
        if (string.IsNullOrWhiteSpace(fileName) || length == 0) return "The file is empty.";
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) return "Only .xlsx files are allowed.";
        if (length > MaxUploadMb * 1024L * 1024) return $"Larger than {MaxUploadMb} MB.";
        return null;
    }

    /// <summary>An xlsx workbook (an upload) as sheets for the grid; null (with the message the page shows) when it will not parse.</summary>
    public static (IReadOnlyList<SpreadsheetSheet>? Sheets, string? Error) ImportSpreadsheet(Stream xlsx)
    {
        try { return (SpreadsheetXlsx.Read(xlsx), null); }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or FormatException or NotSupportedException) { return (null, "That file is not a readable xlsx workbook."); }
    }

    // Document ------------------------------------------------------------------------------------------------

    /// <summary>The document page demos: a purchase contract (the default), an internal memo, a blank page and a read-only one.</summary>
    public static readonly (string Key, string Label)[] DocumentViews =
        [("contract", "Contract"), ("memo", "Memo"), ("blank", "Blank page"), ("readonly", "Read-only")];

    /// <summary>The demo the document page shows; anything else reads as "contract".</summary>
    public static string DocumentView(string? view) => view is "memo" or "blank" or "readonly" ? view : "contract";

    /// <summary>The document page URL of one of <see cref="DocumentViews"/> (the default demo has no query string).</summary>
    public static string DocumentUrl(string view) => view == "contract" ? "/plugins/document" : $"/plugins/document?view={view}";

    /// <summary>Where the document page posts its document to download it as a Word file.</summary>
    public const string DocumentDownloadUrl = "/plugins/document/download";

    /// <summary>The file name of the Word file the document page downloads.</summary>
    public const string DocumentDownloadName = "document.docx";

    /// <summary>The document a demo starts with, as the editor's JSON (the read-only demo shows the contract).</summary>
    public static string DocumentTemplate(string view) => view switch
    {
        "memo" => Memo,
        "blank" => BlankDocument,
        _ => Contract,
    };

    /// <summary>An empty page: the document's blank demo.</summary>
    public static readonly string BlankDocument = UniverDocument.ToJson([], "Untitled");

    /// <summary>A purchase agreement template: title, parties, numbered clauses, a priced items table and signatures.</summary>
    public static readonly string Contract = UniverDocument.ToJson(
    [
        DocBlock.Title("Purchase Agreement"),
        DocBlock.Subtitle("Agreement No. PA-2026-0142"),
        DocBlock.Paragraph("This agreement is made on 3 October 2026 between ", new DocRun("Acme Corporation", Bold: true),
            " (the Buyer) and ", new DocRun("Northwind Components Ltd.", Bold: true), " (the Supplier)."),
        DocBlock.Heading(1, "1. Goods"),
        DocBlock.Paragraph("The Supplier sells and the Buyer buys the following goods at the prices below, excluding VAT:"),
        DocBlock.TableOf(new[]
        {
            new[] { "Item", "Quantity", "Unit price", "Amount" }.Select(h => (IReadOnlyList<DocBlock>)[DocBlock.Paragraph(new DocRun(h, Bold: true))]),
            Row("Mechanical keyboard", "120", "49.00", "5,880.00"),
            Row("27\" monitor", "60", "219.00", "13,140.00"),
            Row("USB-C dock", "60", "159.00", "9,540.00"),
            new[] { "Total", "", "", "28,560.00" }.Select(h => (IReadOnlyList<DocBlock>)[DocBlock.Paragraph(new DocRun(h, Bold: true))]),
        }),
        DocBlock.Heading(1, "2. Delivery"),
        DocBlock.Numbered("Delivery to the Buyer's warehouse within 30 days of this agreement."),
        DocBlock.Numbered("Risk passes to the Buyer when the goods are signed for."),
        DocBlock.Numbered("Late delivery reduces the price by 0.5% per week, up to 5%."),
        DocBlock.Heading(1, "3. Payment"),
        DocBlock.Bullet("30% on signature, 70% within 30 days of delivery."),
        DocBlock.Bullet("Invoices in USD, paid by bank transfer."),
        DocBlock.Heading(1, "4. Warranty"),
        DocBlock.Paragraph("The Supplier warrants the goods free of defects for ", new DocRun("24 months", Italic: true),
            " from delivery and replaces faulty units within 10 working days."),
        DocBlock.Heading(1, "Signatures"),
        DocBlock.Table([["For the Buyer", "For the Supplier"], ["Name:", "Name:"], ["Signature:", "Signature:"], ["Date:", "Date:"]]),
    ], "Purchase Agreement");

    private static IEnumerable<IReadOnlyList<DocBlock>> Row(params string[] cells) =>
        cells.Select(c => (IReadOnlyList<DocBlock>)[DocBlock.Paragraph(c)]);

    /// <summary>An internal memo: a header table, a summary and the actions.</summary>
    public static readonly string Memo = UniverDocument.ToJson(
    [
        DocBlock.Title("Memo"),
        DocBlock.Table([["To", "All department heads"], ["From", "Operations"], ["Date", "3 October 2026"], ["Subject", "Year-end stock count"]]),
        DocBlock.Heading(2, "Summary"),
        DocBlock.Paragraph("The year-end stock count runs on ", new DocRun("Friday 18 December", Bold: true),
            ". Warehouses close to dispatch from noon that day; orders after 10:00 ship the following Monday."),
        DocBlock.Heading(2, "What each team does"),
        DocBlock.Bullet("Warehouse: count teams of two, one counter and one checker."),
        DocBlock.Bullet("Finance: freeze stock postings from 12:00 until the count is approved."),
        DocBlock.Bullet("Sales: tell key customers about the dispatch pause by 11 December."),
        DocBlock.Paragraph(new DocRun("Questions go to operations@acme.example.", Color: "#066fd1")),
    ], "Memo");

    /// <summary>The document JSON the editor posts; null when it is missing or not a document.</summary>
    public static string? PostedDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json).RootElement.TryGetProperty("body", out _) ? json : null; }
        catch (JsonException) { return null; }
    }

    /// <summary>The server-side check of a docx import: null when the file is accepted, else the message the page shows.</summary>
    public static string? CheckDocumentUpload(string? fileName, long length)
    {
        if (string.IsNullOrWhiteSpace(fileName) || length == 0) return "The file is empty.";
        if (!fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)) return "Only .docx files are allowed.";
        if (length > MaxUploadMb * 1024L * 1024) return $"Larger than {MaxUploadMb} MB.";
        return null;
    }

    /// <summary>A docx file (an upload) as the editor's JSON; null (with the message the page shows) when it will not parse.</summary>
    public static (string? Document, string? Error) ImportDocument(Stream docx, string? title = null)
    {
        try { return (DocumentDocx.ReadJson(docx, title), null); }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or FormatException or NotSupportedException)
        { return (null, "That file is not a readable Word document."); }
    }

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
