namespace Modulus.Ui.Abstractions.Forms;

/// <summary>
/// The rules of a tag input (free-form multiple values), shared by the Blazor <c>AppTagInput</c> and the MVC
/// <c>&lt;app-tag-input&gt;</c> so both accept and reject the same text. modulus-shell.js applies the same rules
/// in the browser for the MVC and Razor Pages flavour.
/// </summary>
public static class TagInput
{
    /// <summary>The characters that end a tag when typed or pasted.</summary>
    public const string DefaultSeparators = ",;";

    /// <summary>Splits pasted or typed text at the separators; blanks are dropped and each part is trimmed.</summary>
    public static IReadOnlyList<string> Split(string? text, string? separators = DefaultSeparators)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var chars = (separators ?? DefaultSeparators).Concat(['\n', '\r', '\t']).ToArray();
        return text.Split(chars, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Adds <paramref name="raw"/> (trimmed) to <paramref name="tags"/> unless it is empty, too long (<paramref name="maxLength"/>,
    /// 0 for no limit), a duplicate (compared without case) or the list is full (<paramref name="max"/>, 0 for no limit).
    /// </summary>
    public static bool TryAdd(IList<string> tags, string? raw, int max = 0, int maxLength = 0, bool allowDuplicates = false)
    {
        var tag = raw?.Trim();
        if (string.IsNullOrEmpty(tag)) return false;
        if (maxLength > 0 && tag.Length > maxLength) return false;
        if (max > 0 && tags.Count >= max) return false;
        if (!allowDuplicates && tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))) return false;
        tags.Add(tag);
        return true;
    }

    /// <summary>The tags of a stored value (a posted or saved list), cleaned with the same rules.</summary>
    public static List<string> Clean(IEnumerable<string>? values, int max = 0, int maxLength = 0, bool allowDuplicates = false)
    {
        var list = new List<string>();
        foreach (var v in values ?? []) TryAdd(list, v, max, maxLength, allowDuplicates);
        return list;
    }
}
