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

/// <summary>How far a grid row can be changed (<see cref="GridRecord.State"/>).</summary>
public enum GridRowState
{
    /// <summary>Its editable cells change; it can be moved and deleted.</summary>
    Editable,

    /// <summary>Its cells don't change (an approved or computed line); it can still be moved and deleted.</summary>
    ReadOnly,

    /// <summary>Nothing changes: no edits, no delete, no move (a posted document, a row someone else holds).</summary>
    Locked,
}

/// <summary>
/// A button in the editable grid's batch bar that acts on the selected rows: Approve, Reject, Post, Print … The grid
/// counts the rows it applies to ("Approve 3"), skips locked rows (except <see cref="Print"/>), asks first when
/// <see cref="Confirm"/> is set, and hands the app those rows; the app does the work (Blazor: <c>OnBatch</c>; MVC: the
/// form posts <c>{name}.op=batch:{key}</c>, read with <c>EditableGridForm.Batch</c>).
/// </summary>
public sealed class GridBatchAction
{
    /// <summary>Its id in a post (<c>batch:{key}</c>); letters, digits, <c>-</c> and <c>_</c>.</summary>
    public required string Key { get; init; }

    public required string Label { get; init; }

    /// <summary>An Ant Design icon name (<c>check</c>, <c>close</c>, <c>printer</c>).</summary>
    public string? Icon { get; init; }

    /// <summary>The primary button of the bar (one, at most).</summary>
    public bool Primary { get; init; }

    /// <summary>A red button (Reject, Void).</summary>
    public bool Danger { get; init; }

    /// <summary>Ask "Approve 3 rows?" first.</summary>
    public bool Confirm { get; init; }

    /// <summary>The rows it applies to (a pending document), or null for any row. Locked rows never apply.</summary>
    [JsonIgnore]
    public Func<GridRecord, bool>? When { get; init; }

    /// <summary>Prints the selected rows (the visible columns, every selected row, locked ones too) instead of calling the app.</summary>
    public bool Print { get; init; }

    /// <summary>A Print button: the selected rows as a table, in the browser's print dialog.</summary>
    public static GridBatchAction Printing(string label = "Print") => new() { Key = "print", Label = label, Icon = "printer", Print = true };
}

/// <summary>A batch button was clicked (and confirmed): the action and the selected rows it acts on (<see cref="GridSchema.BatchRows"/>).</summary>
public sealed record GridBatchRequest(GridBatchAction Action, IReadOnlyList<GridRecord> Rows);

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

    /// <summary>
    /// Read-only or locked rows can't be edited in the grid. Set by the server and never read from a post: MVC apps
    /// call <see cref="GridSchema.Protect"/> to keep a posted grid to the stored states.
    /// </summary>
    public GridRowState State { get; set; }

    /// <summary>Why the row is read-only or locked ("Invoiced on 3 Oct"): its tooltip, and read out with the lock.</summary>
    public string? StateReason { get; set; }

    /// <summary>The row's cells can change.</summary>
    [JsonIgnore]
    public bool Editable => State == GridRowState.Editable;

    public string? this[string key]
    {
        get => Values.GetValueOrDefault(key);
        set => Values[key] = value;
    }
}

/// <summary>A validation error on one cell, or on a whole row when <see cref="Key"/> is empty (<see cref="ForRow"/>).</summary>
public sealed record GridError(string RowId, string Key, string Message)
{
    /// <summary>An error on a row rather than a cell: a rule over several cells, or the server refusing the row.</summary>
    public static GridError ForRow(string rowId, string message) => new(rowId, "", message);

    /// <summary>The error is on the whole row.</summary>
    public bool IsRow => Key.Length == 0;
}

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

    /// <summary>
    /// The columns <paramref name="allow"/> lets the user see (<see cref="State.FieldAccess.For"/>), for rendering and for
    /// reading posted rows: apply a post against this list, so a column the user can't see can't be written either.
    /// </summary>
    public static List<GridColumn> Allowed(IEnumerable<GridColumn> columns, Func<string, bool> allow) =>
        columns.Where(c => allow(c.Key)).ToList();

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

    /// <summary>
    /// Puts the items named in <paramref name="order"/> (a page of rows as the user dragged them) back into the slots
    /// those items held in <paramref name="all"/>, so items on other pages or filtered out keep their places. Ids that
    /// are not in the list are skipped.
    /// </summary>
    public static void Reorder<T>(IList<T> all, IEnumerable<string> order, Func<T, string> idOf)
    {
        var byId = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in all) byId.TryAdd(idOf(item), item);
        var wanted = order.Where(byId.ContainsKey).Distinct(StringComparer.Ordinal).ToList();
        var set = wanted.ToHashSet(StringComparer.Ordinal);
        var slots = Enumerable.Range(0, all.Count).Where(i => set.Contains(idOf(all[i]))).ToList();
        for (var k = 0; k < slots.Count && k < wanted.Count; k++) all[slots[k]] = byId[wanted[k]];
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

    /// <summary>Ant Design preset tag colours an option may have (stored options keep theirs).</summary>
    public static readonly IReadOnlyList<string> OptionColors = [.. TagColors, "volcano", "geekblue", "lime"];

    /// <summary>
    /// The colours offered when creating an option: eight clearly different presets, each with a name
    /// (<see cref="ColorName"/>) shown next to the swatches. New options take them in turn.
    /// </summary>
    public static readonly IReadOnlyList<string> PickerColors = TagColors;

    /// <summary>The name of a tag colour for people ("magenta" → "Pink").</summary>
    public static string ColorName(string? color) => color switch
    {
        null or "" or "default" => "Grey",
        "magenta" => "Pink",
        "geekblue" => "Indigo",
        "volcano" => "Coral",
        _ => char.ToUpperInvariant(color[0]) + color[1..],
    };

    /// <summary>The colour a new option takes when none is picked: the next picker colour in turn.</summary>
    public static string NextColor(int optionCount) => PickerColors[Math.Abs(optionCount) % PickerColors.Count];

    /// <summary>A lower-case singular of a plural title for labels ("Styles" → "style", "Deliveries" → "delivery").</summary>
    public static string Singular(string title)
    {
        var t = title.Trim().ToLowerInvariant();
        if (t.EndsWith("ies", StringComparison.Ordinal) && t.Length > 3) return t[..^3] + "y";
        return t.EndsWith('s') && !t.EndsWith("ss", StringComparison.Ordinal) && t.Length > 1 ? t[..^1] : t;
    }

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
        var tint = color is not null && OptionColors.Contains(color) ? color : NextColor(options.Count);
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
    /// filter (column key → option value; empty values are ignored) and every column search (column key → query,
    /// see <see cref="ColumnSearch"/>).
    /// </summary>
    public static bool Matches(IEnumerable<GridColumn> columns, GridRecord row, string? keyword, IReadOnlyDictionary<string, string?>? filters = null, CultureInfo? culture = null,
        IReadOnlyDictionary<string, string?>? searches = null)
    {
        var cols = columns as IReadOnlyList<GridColumn> ?? columns.ToList();
        if (searches is not null)
        {
            foreach (var (key, query) in searches)
            {
                // A hidden column's search is ignored: its header, and so the search, is out of sight.
                if (string.IsNullOrWhiteSpace(query) || Find(cols, key) is not { Hidden: false } c) continue;
                if (!ColumnMatches(c, row, query, culture)) return false;
            }
        }
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

    /// <summary>
    /// The choices of a column's box in a table's filter row, for Select, Tag, Switch and Multi-select columns (a
    /// select instead of a text box). Each is a column search on the displayed text: exact for a single value
    /// (<c>=Approved</c>), "contains" for a multi-select. Null for other columns, or when there are no options.
    /// </summary>
    public static IReadOnlyList<ColumnChoice>? SearchChoices(GridColumn column)
    {
        IReadOnlyList<ColumnChoice> choices = column.Type switch
        {
            GridCellType.Select or GridCellType.Tag or GridCellType.Switch => [.. FilterOptions(column).Select(o => new ColumnChoice(o.Label, "=" + o.Label))],
            GridCellType.MultiSelect => [.. column.Options.Select(o => new ColumnChoice(o.Label, o.Label))],
            _ => [],
        };
        return choices.Count == 0 ? null : choices;
    }

    /// <summary>
    /// The values of a column's checkbox list (the Excel-style filter menu), for Select, Tag and Switch columns: the
    /// option labels in their order, then any other text the cells have, then blanks. With <paramref name="rows"/>
    /// each value has its count (a table searched on the client); without, no counts (searched on the server). Null
    /// for other columns, or when there are no options.
    /// </summary>
    public static IReadOnlyList<ColumnValue>? SearchValues(GridColumn column, IEnumerable<GridRecord>? rows = null, CultureInfo? culture = null)
    {
        if (column.Type is not (GridCellType.Select or GridCellType.Tag or GridCellType.Switch)) return null;
        var labels = FilterOptions(column).Select(o => o.Label.Trim())
            .Where(l => l.Length > 0 && l.IndexOf(ColumnSearch.ValueSeparator) < 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase).Take(ColumnSearch.MaxValues).ToList();
        if (labels.Count == 0) return null;
        if (rows is null) return [.. labels.Select(l => new ColumnValue(l))];
        var cells = ColumnSearch.ValuesOf(rows.Select(r => Format(column, r[column.Key], culture)));
        var counts = cells.ToDictionary(v => v.Text, v => v.Count ?? 0, StringComparer.CurrentCultureIgnoreCase);
        var known = new HashSet<string>(labels, StringComparer.CurrentCultureIgnoreCase);
        return [.. labels.Select(l => new ColumnValue(l, counts.GetValueOrDefault(l))),
            .. cells.Where(v => !known.Contains(v.Text))];
    }

    /// <summary>How a column search compares the column: numbers and dates by value, everything else as text.</summary>
    public static ColumnKind Kind(GridColumn column) => column.Type switch
    {
        GridCellType.Number or GridCellType.Currency or GridCellType.Percent or GridCellType.Progress or GridCellType.Rating => ColumnKind.Number,
        GridCellType.Date => ColumnKind.Date,
        _ => ColumnKind.Text,
    };

    /// <summary>Whether a row's cell matches a column search (<see cref="ColumnSearch"/>); the subtitle counts as text.</summary>
    public static bool ColumnMatches(GridColumn column, GridRecord row, string? query, CultureInfo? culture = null)
    {
        var value = row[column.Key];
        var text = Format(column, value, culture);
        var kind = Kind(column);
        if (column.SubtitleKey is not { } sub || string.IsNullOrEmpty(row[sub]) || ColumnSearch.IsValueList(query))
            return ColumnSearch.Matches(query, text, kind, Number(value), Date(value));
        // With a subtitle: "contains" and "=exact" match the cell or the cell with its subtitle (so a filter-row
        // choice, =Label, still matches), and "!exclude" needs neither to contain it.
        var withSub = text + " " + row[sub];
        return (query ?? "").TrimStart().StartsWith('!')
            ? ColumnSearch.Matches(query, withSub, kind, Number(value), Date(value))
            : ColumnSearch.Matches(query, text, kind, Number(value), Date(value)) || ColumnSearch.Matches(query, withSub, kind, Number(value), Date(value));
    }

    /// <summary>Rows sorted by a column: numbers and dates by value, the rest by displayed text; empty cells last.</summary>
    public static IEnumerable<GridRecord> Sort(IEnumerable<GridRecord> rows, GridColumn column, bool descending) =>
        rows.OrderBy(r => SortKey(column, r[column.Key]), descending ? SortComparer.Desc : SortComparer.Asc);

    private static IComparable? SortKey(GridColumn c, string? value) => c.Type switch
    {
        GridCellType.Number or GridCellType.Currency or GridCellType.Percent or GridCellType.Progress or GridCellType.Rating => Number(value),
        GridCellType.Date => Date(value),
        GridCellType.Switch => Flag(value) ? 1 : 0,
        _ => string.IsNullOrEmpty(value) ? null : Format(c, value, CultureInfo.InvariantCulture),
    };

    private sealed class SortComparer : IComparer<IComparable?>
    {
        public static readonly SortComparer Asc = new(false), Desc = new(true);

        private readonly bool _desc;

        private SortComparer(bool desc) => _desc = desc;

        public int Compare(IComparable? x, IComparable? y) => (x, y) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            (string a, string b) => (_desc ? -1 : 1) * string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase),
            _ => (_desc ? -1 : 1) * x.CompareTo(y),
        };
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

    /// <summary>
    /// Validates every cell (required, number format and range, date format, known options), then
    /// <paramref name="rowRule"/> over each row whose cells are valid (a check across cells: "Due is before the
    /// start."), as a row error. Read-only and locked rows are skipped: they can't be fixed in the grid.
    /// </summary>
    public static IReadOnlyList<GridError> Validate(IEnumerable<GridColumn> columns, IEnumerable<GridRecord> rows, Func<GridRecord, string?>? rowRule = null)
    {
        var errors = new List<GridError>();
        var cols = columns.ToList();
        foreach (var row in rows)
        {
            if (!row.Editable) continue;
            var before = errors.Count;
            foreach (var c in cols)
            {
                if (ValidateCell(c, row[c.Key]) is { } message) errors.Add(new GridError(row.Id, c.Key, message));
            }
            if (errors.Count == before && rowRule?.Invoke(row) is { Length: > 0 } rowMessage) errors.Add(GridError.ForRow(row.Id, rowMessage));
        }
        return errors;
    }

    /// <summary>The cell can be edited: its column is editable and its row neither read-only nor locked.</summary>
    public static bool CanEdit(GridColumn column, GridRecord row) => column.Editable && row.Editable;

    /// <summary>"Locked: Invoiced on 3 Oct", "Read-only", or null for an editable row: the row's tooltip and its mark's spoken text.</summary>
    public static string? StateText(GridRecord row)
    {
        var state = row.State switch
        {
            GridRowState.Locked => "Locked",
            GridRowState.ReadOnly => "Read-only",
            _ => null,
        };
        return state is null || string.IsNullOrWhiteSpace(row.StateReason) ? state : $"{state}: {row.StateReason}";
    }

    /// <summary>The Ant Design icon that marks a read-only (<c>eye</c>) or locked (<c>lock</c>) row, or null.</summary>
    public static string? StateIcon(GridRecord row) => row.State switch
    {
        GridRowState.Locked => "lock",
        GridRowState.ReadOnly => "eye",
        _ => null,
    };

    /// <summary>The row's state class: <c>m-grid-row-readonly</c> or <c>m-grid-row-locked</c>, or null.</summary>
    public static string? StateClass(GridRecord row) => row.State switch
    {
        GridRowState.Locked => "m-grid-row-locked",
        GridRowState.ReadOnly => "m-grid-row-readonly",
        _ => null,
    };

    /// <summary>"3 cells need attention", or rows when some errors are on rows: the grid's error banner.</summary>
    public static string ErrorSummary(IEnumerable<GridError> errors)
    {
        var list = errors.ToList();
        var noun = list.All(e => !e.IsRow) ? "cell" : list.All(e => e.IsRow) ? "row" : "item";
        return $"{list.Count} {noun}{(list.Count == 1 ? " needs" : "s need")} attention before saving.";
    }

    // ---- The batch bar (GridBatchAction) ----

    /// <summary>The rows of <paramref name="selected"/> that <paramref name="action"/> acts on: not locked, and <see cref="GridBatchAction.When"/>.</summary>
    public static List<GridRecord> BatchRows(GridBatchAction action, IEnumerable<GridRecord> selected) =>
        [.. selected.Where(r => Applies(action, r))];

    /// <summary>Whether <paramref name="action"/> acts on <paramref name="row"/> (a print takes every row).</summary>
    public static bool Applies(GridBatchAction action, GridRecord row) =>
        action.Print || (row.State != GridRowState.Locked && (action.When?.Invoke(row) ?? true));

    /// <summary>The keys of the actions that act on <paramref name="row"/>, space-separated (MVC: the row's <c>data-m-batch</c>).</summary>
    public static string BatchKeys(IEnumerable<GridBatchAction> actions, GridRecord row) =>
        string.Join(' ', actions.Where(a => !a.Print && Applies(a, row)).Select(a => a.Key));

    /// <summary>"Approve 3 rows?", and " 2 selected rows are skipped." when some don't apply: the confirm.</summary>
    public static string BatchConfirm(GridBatchAction action, int count, int selected)
    {
        var skipped = selected - count;
        return $"{action.Label} {count} {(count == 1 ? "row" : "rows")}?" +
            (skipped > 0 ? $" {skipped} selected {(skipped == 1 ? "row is" : "rows are")} skipped." : "");
    }

    /// <summary>The batch button's tooltip when none of the selected rows apply.</summary>
    public static string BatchNone(GridBatchAction action) => $"{action.Label} doesn't apply to the selected rows.";

    /// <summary>A valid <see cref="GridBatchAction.Key"/> (it travels in a post).</summary>
    public static bool IsBatchKey(string? key) =>
        !string.IsNullOrEmpty(key) && key.Length <= 40 && key.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');

    /// <summary>
    /// Keeps a posted grid (MVC: <c>EditableGridForm.Read</c>) to the row states of the <paramref name="stored"/> rows,
    /// since a browser can post anything: read-only and locked rows get their stored values, state and reason back, and
    /// a locked row missing from the post (deleted in the browser) is put back where it was. Call it before applying
    /// the post's operation.
    /// </summary>
    public static void Protect(IEnumerable<GridColumn> columns, List<GridRecord> posted, IReadOnlyList<GridRecord> stored)
    {
        var cols = columns.ToList();
        for (var i = 0; i < stored.Count; i++)
        {
            var s = stored[i];
            if (s.Editable) continue;
            var p = posted.Find(r => r.Id == s.Id);
            if (p is null)
            {
                if (s.State != GridRowState.Locked) continue;
                p = new GridRecord(s.Id);
                foreach (var c in cols) p[c.Key] = c.Type == GridCellType.Switch ? "false" : null;
                posted.Insert(Math.Min(i, posted.Count), p);
            }
            // The posted columns: one removed in this session stays removed, one added keeps its empty value.
            foreach (var c in cols)
                if (s.Values.TryGetValue(c.Key, out var value)) p[c.Key] = value;
            p.State = s.State;
            p.StateReason = s.StateReason;
        }
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
