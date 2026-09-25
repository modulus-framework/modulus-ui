using Modulus.Ui.Abstractions.Charts;

namespace Modulus.Samples.Shared;

public sealed record Kpi(string Label, string Value, double Delta, IReadOnlyList<ChartPoint> Trend, string Note, bool InverseDelta = false);

public sealed record StatTile(string Icon, string Tone, string Value, string Caption);

public sealed record TopPage(string Path, int Views, int Visitors, double BounceRate);

public sealed record BrowserShare(string Name, string Icon, double Share);

public sealed record AnalyticsDashboard(
    int RangeDays,
    IReadOnlyList<Kpi> Kpis,
    IReadOnlyList<StatTile> Tiles,
    IReadOnlyList<string> Labels,
    IReadOnlyList<ChartSeries> Revenue,
    IReadOnlyList<ChartSlice> Channels,
    IReadOnlyList<string> HeatRows,
    IReadOnlyList<string> HeatColumns,
    IReadOnlyList<IReadOnlyList<double>> Heat,
    IReadOnlyList<TopPage> TopPages,
    IReadOnlyList<BrowserShare> Browsers,
    IReadOnlyList<RecentOrder> RecentOrders,
    IReadOnlyList<ActivityItem> Activity);

public sealed record FunnelStage(string Stage, int Count);

public sealed record TopCustomer(string Name, string Company, int Deals, decimal Revenue, double Change, int Tint);

public sealed record RepQuota(string Name, decimal Closed, decimal Quota, int Tint);

public sealed record Meeting(string Title, string With, DateTime At, string Kind);

public sealed record CrmDashboard(
    IReadOnlyList<Kpi> Kpis,
    IReadOnlyList<string> Months,
    IReadOnlyList<ChartSeries> WonLost,
    IReadOnlyList<FunnelStage> Funnel,
    IReadOnlyList<TopCustomer> TopCustomers,
    IReadOnlyList<RepQuota> Team,
    IReadOnlyList<CountrySales> Countries,
    IReadOnlyList<Meeting> Meetings,
    IReadOnlyList<ActivityItem> Activity);

public sealed record PopularProduct(string Name, string Sku, decimal Price, int Sold, string Icon, int Tint);

public sealed record EcommerceDashboard(
    decimal MonthBest,
    double MonthTarget,
    IReadOnlyList<StatTile> Tiles,
    IReadOnlyList<string> Months,
    IReadOnlyList<ChartSeries> EarningExpense,
    decimal YearEarning,
    decimal YearExpense,
    IReadOnlyList<ChartSlice> Categories,
    IReadOnlyList<PopularProduct> Popular,
    IReadOnlyList<Transaction> Transactions,
    IReadOnlyList<Order> RecentOrders,
    IReadOnlyDictionary<OrderStatus, int> OrderCounts);

public sealed partial class SampleStore
{
    private static readonly string[] MonthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>Analytics dashboard for the last <paramref name="days"/> days (7, 30 or 90).</summary>
    public AnalyticsDashboard Analytics(int days = 30)
    {
        days = days is 7 or 90 ? days : 30;
        var rng = new Random(days);
        var today = DateTime.Today;
        // Daily points for 7/30 days; weekly buckets for 90 days keep the axis readable.
        var buckets = days == 90 ? 13 : days;
        var step = days == 90 ? 7 : 1;
        var labels = Enumerable.Range(0, buckets).Select(i => today.AddDays((i - buckets + 1) * step).ToString(days == 7 ? "ddd" : "MMM d")).ToList();
        double Wave(int i, double baseValue, double amp, double noise) => Math.Round(baseValue + Math.Sin(i / 3.0) * amp + rng.NextDouble() * noise + i * baseValue * 0.006);
        var current = Enumerable.Range(0, buckets).Select(i => Wave(i, 4_200 * step, 900 * step, 1_400 * step)).ToList();
        var previous = Enumerable.Range(0, buckets).Select(i => Wave(i + 5, 3_700 * step, 800 * step, 1_300 * step)).ToList();
        var sum = current.Sum();
        var prevSum = previous.Sum();

        List<ChartPoint> Spark(double b, double a, int seed)
        {
            var r = new Random(seed + days);
            return Enumerable.Range(0, 12).Select(i => new ChartPoint(labels[Math.Min(labels.Count - 1, i * labels.Count / 12)], Math.Round(b + Math.Sin(i / 2.0) * a + r.NextDouble() * a))).ToList();
        }

        var kpis = new List<Kpi>
        {
            new("Revenue", "$" + sum.ToString("N0"), Math.Round((sum - prevSum) / prevSum * 100, 1), Spark(4_000, 900, 1), $"vs. ${prevSum:N0} previous {days} days"),
            new("Sales", (days * 43).ToString("N0"), 8.2, Spark(40, 12, 2), "Conversion rate 3.4%"),
            new("New customers", (days * 6 + 11).ToString("N0"), -4.3, Spark(6, 3, 3), "Churned this period: 9"),
            new("Active users", "2,986", 12.6, Spark(2_800, 300, 4), "Across web and mobile"),
        };
        var tiles = new List<StatTile>
        {
            new("currency-dollar", "primary", $"{days * 43} Sales", $"{days / 3 + 2} waiting payments"),
            new("shopping-cart", "green", $"{days * 26} Orders", $"{days + 2} shipped"),
            new("users", "orange", $"{days * 6 + 11} New customers", "18 from referrals"),
            new("lifebuoy", "purple", $"{days * 2 + 7} Tickets", $"{days / 4 + 1} awaiting reply"),
        };
        var channels = new List<ChartSlice> { new("Organic search", 4_812), new("Direct", 2_630), new("Referral", 1_472), new("Social", 1_104), new("Email", 687) };

        string[] rows = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
        var cols = Enumerable.Range(0, 12).Select(h => $"{h * 2:00}h").ToList();
        var heat = rows.Select((_, r) => (IReadOnlyList<double>)Enumerable.Range(0, 12).Select(c =>
        {
            var workday = r < 5 ? 1.0 : 0.45;
            var hour = c * 2;
            var curve = hour is >= 8 and <= 18 ? 1.0 : hour is >= 6 and <= 22 ? 0.5 : 0.12;
            return Math.Round((80 + rng.Next(0, 60)) * workday * curve);
        }).ToList()).ToList();

        var pages = new List<TopPage>
        {
            new("/", 12_480, 8_512, 0.38), new("/pricing", 6_320, 5_102, 0.42), new("/apps/invoice/list", 4_870, 2_910, 0.21),
            new("/features/analytics", 3_992, 3_104, 0.47), new("/blog/htmx-and-blazor", 3_214, 2_870, 0.55), new("/docs/getting-started", 2_830, 2_016, 0.29),
        };
        var browsers = new List<BrowserShare> { new("Chrome", "brand-chrome", 0.62), new("Safari", "brand-safari", 0.19), new("Edge", "brand-edge", 0.09), new("Firefox", "brand-firefox", 0.07) };

        List<RecentOrder> orders;
        lock (_gate)
        {
            orders = _orders.OrderByDescending(o => o.PlacedAt).Take(6)
                .Select(o => new RecentOrder(o.Number, o.CustomerName, o.Total, o.Payment == PaymentStatus.Paid ? "Paid" : o.Payment == PaymentStatus.Refunded ? "Refunded" : "Pending", o.PlacedAt))
                .ToList();
        }
        var now = DateTime.Now;
        List<ActivityItem> activity =
        [
            new("Olivia Bennett", "paid invoice", "#5012", now.AddMinutes(-12), "receipt"),
            new("Noah Tanaka", "upgraded", "Fabrikam to Enterprise", now.AddHours(-2), "trending-up"),
            new("Aisha Khan", "resolved ticket", "#4821", now.AddHours(-5), "circle-check"),
            new("System", "flagged low stock for", "Dock Pro", now.AddHours(-9), "alert-triangle"),
            new("Mateo Reyes", "added customer", "Coho Vineyard", now.AddDays(-1), "user-plus"),
        ];

        return new AnalyticsDashboard(days, kpis, tiles, labels,
            [new ChartSeries("This period", current), new ChartSeries("Previous period", previous)],
            channels, rows, cols, heat, pages, browsers, orders, activity);
    }

    public CrmDashboard Crm()
    {
        var rng = new Random(21);
        var won = MonthNames.Select((_, i) => (double)Math.Round(38 + i * 2.6 + Math.Sin(i) * 8 + rng.Next(0, 10))).ToList();
        var lost = MonthNames.Select((_, i) => (double)Math.Round(14 + Math.Cos(i) * 5 + rng.Next(0, 8))).ToList();
        List<ChartPoint> Spark(double b, double a) => Enumerable.Range(0, 12).Select(i => new ChartPoint(MonthNames[i], Math.Round(b + Math.Sin(i / 1.7) * a + rng.NextDouble() * a))).ToList();
        var now = DateTime.Now;
        return new CrmDashboard(
            [
                new("Pipeline value", "$1.84M", 14.2, Spark(140, 30), "148 open deals"),
                new("Deals won", won.Sum().ToString("N0"), 9.1, Spark(45, 10), "This year"),
                new("Win rate", "68%", 3.4, Spark(62, 6), "Won ÷ (won + lost)"),
                new("Avg. deal size", "$12,480", -2.3, Spark(12, 2), "Median $9,900"),
            ],
            MonthNames,
            [new ChartSeries("Won", won), new ChartSeries("Lost", lost)],
            [new("Leads", 2_480), new("Qualified", 1_320), new("Proposal", 640), new("Negotiation", 290), new("Won", 184)],
            [
                new("Olivia Bennett", "Northwind", 12, 184_200, 12.4, 1),
                new("Aisha Khan", "Fabrikam", 9, 142_900, 8.1, 2),
                new("Mateo Reyes", "Litware", 7, 98_600, -3.2, 3),
                new("Yuki Tanaka", "Contoso", 6, 87_300, 5.6, 4),
                new("Chloe Dubois", "Woodgrove Bank", 5, 64_100, 1.9, 5),
                new("Omar Hartmann", "Proseware", 4, 51_800, -1.4, 6),
            ],
            [
                new("Noah Tanaka", 182_000, 200_000, 1), new("Priya Silva", 164_500, 180_000, 2),
                new("Kwame Mensah", 121_300, 160_000, 3), new("Ingrid Lindqvist", 96_800, 140_000, 4),
            ],
            [
                new("US", "United States", 842_310, 25.8), new("BR", "Brazil", 312_640, 6.2), new("IN", "India", 284_120, 12.4),
                new("AU", "Australia", 162_980, -11.9), new("FR", "France", 128_400, 16.2), new("CN", "China", 103_760, -8.6),
            ],
            [
                new("Renewal call", "Olivia Bennett · Northwind", now.Date.AddHours(10), "phone"),
                new("Product demo", "Litware procurement", now.Date.AddHours(13.5), "video"),
                new("Contract review", "Legal, Fabrikam", now.Date.AddDays(1).AddHours(9), "file-text"),
                new("QBR", "Woodgrove Bank", now.Date.AddDays(2).AddHours(15), "presentation"),
            ],
            [
                new("Priya Silva", "closed deal with", "Tailspin ($42,000)", now.AddMinutes(-35), "trophy"),
                new("Noah Tanaka", "moved", "Contoso to Negotiation", now.AddHours(-3), "arrow-right"),
                new("Kwame Mensah", "logged a call with", "Blue Yonder", now.AddHours(-6), "phone"),
                new("Serena Park", "created", "Q4 forecast", now.AddDays(-1), "chart-line"),
            ]);
    }

    public EcommerceDashboard Ecommerce()
    {
        var rng = new Random(33);
        var earning = MonthNames.Select((_, i) => (double)Math.Round(62_000 + i * 3_900 + Math.Sin(i) * 9_000 + rng.Next(0, 7_000))).ToList();
        var expense = earning.Select(e => Math.Round(e * (0.48 + rng.NextDouble() * 0.12))).ToList();
        List<Order> recent;
        lock (_gate) recent = _orders.OrderByDescending(o => o.PlacedAt).Take(6).ToList();
        var now = DateTime.Now;
        return new EcommerceDashboard(
            48_900, 0.78,
            [
                new("chart-bar", "primary", "230k", "Sales"),
                new("users", "azure", "8.549k", "Customers"),
                new("package", "red", "1.423k", "Products"),
                new("currency-dollar", "green", "$9,745", "Revenue"),
            ],
            MonthNames,
            [new ChartSeries("Earning", earning), new ChartSeries("Expense", expense)],
            (decimal)earning.Sum(), (decimal)expense.Sum(),
            [new("Hardware", 38_420), new("Software", 29_870), new("Services", 18_310), new("Accessories", 9_640)],
            [
                new("Edge Gateway X2", "MD-1000", 999.99m, 2_486, "server", 1),
                new("Inventory Cloud", "MD-1084", 299.99m, 2_590, "cloud", 2),
                new("Access Card (100)", "MD-1105", 89.99m, 2_176, "id-badge-2", 3),
                new("Sensor Hub", "MD-1007", 149.99m, 2_128, "cpu", 4),
                new("Fleet Tracker", "MD-1091", 219.99m, 2_110, "truck", 5),
            ],
            [
                new("Wallet", "Starbucks", -74.00m, "wallet", 1),
                new("Bank transfer", "Add money", 480.00m, "building-bank", 2),
                new("PayPal", "Client payment", 268.00m, "brand-paypal", 3),
                new("Mastercard", "Ordered food", -22.00m, "credit-card", 4),
                new("Refund", "Order #6951", 98.00m, "arrow-back-up", 5),
            ],
            recent,
            OrderCounts());
    }
}
