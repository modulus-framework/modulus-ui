namespace Modulus.Ui.Abstractions.Forms;

/// <summary>A currency for money fields: ISO 4217 code, symbol, name and minor-unit digits (JPY 0, KWD 3).</summary>
public sealed record Currency(string Code, string Symbol, string Name, int Decimals = 2)
{
    /// <summary>The currencies most ERPs ship enabled. Pass your own list to limit or extend it.</summary>
    public static IReadOnlyList<Currency> Common { get; } =
    [
        new("USD", "$", "US dollar"),
        new("EUR", "€", "Euro"),
        new("GBP", "£", "Pound sterling"),
        new("JPY", "¥", "Japanese yen", 0),
        new("CHF", "CHF", "Swiss franc"),
        new("CAD", "CA$", "Canadian dollar"),
        new("AUD", "A$", "Australian dollar"),
        new("NZD", "NZ$", "New Zealand dollar"),
        new("CNY", "¥", "Chinese yuan"),
        new("HKD", "HK$", "Hong Kong dollar"),
        new("SGD", "S$", "Singapore dollar"),
        new("INR", "₹", "Indian rupee"),
        new("BDT", "৳", "Bangladeshi taka"),
        new("PKR", "Rs", "Pakistani rupee"),
        new("KRW", "₩", "South Korean won", 0),
        new("AED", "AED", "UAE dirham"),
        new("SAR", "SAR", "Saudi riyal"),
        new("KWD", "KD", "Kuwaiti dinar", 3),
        new("BHD", "BD", "Bahraini dinar", 3),
        new("TRY", "₺", "Turkish lira"),
        new("SEK", "kr", "Swedish krona"),
        new("NOK", "kr", "Norwegian krone"),
        new("DKK", "kr", "Danish krone"),
        new("PLN", "zł", "Polish złoty"),
        new("ZAR", "R", "South African rand"),
        new("BRL", "R$", "Brazilian real"),
        new("MXN", "MX$", "Mexican peso"),
    ];

    /// <summary>The currency with this code (case-insensitive) in <paramref name="list"/> (default <see cref="Common"/>).</summary>
    public static Currency? Find(string? code, IEnumerable<Currency>? list = null) =>
        string.IsNullOrWhiteSpace(code) ? null
            : (list ?? Common).FirstOrDefault(c => string.Equals(c.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// A unit of measure for quantity fields. <see cref="Decimals"/> is the precision the unit allows (pieces 0,
/// kilograms 3); <see cref="Category"/> groups the unit list (Unit, Weight, Volume, Length, Area, Time).
/// </summary>
public sealed record UnitOfMeasure(string Code, string Label, int Decimals = 0, string? Category = null)
{
    public static IReadOnlyList<UnitOfMeasure> Common { get; } =
    [
        new("pcs", "Units", 0, "Unit"),
        new("dozen", "Dozens", 0, "Unit"),
        new("box", "Boxes", 0, "Unit"),
        new("pallet", "Pallets", 0, "Unit"),
        new("kg", "kg", 3, "Weight"),
        new("g", "g", 0, "Weight"),
        new("t", "Tonnes", 3, "Weight"),
        new("lb", "lb", 2, "Weight"),
        new("l", "Litres", 3, "Volume"),
        new("ml", "ml", 0, "Volume"),
        new("m3", "m³", 3, "Volume"),
        new("m", "m", 2, "Length"),
        new("cm", "cm", 1, "Length"),
        new("yd", "Yards", 2, "Length"),
        new("m2", "m²", 2, "Area"),
        new("h", "Hours", 2, "Time"),
        new("day", "Days", 1, "Time"),
    ];

    /// <summary>The unit with this code (case-insensitive) in <paramref name="list"/> (default <see cref="Common"/>).</summary>
    public static UnitOfMeasure? Find(string? code, IEnumerable<UnitOfMeasure>? list = null) =>
        string.IsNullOrWhiteSpace(code) ? null
            : (list ?? Common).FirstOrDefault(u => string.Equals(u.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
}
