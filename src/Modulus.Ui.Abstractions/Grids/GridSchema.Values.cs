using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
public static partial class GridSchema
{
    // ---- Values ----

    public static decimal? Number(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    public static DateOnly? Date(string? value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    public static bool Flag(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "on";

    public static IReadOnlyList<string> Many(string? value) =>
        string.IsNullOrEmpty(value) ? [] : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string? FromNumber(decimal? value) => value?.ToString("G29", CultureInfo.InvariantCulture);

    public static string? FromDate(DateOnly? value) => value?.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string? FromDate(DateTime? value) => value?.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string FromFlag(bool value) => value ? "true" : "false";

    public static string? FromMany(IEnumerable<string>? values) => values is null ? null : string.Join(',', values);

    public static GridOption? Option(GridColumn column, string? value) => column.Options.FirstOrDefault(o => o.Value == value);

    /// <summary>The cell as text for display, export or a screen reader, in the current culture.</summary>
    public static string Format(GridColumn column, string? value, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (string.IsNullOrEmpty(value)) return column.Type == GridCellType.Switch ? "No" : "";
        return column.Type switch
        {
            GridCellType.Number => Number(value)?.ToString("#,0.##", culture) ?? value,
            GridCellType.Currency => Number(value) is { } m ? m.ToString(m % 1 == 0 ? "C0" : "C", culture) : value,
            GridCellType.Percent => Number(value) is { } p ? (p / 100m).ToString("P0", culture) : value,
            GridCellType.Progress => Number(value) is { } g ? $"{g:0}%" : value,
            GridCellType.Rating => Number(value) is { } r ? $"{r:0} / {column.Max ?? 5:0}" : value,
            GridCellType.Date => Date(value)?.ToString("d", culture) ?? value,
            GridCellType.Switch => Flag(value) ? "Yes" : "No",
            GridCellType.Select or GridCellType.Tag => Option(column, value)?.Label ?? value,
            GridCellType.MultiSelect => string.Join(", ", Many(value).Select(v => Option(column, v)?.Label ?? v)),
            _ => value,
        };
    }
}

