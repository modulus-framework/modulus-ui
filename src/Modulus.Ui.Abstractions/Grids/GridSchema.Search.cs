using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
public static partial class GridSchema
{
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
}

