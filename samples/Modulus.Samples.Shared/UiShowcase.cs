using Modulus.Ui.Abstractions.Charts;

namespace Modulus.Samples.Shared;

/// <summary>A statistics card: icon tile, value, label and change against the previous period.</summary>
public sealed record StatCard(string Label, string Value, string Icon, string Tone, double Delta, string Note);

/// <summary>A goal with progress (cards page).</summary>
public sealed record Goal(string Title, int Done, int Target, string Tone);

/// <summary>An upcoming meeting (cards page).</summary>
public sealed record MeetingCard(string Title, string When, string Icon, string Tone, string[] People);

/// <summary>Static content for the UI pages (cards, charts). Fixed values so all three samples render the same numbers.</summary>
public static class UiShowcase
{
    public static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    // ---- Cards ----

    public static readonly StatCard[] Stats =
    [
        new("Revenue", "$48,250", "currency-dollar", "green", 12.4, "vs. $42,930 last month"),
        new("New customers", "1,284", "users", "blue", 8.1, "vs. 1,188 last month"),
        new("Orders", "3,642", "shopping-cart", "orange", -2.6, "vs. 3,739 last month"),
        new("Refund rate", "1.8%", "ant:rollback", "red", -0.4, "vs. 2.2% last month"),
    ];

    public static readonly (string Label, string Value, string Icon, string Tone, double Share)[] Usage =
    [
        ("Storage", "68.2 GB of 100 GB", "database", "blue", 0.68),
        ("Bandwidth", "412 GB of 1 TB", "world", "green", 0.41),
        ("API calls", "8.4M of 10M", "api", "orange", 0.84),
        ("Seats", "22 of 25", "users", "purple", 0.88),
    ];

    public static readonly Goal[] Goals =
    [
        new("Close Q4 deals", 34, 50, "blue"),
        new("Onboard enterprise accounts", 7, 8, "green"),
        new("Resolve support backlog", 128, 320, "orange"),
        new("Ship design system v2", 18, 24, "purple"),
    ];

    public static readonly MeetingCard[] Meetings =
    [
        new("Quarterly business review", "Today, 14:00 – 15:00", "presentation", "blue", ["Serena Park", "Omar Hartmann", "Yuki Tanaka"]),
        new("Design critique", "Tomorrow, 10:30 – 11:15", "palette", "purple", ["Noah Tanaka", "Lena Fischer"]),
        new("Hiring sync: backend", "Thu, 09:00 – 09:30", "user-plus", "green", ["Serena Park", "Marco Rossi"]),
        new("Customer call: Globex", "Fri, 16:00 – 16:45", "phone", "orange", ["Aisha Khan", "Omar Hartmann", "Lena Fischer", "Noah Tanaka"]),
    ];

    public static readonly (string Key, string Label)[] CardTabs = [("overview", "Overview"), ("activity", "Activity"), ("settings", "Settings")];

    // ---- Charts ----

    public static readonly ChartSeries[] Traffic =
    [
        new("Organic", [3200, 3450, 3900, 4100, 4380, 4820, 5100, 5020, 5460, 5890, 6210, 6640]),
        new("Paid", [1800, 2100, 2050, 2400, 2600, 2550, 2980, 3200, 3050, 3380, 3500, 3720]),
        new("Referral", [900, 950, 1100, 1040, 1180, 1250, 1320, 1290, 1400, 1480, 1510, 1620]),
    ];

    public static readonly ChartSeries[] Sessions =
    [
        new("This year", [42, 48, 45, 56, 61, 58, 67, 72, 70, 78, 84, 91]),
        new("Last year", [35, 38, 40, 42, 47, 49, 52, 55, 57, 60, 63, 66]),
    ];

    public static readonly ChartSeries[] Finance =
    [
        new("Earning", [18_400, 21_200, 19_800, 24_600, 26_100, 25_300, 28_900, 30_200, 27_600, 31_800, 33_400, 36_100]),
        new("Expense", [12_100, 13_400, 12_900, 15_200, 16_800, 15_900, 17_300, 18_600, 17_100, 19_400, 20_200, 21_800]),
    ];

    public static readonly string[] Quarters = ["Q1", "Q2", "Q3", "Q4"];

    public static readonly ChartSeries[] Channels =
    [
        new("Direct", [4200, 4800, 5300, 6100]),
        new("Partners", [2600, 2900, 3400, 3800]),
        new("Marketplace", [1500, 1900, 2300, 2900]),
    ];

    public static readonly ChartPoint[] Signups =
    [
        new("Sep 12", 124), new("Sep 13", 98), new("Sep 14", 86), new("Sep 15", 142), new("Sep 16", 168), new("Sep 17", 155), new("Sep 18", 171),
        new("Sep 19", 132), new("Sep 20", 104), new("Sep 21", 96), new("Sep 22", 181), new("Sep 23", 194), new("Sep 24", 176), new("Sep 25", 203),
    ];

    public static readonly ChartSlice[] Devices = [new("Desktop", 58_200), new("Mobile", 34_600), new("Tablet", 7_900), new("Other", 1_300)];

    public static readonly ChartPoint[] ResponseTime = Months.Zip(new double[] { 182, 176, 190, 168, 159, 171, 148, 142, 151, 139, 134, 128 }, (m, v) => new ChartPoint(m, v)).ToArray();

    public static readonly ChartPoint[] Tickets = Months.Zip(new double[] { 64, 58, 71, 49, 55, 62, 44, 51, 47, 39, 42, 36 }, (m, v) => new ChartPoint(m, v)).ToArray();

    public static readonly string[] Weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    public static readonly string[] HourBlocks = ["00", "02", "04", "06", "08", "10", "12", "14", "16", "18", "20", "22"];

    /// <summary>Orders per weekday and two-hour block: busy weekday mornings and evenings, quiet nights.</summary>
    public static readonly IReadOnlyList<IReadOnlyList<double>> Activity = Weekdays.Select((_, d) =>
        (IReadOnlyList<double>)HourBlocks.Select((_, h) =>
        {
            var baseline = h switch { < 3 => 6, 3 => 14, 4 or 5 => 48, 6 => 56, 7 or 8 => 44, 9 or 10 => 62, _ => 30 };
            var weekend = d >= 5 ? 0.55 : 1.0;
            return Math.Round(baseline * weekend + (d * 7 + h * 3) % 11);
        }).ToList()).ToList();
}
