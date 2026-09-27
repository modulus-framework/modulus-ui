namespace Modulus.Ui.Abstractions.Forms;

/// <summary>
/// A record a lookup field can pick (Odoo many2one, SAP value help, Oracle list of values): the stored
/// <see cref="Value"/> (id or code), the <see cref="Label"/> shown in the field, a second line in the suggestion
/// list, and extra <see cref="Fields"/> shown as columns in the "Search more" dialog.
/// </summary>
public sealed record LookupItem(string Value, string Label)
{
    public string? Description { get; init; }

    public IReadOnlyDictionary<string, string>? Fields { get; init; }
}

/// <summary>A column of the lookup's "Search more" dialog. Key is "value", "label", "description" or a key of <see cref="LookupItem.Fields"/>.</summary>
public sealed record LookupColumn(string Key, string Title);

/// <summary>Search rules of the lookup field, mirrored by <c>mLookup</c> in modulus-widgets.js.</summary>
public static class LookupSearch
{
    /// <summary>Default columns: code and name, plus the description.</summary>
    public static IReadOnlyList<LookupColumn> DefaultColumns { get; } = [new("value", "Code"), new("label", "Name"), new("description", "Details")];

    /// <summary>
    /// Items where every word of <paramref name="text"/> appears (case-insensitive) in the value, label,
    /// description or a field. Labels that start with the text come first, then values that do, then the rest,
    /// each in list order. Empty text returns the list as it is.
    /// </summary>
    public static IReadOnlyList<LookupItem> Filter(IEnumerable<LookupItem> items, string? text, int max = int.MaxValue)
    {
        var q = (text ?? "").Trim();
        if (q.Length == 0) return items.Take(max).ToList();
        var terms = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return items
            .Select((item, index) => (item, index))
            .Where(x => terms.All(t => Haystack(x.item).Contains(t, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.item.Label.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : x.item.Value.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ThenBy(x => x.index)
            .Take(max)
            .Select(x => x.item)
            .ToList();
    }

    /// <summary>
    /// What a field does with text the user leaves behind: the item whose label or value equals it
    /// (case-insensitive), else the only match; null when there is none or more than one.
    /// </summary>
    public static LookupItem? Resolve(IEnumerable<LookupItem> items, string? text)
    {
        var q = (text ?? "").Trim();
        if (q.Length == 0) return null;
        var list = items as IReadOnlyList<LookupItem> ?? items.ToList();
        var exact = list.FirstOrDefault(i => string.Equals(i.Label, q, StringComparison.OrdinalIgnoreCase) || string.Equals(i.Value, q, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;
        var matches = Filter(list, q, 2);
        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>The text of one dialog cell.</summary>
    public static string Cell(LookupItem item, string key) => key switch
    {
        "value" => item.Value,
        "label" => item.Label,
        "description" => item.Description ?? "",
        _ => item.Fields is not null && item.Fields.TryGetValue(key, out var v) ? v : "",
    };

    private static string Haystack(LookupItem i) =>
        string.Join('\u001f', new[] { i.Value, i.Label, i.Description ?? "" }.Concat(i.Fields?.Values ?? []));
}

/// <summary>
/// A stage of a record's workflow for the status bar (Odoo statusbar, SAP document status): Draft → Sent →
/// Confirmed → Done. A <see cref="Folded"/> stage (Cancelled, On hold) shows only while it is the current one.
/// </summary>
public sealed record WorkflowStage(string Key, string Label)
{
    public bool Folded { get; init; }

    /// <summary>The stages to draw: every unfolded stage, plus a folded one when it is current.</summary>
    public static IReadOnlyList<WorkflowStage> Visible(IEnumerable<WorkflowStage> stages, string? current) =>
        stages.Where(s => !s.Folded || s.Key == current).ToList();
}
