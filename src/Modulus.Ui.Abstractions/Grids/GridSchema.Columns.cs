using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
public static partial class GridSchema
{
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
}

