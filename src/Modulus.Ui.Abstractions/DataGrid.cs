using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;

/// <summary>The editor and display format of a <see cref="GridColumn"/>.</summary>
public enum GridCellType
{
    Text,
    Number,
    Currency,
    Percent,
    Select,
    MultiSelect,
    Date,
    Switch,
    Tag,
    Progress,
    Rating,
}

/// <summary>Where a column is pinned while the table scrolls horizontally (Ant Design's <c>fixed</c>).</summary>
public enum GridFixed
{
    None,
    Left,
    Right,
}

/// <summary>
/// An option of a Select, MultiSelect or Tag column. <paramref name="Color"/> is an Ant Design tag colour;
/// <paramref name="Icon"/> an Ant Design icon shown before the label in the cell ("arrow-up", "fire").
/// </summary>
public sealed record GridOption(string Value, string Label, string? Color = null, string? Icon = null);

/// <summary>
/// A column of an editable, schema-driven grid (Blazor <c>EditableGrid</c>, MVC <c>app-editable-grid</c>).
/// Columns are data, so an app can build them at runtime, store them, or let users add their own.
/// </summary>
public sealed class GridColumn
{
    public string Key { get; set; } = "";

    public string Title { get; set; } = "";

    public GridCellType Type { get; set; }

    /// <summary>Width in pixels; pinned columns need one so the next pinned column knows its offset.</summary>
    public int? Width { get; set; }

    public GridFixed Fixed { get; set; }

    public bool Hidden { get; set; }

    public bool Editable { get; set; } = true;

    public bool Required { get; set; }

    /// <summary>Lower bound for Number, Currency, Percent, Progress and Rating.</summary>
    public decimal? Min { get; set; }

    /// <summary>Upper bound for Number, Currency, Percent, Progress and Rating.</summary>
    public decimal? Max { get; set; }

    public IReadOnlyList<GridOption> Options { get; set; } = [];

    /// <summary>Show an initials avatar before the cell text (a person or company column).</summary>
    public bool Avatar { get; set; }

    /// <summary>Key of a row value shown as a muted second line under the cell text (an email under a name).</summary>
    public string? SubtitleKey { get; set; }

    /// <summary>
    /// Give the column a dropdown in the grid's filter row. Select, Tag, MultiSelect and Switch columns can be filtered.
    /// </summary>
    public bool Filterable { get; set; }

    /// <summary>Numbers and money align right.</summary>
    [JsonIgnore]
    public bool AlignRight => Type is GridCellType.Number or GridCellType.Currency or GridCellType.Percent;

    public GridColumn Clone() => (GridColumn)MemberwiseClone();
}

/// <summary>
/// A grid row. Values are stored as invariant strings keyed by column: numbers as <c>1234.5</c>, dates as
/// <c>yyyy-MM-dd</c>, switches as <c>true</c>/<c>false</c>, multi-selects as comma-separated values.
/// That is exactly what a form posts, so MVC and Blazor share one model and one validator.
/// </summary>
public sealed class GridRecord
{
    public GridRecord() { }

    public GridRecord(string id, IDictionary<string, string?>? values = null)
    {
        Id = id;
        if (values is not null) foreach (var (k, v) in values) Values[k] = v;
    }

    public string Id { get; set; } = "";

    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.Ordinal);

    public string? this[string key]
    {
        get => Values.GetValueOrDefault(key);
        set => Values[key] = value;
    }
}

/// <summary>A validation error on one cell.</summary>
public sealed record GridError(string RowId, string Key, string Message);

/// <summary>Column operations, formatting, parsing and validation for schema-driven grids.</summary>
public static class GridSchema
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>Upper bound on columns (a posted schema with more is rejected).</summary>
    public const int MaxColumns = 50;

    /// <summary>Visible columns in display order: left-pinned first, right-pinned last (Ant Design requires pinned columns at the edges).</summary>
    public static IReadOnlyList<GridColumn> Visible(IEnumerable<GridColumn> columns)
    {
        var shown = columns.Where(c => !c.Hidden).ToList();
        return [.. shown.Where(c => c.Fixed == GridFixed.Left), .. shown.Where(c => c.Fixed == GridFixed.None), .. shown.Where(c => c.Fixed == GridFixed.Right)];
    }

    /// <summary>Sticky offsets (px) for the pinned columns of <paramref name="visible"/>, keyed by column key.</summary>
    public static IReadOnlyDictionary<string, int> StickyOffsets(IReadOnlyList<GridColumn> visible, int leadingWidth = 0)
    {
        var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
        var left = leadingWidth;
        foreach (var c in visible.Where(c => c.Fixed == GridFixed.Left)) { offsets[c.Key] = left; left += c.Width ?? 150; }
        var right = 0;
        foreach (var c in visible.Where(c => c.Fixed == GridFixed.Right).Reverse()) { offsets[c.Key] = right; right += c.Width ?? 150; }
        return offsets;
    }

    /// <summary>Total width of the visible columns, for the table's horizontal scroll (<c>scroll.x</c>).</summary>
    public static int ScrollWidth(IEnumerable<GridColumn> columns, int extra = 0) =>
        Visible(columns).Sum(c => c.Width ?? 150) + extra;

    // ---- Column operations (the column settings menu, and the "Add column" form) ----

    public static void Pin(IList<GridColumn> columns, string key, GridFixed where)
    {
        if (Find(columns, key) is { } c) c.Fixed = where;
    }

    public static void SetHidden(IList<GridColumn> columns, string key, bool hidden)
    {
        // Keep at least one column visible.
        if (hidden && columns.Count(c => !c.Hidden) <= 1) return;
        if (Find(columns, key) is { } c) c.Hidden = hidden;
    }

    /// <summary>Moves a column one place left (<paramref name="delta"/> = -1) or right (+1) among all columns.</summary>
    public static void Move(IList<GridColumn> columns, string key, int delta)
    {
        var i = IndexOf(columns, key);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= columns.Count) return;
        (columns[i], columns[j]) = (columns[j], columns[i]);
    }

    public static void Remove(IList<GridColumn> columns, IEnumerable<GridRecord> rows, string key)
    {
        if (columns.Count <= 1) return;
        var i = IndexOf(columns, key);
        if (i < 0) return;
        columns.RemoveAt(i);
        foreach (var r in rows) r.Values.Remove(key);
    }

    /// <summary>
    /// Adds a column built from user input. The key is derived from the title and made unique; options come from a
    /// comma- or newline-separated list. Returns the new column, or null when the title is blank.
    /// </summary>
    public static GridColumn? Add(IList<GridColumn> columns, string? title, GridCellType type, string? options = null, GridFixed pin = GridFixed.None, bool required = false)
    {
        title = title?.Trim();
        if (string.IsNullOrEmpty(title) || columns.Count >= MaxColumns) return null;
        var column = new GridColumn
        {
            Key = UniqueKey(title, columns.Select(c => c.Key)),
            Title = title.Length > 60 ? title[..60] : title,
            Type = type,
            Fixed = pin,
            Required = required,
            Width = DefaultWidth(type),
            Options = ParseOptions(options),
            Min = type is GridCellType.Progress or GridCellType.Percent ? 0 : type == GridCellType.Rating ? 0 : null,
            Max = type == GridCellType.Progress ? 100 : type == GridCellType.Rating ? 5 : null,
        };
        if (column.Type is GridCellType.Select or GridCellType.MultiSelect or GridCellType.Tag && column.Options.Count == 0)
            column.Options = [new("a", "Option A"), new("b", "Option B")];
        columns.Add(column);
        return column;
    }

    public static int DefaultWidth(GridCellType type) => type switch
    {
        GridCellType.Switch => 100,
        GridCellType.Rating => 160,
        GridCellType.Progress => 180,
        GridCellType.MultiSelect => 220,
        GridCellType.Number or GridCellType.Percent => 120,
        GridCellType.Currency or GridCellType.Date => 140,
        _ => 180,
    };

    private static readonly string[] TagColors = ["blue", "green", "orange", "purple", "cyan", "magenta", "gold", "red"];

    /// <summary>Ant Design preset tag colours offered when adding an option.</summary>
    public static readonly IReadOnlyList<string> OptionColors = [.. TagColors, "volcano", "geekblue", "lime"];

    public const int MaxOptions = 50;
    public const int MaxOptionLabel = 60;

    /// <summary>
    /// Adds an option with <paramref name="label"/> and <paramref name="color"/> (one of <see cref="OptionColors"/>;
    /// anything else takes the next colour in turn). Returns the new list and option, or an error and the list unchanged.
    /// </summary>
    public static (IReadOnlyList<GridOption> Options, GridOption? Added, string? Error) AddOption(IReadOnlyList<GridOption> options, string? label, string? color)
    {
        label = label?.Trim();
        if (string.IsNullOrEmpty(label)) return (options, null, "Enter a name.");
        if (label.Length > MaxOptionLabel) return (options, null, $"Use {MaxOptionLabel} characters or fewer.");
        if (options.Any(o => string.Equals(o.Label, label, StringComparison.OrdinalIgnoreCase))) return (options, null, $"“{label}” is already in the list.");
        if (options.Count >= MaxOptions) return (options, null, $"A list holds up to {MaxOptions} options.");
        var tint = color is not null && OptionColors.Contains(color) ? color : OptionColors[options.Count % OptionColors.Count];
        var added = new GridOption(UniqueKey(label, options.Select(o => o.Value)), label, tint);
        return ([.. options, added], added, null);
    }

    public static IReadOnlyList<GridOption> ParseOptions(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var labels = text.Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToList();
        var used = new List<string>();
        return labels.Select((label, i) =>
        {
            var value = UniqueKey(label, used);
            used.Add(value);
            return new GridOption(value, label, TagColors[i % TagColors.Length]);
        }).ToList();
    }

    /// <summary>camelCase ASCII key from a title, suffixed with a number when taken.</summary>
    public static string UniqueKey(string title, IEnumerable<string> taken)
    {
        var words = new string(title.Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var key = string.Concat(words.Select((w, i) => i == 0 ? w.ToLowerInvariant() : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
        if (key.Length == 0 || char.IsDigit(key[0])) key = "col" + key;
        var set = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = key;
        for (var n = 2; set.Contains(candidate); n++) candidate = key + n;
        return candidate;
    }

    public static GridColumn? Find(IEnumerable<GridColumn> columns, string key) => columns.FirstOrDefault(c => c.Key == key);

    private static int IndexOf(IList<GridColumn> columns, string key)
    {
        for (var i = 0; i < columns.Count; i++) if (columns[i].Key == key) return i;
        return -1;
    }

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

    // ---- Search, filters, avatars and export (mirrored by modulusGrid in modulus-widgets.js) ----

    /// <summary>Whether <paramref name="column"/> can have a filter dropdown (Select, Tag, MultiSelect, Switch).</summary>
    public static bool CanFilter(GridColumn column) =>
        column.Type is GridCellType.Select or GridCellType.Tag or GridCellType.MultiSelect or GridCellType.Switch;

    /// <summary>The columns shown in the filter row: <see cref="GridColumn.Filterable"/> ones that can be filtered.</summary>
    public static IReadOnlyList<GridColumn> FilterColumns(IEnumerable<GridColumn> columns) =>
        [.. columns.Where(c => c.Filterable && CanFilter(c))];

    /// <summary>The choices of a column's filter: its options, or Yes / No for a switch.</summary>
    public static IReadOnlyList<GridOption> FilterOptions(GridColumn column) =>
        column.Type == GridCellType.Switch ? [new("true", "Yes"), new("false", "No")] : column.Options;

    /// <summary>
    /// Whether a row matches a keyword (in the displayed text of any visible column or its subtitle) and every
    /// filter (column key → option value; empty values are ignored).
    /// </summary>
    public static bool Matches(IEnumerable<GridColumn> columns, GridRecord row, string? keyword, IReadOnlyDictionary<string, string?>? filters = null, CultureInfo? culture = null)
    {
        var cols = columns as IReadOnlyList<GridColumn> ?? columns.ToList();
        if (filters is not null)
        {
            foreach (var (key, wanted) in filters)
            {
                if (string.IsNullOrEmpty(wanted) || Find(cols, key) is not { } c) continue;
                var ok = c.Type switch
                {
                    GridCellType.MultiSelect => Many(row[key]).Contains(wanted),
                    GridCellType.Switch => Flag(row[key]) == Flag(wanted),
                    _ => row[key] == wanted,
                };
                if (!ok) return false;
            }
        }
        if (string.IsNullOrWhiteSpace(keyword)) return true;
        var k = keyword.Trim();
        return cols.Where(c => !c.Hidden).Any(c =>
            Format(c, row[c.Key], culture).Contains(k, StringComparison.CurrentCultureIgnoreCase) ||
            (c.SubtitleKey is { } sub && (row[sub] ?? "").Contains(k, StringComparison.CurrentCultureIgnoreCase)));
    }

    /// <summary>Up to two initials for an avatar ("Olivia Bennett" → "OB").</summary>
    public static string Initials(string? text) =>
        string.Concat((text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsLetterOrDigit(w[0])).Take(2).Select(w => char.ToUpperInvariant(w[0])));

    /// <summary>A stable avatar tint, 1 to 8 (the <c>m-av-{n}</c> classes), for a text.</summary>
    public static int Tint(string? text)
    {
        var h = 17u;
        foreach (var ch in text ?? "") h = unchecked(h * 31 + ch);
        return (int)(h % 8) + 1;
    }

    /// <summary>
    /// A cell for CSV: numbers and dates stay invariant (<c>1234.5</c>, <c>2026-03-01</c>), options become their
    /// labels (several joined with "; "), switches Yes / No.
    /// </summary>
    public static string CsvValue(GridColumn column, string? value) => column.Type switch
    {
        GridCellType.Select or GridCellType.Tag => Option(column, value)?.Label ?? value ?? "",
        GridCellType.MultiSelect => string.Join("; ", Many(value).Select(v => Option(column, v)?.Label ?? v)),
        GridCellType.Switch => Flag(value) ? "Yes" : "No",
        _ => value ?? "",
    };

    /// <summary>The rows as CSV with a header of column titles, in the order of <paramref name="columns"/>.</summary>
    public static string Csv(IEnumerable<GridColumn> columns, IEnumerable<GridRecord> rows)
    {
        var cols = columns.ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendJoin(',', cols.Select(c => CsvField(c.Title))).Append("\r\n");
        foreach (var row in rows) sb.AppendJoin(',', cols.Select(c => CsvField(CsvValue(c, row[c.Key])))).Append("\r\n");
        return sb.ToString();
    }

    private static string CsvField(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;

    /// <summary>
    /// Normalises a raw posted value: trims text, rewrites numbers and dates invariantly, drops unknown options.
    /// Values that do not parse are kept so validation can report them.
    /// </summary>
    public static string? Normalize(GridColumn column, string? value)
    {
        value = value?.Trim();
        if (column.Type == GridCellType.Switch) return FromFlag(Flag(value));
        if (string.IsNullOrEmpty(value)) return null;
        return column.Type switch
        {
            GridCellType.Number or GridCellType.Currency or GridCellType.Percent or GridCellType.Progress or GridCellType.Rating
                => Number(value) is { } d ? FromNumber(d) : value,
            GridCellType.MultiSelect => FromMany(Many(value).Where(v => Option(column, v) is not null).Distinct()),
            GridCellType.Text => value.Length > 500 ? value[..500] : value,
            _ => value,
        };
    }

    /// <summary>Validates every cell: required, number format and range, date format, known options.</summary>
    public static IReadOnlyList<GridError> Validate(IEnumerable<GridColumn> columns, IEnumerable<GridRecord> rows)
    {
        var errors = new List<GridError>();
        var cols = columns.ToList();
        foreach (var row in rows)
        {
            foreach (var c in cols)
            {
                if (ValidateCell(c, row[c.Key]) is { } message) errors.Add(new GridError(row.Id, c.Key, message));
            }
        }
        return errors;
    }

    public static string? ValidateCell(GridColumn c, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return c.Required && c.Type != GridCellType.Switch ? $"{c.Title} is required." : null;
        switch (c.Type)
        {
            case GridCellType.Number or GridCellType.Currency or GridCellType.Percent or GridCellType.Progress or GridCellType.Rating:
                if (Number(value) is not { } d) return $"{c.Title} must be a number.";
                if (c.Min is { } min && d < min) return $"{c.Title} must be at least {min.ToString(CultureInfo.CurrentCulture)}.";
                if (c.Max is { } max && d > max) return $"{c.Title} must be at most {max.ToString(CultureInfo.CurrentCulture)}.";
                return null;
            case GridCellType.Date:
                return Date(value) is null ? $"{c.Title} must be a date." : null;
            case GridCellType.Select or GridCellType.Tag:
                return Option(c, value) is null ? $"Choose a valid {c.Title.ToLowerInvariant()}." : null;
            case GridCellType.MultiSelect:
                return Many(value).All(v => Option(c, v) is not null) ? null : $"Choose valid {c.Title.ToLowerInvariant()}.";
            default:
                return null;
        }
    }

    // ---- Round trip (MVC keeps the schema in a hidden field between posts) ----

    public static string Serialize(IEnumerable<GridColumn> columns) => JsonSerializer.Serialize(columns, Json);

    /// <summary>Reads a schema posted back by the browser; returns null when it is missing or malformed.</summary>
    public static List<GridColumn>? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var columns = JsonSerializer.Deserialize<List<GridColumn>>(json, Json);
            if (columns is null || columns.Count == 0 || columns.Count > MaxColumns) return null;
            // The schema came from the client: keep keys unique and well formed.
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in columns)
            {
                if (string.IsNullOrWhiteSpace(c.Key) || c.Key.Length > 40 || !c.Key.All(char.IsAsciiLetterOrDigit) || !keys.Add(c.Key)) return null;
                c.Title = string.IsNullOrWhiteSpace(c.Title) ? c.Key : c.Title.Length > 60 ? c.Title[..60] : c.Title;
                c.Width = c.Width is { } w ? Math.Clamp(w, 60, 600) : null;
                c.Options ??= [];
                if (c.SubtitleKey is { } sub && (sub.Length > 40 || !sub.All(char.IsAsciiLetterOrDigit))) c.SubtitleKey = null;
            }
            return columns;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A new row with an id not used by <paramref name="rows"/> and each column's empty value.</summary>
    public static GridRecord NewRow(IEnumerable<GridColumn> columns, IEnumerable<GridRecord> rows)
    {
        var taken = rows.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var n = taken.Count + 1;
        while (taken.Contains("n" + n)) n++;
        var row = new GridRecord("n" + n);
        foreach (var c in columns) row[c.Key] = c.Type == GridCellType.Switch ? "false" : null;
        return row;
    }
}
