using Modulus.Ui.Abstractions.Charts;

namespace Modulus.Samples.Shared;

/// <summary>A headline figure on the ERP dashboard, with its trend and the change against last year.</summary>
public sealed record ErpKpi(string Label, string Value, double Delta, string Note, ChartPoint[] Trend, bool LowerIsBetter = false);

/// <summary>
/// Fixed figures for the ERP analytics dashboard and the business charts on the charts page: one mid-size
/// manufacturer and distributor, fiscal year 2026. Static so all three samples render the same numbers.
/// </summary>
public static class ErpShowcase
{
    public static readonly string[] Months = UiShowcase.Months;

    private static ChartPoint[] Trend(params double[] values) => Months.Zip(values, (m, v) => new ChartPoint(m, v)).ToArray();

    // ---- Headline KPIs ----

    public static readonly ErpKpi[] Kpis =
    [
        new("Revenue YTD", "$26.25M", 9.8, "vs. $23.91M last year", Trend(1.82, 1.76, 2.04, 2.11, 2.26, 2.19, 2.07, 2.24, 2.38, 2.45, 2.31, 2.62)),
        new("Gross margin", "41.0%", 1.6, "vs. 39.4% last year", Trend(39.2, 39.8, 40.1, 40.6, 40.2, 41.1, 40.8, 41.4, 41.2, 41.6, 41.3, 41.9)),
        new("EBITDA", "$3.07M", 14.2, "11.7% of revenue", Trend(0.18, 0.16, 0.24, 0.25, 0.29, 0.26, 0.22, 0.27, 0.31, 0.32, 0.27, 0.30)),
        new("Days sales outstanding", "46 days", -3.1, "vs. 49 days last year", Trend(52, 51, 50, 50, 49, 48, 48, 47, 47, 46, 46, 46), LowerIsBetter: true),
    ];

    // ---- Finance ----

    public static readonly ChartSeries[] RevenueActual =
        [new("Actual", [1_820_000, 1_760_000, 2_040_000, 2_110_000, 2_260_000, 2_190_000, 2_070_000, 2_240_000, 2_380_000, 2_450_000, 2_310_000, 2_620_000])];

    public static readonly ChartSeries[] RevenueBudget =
        [new("Budget", [1_900_000, 1_900_000, 2_000_000, 2_050_000, 2_150_000, 2_200_000, 2_150_000, 2_250_000, 2_300_000, 2_350_000, 2_400_000, 2_500_000])];

    /// <summary>Full-year P&amp;L, revenue to net profit.</summary>
    public static readonly WaterfallStep[] ProfitBridge =
    [
        new("Revenue", 26_250_000),
        new("COGS", -15_480_000),
        new("Gross profit", 0, IsTotal: true),
        new("Payroll", -4_920_000),
        new("Logistics", -1_340_000),
        new("Marketing", -890_000),
        new("Other opex", -550_000),
        new("EBITDA", 0, IsTotal: true),
        new("D&A", -720_000),
        new("Interest & tax", -700_000),
        new("Net profit", 0, IsTotal: true),
    ];

    /// <summary>Spend minus budget per department, year to date: positive is overspend.</summary>
    public static readonly ChartSlice[] BudgetVariance =
    [
        new("Production", 184_000),
        new("Logistics", 96_000),
        new("R&D", 63_000),
        new("IT", 37_000),
        new("Finance", -9_000),
        new("HR", -21_000),
        new("Sales", -42_000),
        new("Marketing", -118_000),
    ];

    public static readonly BulletItem[] Targets =
    [
        new("Net sales", 26.25, 25.4, [18, 23, 30], "$M, target 25.4"),
        new("Gross margin", 41.0, 42.0, [36, 40, 46], "%, target 42"),
        new("Production output", 18_420, 19_500, [15_000, 18_000, 21_000], "units, plan 19,500"),
        new("Inventory turns", 5.3, 6.0, [4, 5.5, 7], "×, target 6.0"),
        new("Cash collected", 24.1, 25.0, [20, 23.5, 27], "$M, target 25.0"),
    ];

    // ---- Sales ----

    /// <summary>Order-to-cash: how many opportunities survive each step this year.</summary>
    public static readonly ChartSlice[] OrderToCash =
    [
        new("Leads", 4_820),
        new("Qualified", 2_140),
        new("Quoted", 1_160),
        new("Ordered", 612),
        new("Invoiced", 588),
        new("Paid", 541),
    ];

    public static readonly ChartSlice[] TopCustomers =
    [
        new("Northwind Traders", 3_420_000),
        new("Contoso Manufacturing", 2_860_000),
        new("Fabrikam Industries", 2_310_000),
        new("Globex Corporation", 1_980_000),
        new("Initech Systems", 1_640_000),
        new("Tailspin Logistics", 1_410_000),
        new("Wide World Importers", 1_220_000),
        new("Adventure Works", 980_000),
        new("Litware Inc.", 760_000),
        new("Proseware Ltd.", 640_000),
        new("Woodgrove Supplies", 520_000),
        new("Lamna Healthcare", 410_000),
    ];

    public static readonly string[] Regions = ["North", "South", "East", "West", "Export"];

    /// <summary>Revenue by region (rows) and product line (series), year to date.</summary>
    public static readonly ChartSeries[] RegionSales =
    [
        new("Machinery", [3_900_000, 2_750_000, 3_300_000, 2_150_000, 1_700_000]),
        new("Spare parts", [1_400_000, 1_150_000, 1_280_000, 900_000, 620_000]),
        new("Services", [1_050_000, 880_000, 960_000, 700_000, 510_000]),
    ];

    // ---- Receivables ----

    public static readonly string[] AgingCustomers = ["Northwind Traders", "Contoso Manufacturing", "Fabrikam Industries", "Globex Corporation", "Initech Systems", "Tailspin Logistics"];

    /// <summary>Open receivables per customer by days past due; the buckets are ordered, so they take the ordinal ramp.</summary>
    public static readonly ChartSeries[] Aging =
    [
        new("Current", [412_000, 318_000, 254_000, 96_000, 188_000, 72_000]),
        new("1–30 days", [96_000, 84_000, 61_000, 58_000, 22_000, 41_000]),
        new("31–60 days", [38_000, 12_000, 34_000, 47_000, 9_000, 28_000]),
        new("61–90 days", [12_000, 0, 18_000, 31_000, 0, 16_000]),
        new("90+ days", [0, 0, 9_000, 42_000, 0, 11_000]),
    ];

    // ---- Inventory ----

    /// <summary>Stock value by category; more than eight, so the tail folds into "Other".</summary>
    public static readonly ChartSlice[] InventoryValue =
    [
        new("Raw materials", 2_840_000),
        new("Finished goods", 2_120_000),
        new("Work in progress", 1_360_000),
        new("Spare parts", 940_000),
        new("Packaging", 410_000),
        new("Consumables", 280_000),
        new("Tooling", 190_000),
        new("MRO supplies", 120_000),
        new("Returns", 70_000),
        new("Samples", 30_000),
    ];

    /// <summary>Revenue per SKU for ABC classification.</summary>
    public static readonly ChartSlice[] SkuRevenue =
    [
        new("HX-200", 4_860_000), new("HX-350", 3_920_000), new("CN-10", 3_110_000), new("PM-4", 2_540_000),
        new("HX-120", 2_080_000), new("SV-A", 1_690_000), new("CN-22", 1_410_000), new("PM-8", 1_180_000),
        new("SP-KIT", 960_000), new("SV-B", 780_000), new("GR-5", 640_000), new("FL-2", 520_000),
        new("BR-9", 430_000), new("SP-BLT", 360_000), new("CL-1", 290_000), new("GR-7", 230_000),
        new("FL-6", 180_000), new("BR-3", 130_000), new("CL-4", 90_000), new("SP-NUT", 50_000),
    ];

    public static readonly string[] ProductLines = ["Machinery", "Spare parts", "Services"];

    /// <summary>Products: revenue ($, x), gross margin (%, y), units sold (bubble), product line (group).</summary>
    public static readonly ScatterPoint[] ProductPortfolio =
    [
        new("HX-200 press", 4_860_000, 38.5, 420, 0),
        new("HX-350 press", 3_920_000, 42.1, 260, 0),
        new("CN-10 cutter", 3_110_000, 33.8, 610, 0),
        new("PM-4 mixer", 2_540_000, 29.4, 380, 0),
        new("HX-120 press", 2_080_000, 36.2, 290, 0),
        new("CN-22 cutter", 1_410_000, 24.6, 180, 0),
        new("PM-8 mixer", 1_180_000, 31.0, 140, 0),
        new("Blade kit", 960_000, 58.4, 5_200, 1),
        new("Filter set", 520_000, 61.2, 3_900, 1),
        new("Bearing pack", 430_000, 54.8, 2_800, 1),
        new("Belt kit", 360_000, 49.5, 2_100, 1),
        new("Gear set", 640_000, 46.3, 1_600, 1),
        new("Service plan A", 1_690_000, 52.7, 310, 2),
        new("Service plan B", 780_000, 47.9, 190, 2),
        new("Field repair", 290_000, 35.1, 420, 2),
    ];

    // ---- Operations ----

    public const double OnTimeInFull = 92.4;
    public const double OnTimeTarget = 95;
    public const double Utilization = 78.6;
    public const double UtilizationTarget = 85;
    public const double BudgetConsumed = 71.2;
    public const double BudgetElapsed = 75; // share of the fiscal year gone
}
