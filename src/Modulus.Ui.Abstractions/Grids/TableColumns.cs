namespace Modulus.Ui.Abstractions;

/// <summary>An optional table column the user can hide from the column picker.</summary>
public sealed record TableColumn(string Key, string Title);

/// <summary>
/// Hidden-column state for a table's column picker, kept as one comma-separated query value
/// (<c>?hide=office,salary</c>) so it survives paging, sorting and reloads.
/// </summary>
public static class TableColumns
{
    /// <summary>Parses a "hide" value, keeping only keys of <paramref name="optional"/> columns.</summary>
    public static HashSet<string> ParseHidden(string? hide, IEnumerable<TableColumn> optional)
    {
        var keys = optional.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        return (hide ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(keys.Contains).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The "hide" value after toggling one column, in column order (null when every column is shown).</summary>
    public static string? ToggleHidden(IReadOnlySet<string> hidden, string key, IEnumerable<TableColumn> optional)
    {
        var next = hidden.Contains(key) ? hidden.Where(k => k != key) : hidden.Append(key);
        var value = string.Join(',', optional.Select(c => c.Key).Where(next.Contains));
        return value.Length == 0 ? null : value;
    }
}
