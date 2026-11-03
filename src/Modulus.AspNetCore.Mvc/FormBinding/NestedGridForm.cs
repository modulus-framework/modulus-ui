using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc;

/// <summary>What <c>app-nested-grid</c> renders: one level's page, as loaded or as posted back.</summary>
/// <param name="LoadedIds">Ids of the rows the page was loaded with; Save deletes the ones no longer posted.</param>
/// <param name="Totals">The level's rollup totals over every matching row (<see cref="NestedGridPage.Totals"/>), shown under the table.</param>
public sealed record NestedGridView(
    NestedGridSettings Settings,
    NestedGridRequest Request,
    IList<GridColumn> Columns,
    IReadOnlyList<GridRecord> Rows,
    int Total,
    IReadOnlyList<string> LoadedIds,
    GridRecord? Parent = null,
    IEnumerable<GridError>? Errors = null,
    bool Dirty = false,
    string? Title = null,
    IReadOnlyList<TableTotal>? Totals = null);

/// <summary>
/// The URL state and form fields of an <c>app-nested-grid</c> level. Every level loads and posts to one endpoint:
/// <c>?level=1&amp;parent=PO-1001&amp;q=&amp;sort=qty:desc&amp;f.color=navy&amp;s.style=polo&amp;page=2</c>; the grid's fields are named <c>l{level}</c>.
/// </summary>
public static partial class NestedGridForm
{
    public static string FieldName(int level) => $"l{level}";

    /// <summary>Id of the element a level swaps (unique per level and parent).</summary>
    public static string ContainerId(int level, string? parentId) =>
        $"nested-{level}-{(parentId is null ? "root" : SafeId().Replace(parentId, "_"))}";

    [GeneratedRegex("[^A-Za-z0-9_-]")]
    private static partial Regex SafeId();

    /// <summary>
    /// Reads level, parent, keyword, sort and page from the query. The page size is the level's (settings), or
    /// <c>size</c> when it is one the pager offers (<see cref="Paging.SizesWith"/>).
    /// </summary>
    public static NestedGridRequest ReadRequest(IQueryCollection query, NestedGridSettings settings)
    {
        var level = int.TryParse(query["level"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? Math.Clamp(l, 0, Math.Max(0, settings.LevelCount - 1)) : 0;
        var parent = level == 0 ? null : NullIfEmpty(query["parent"]);
        var sort = NullIfEmpty(query["sort"])?.Split(':');
        var field = sort?[0] is { Length: > 0 } f ? f : null;
        var page = int.TryParse(query["page"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? Math.Max(1, p) : 1;
        // Filters are f.{column key}=value; only well-formed keys are kept (the level ignores ones it doesn't have).
        var filters = query.Where(kv => kv.Key.StartsWith(FilterPrefix, StringComparison.Ordinal) && FilterKey().IsMatch(kv.Key[FilterPrefix.Length..]) && !string.IsNullOrEmpty(kv.Value))
            .Take(20).ToDictionary(kv => kv.Key[FilterPrefix.Length..], kv => (string?)kv.Value.ToString(), StringComparer.Ordinal);
        // Column searches are s.{column key}=query (ColumnSearch.FromQuery keeps the well-formed ones).
        var searches = query.ColumnSearches();
        var levelSize = Math.Max(1, settings.Level(level)?.PageSize ?? 5);
        var custom = int.TryParse(query["size"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sz) && Paging.SizesWith(levelSize).Contains(sz);
        return new NestedGridRequest(level, parent, NullIfEmpty(query["q"]), field, sort?.ElementAtOrDefault(1) == "desc", page, custom ? sz : levelSize,
            filters.Count == 0 ? null : filters, searches)
        { CustomPageSize = custom };
    }

    /// <summary>Query prefix of a level's filters (<c>f.status=draft</c>).</summary>
    public const string FilterPrefix = "f.";

    [GeneratedRegex("^[A-Za-z0-9]{1,40}$")]
    private static partial Regex FilterKey();

    /// <summary>The endpoint URL for a request; <paramref name="omit"/> leaves out parameters an input supplies itself.</summary>
    public static string Url(string baseUrl, NestedGridRequest request, params string[] omit)
    {
        var values = new List<KeyValuePair<string, string?>> { new("level", request.Level.ToString(CultureInfo.InvariantCulture)) };
        if (request.ParentId is not null) values.Add(new("parent", request.ParentId));
        if (!string.IsNullOrEmpty(request.Keyword)) values.Add(new("q", request.Keyword));
        if (request.SortField is not null) values.Add(new("sort", $"{request.SortField}:{(request.Descending ? "desc" : "asc")}"));
        values.AddRange(request.ActiveFilters.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => new KeyValuePair<string, string?>(FilterPrefix + f.Key, f.Value)));
        values.AddRange(request.ActiveSearches.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => new KeyValuePair<string, string?>(ColumnSearch.Prefix + s.Key, s.Value)));
        if (request.CustomPageSize) values.Add(new("size", request.PageSize.ToString(CultureInfo.InvariantCulture)));
        if (request.Page > 1) values.Add(new("page", request.Page.ToString(CultureInfo.InvariantCulture)));
        return QueryHelpers.AddQueryString(baseUrl, values.Where(v => !omit.Contains(v.Key)));
    }

    /// <summary>The ids the level was loaded with (posted as <c>l{level}.loaded</c>).</summary>
    public static IReadOnlyList<string> LoadedIds(IFormCollection form, int level) =>
        form[$"{FieldName(level)}.loaded"].Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).Distinct().ToList();

    /// <summary>
    /// The changes a Save hands the app: the posted rows (in the order they were dragged to, <see cref="NestedGridChanges.Ordered"/>
    /// unless a column sort was on) and the loaded rows no longer there.
    /// </summary>
    public static NestedGridChanges Changes(EditableGridPost post, IFormCollection form, NestedGridRequest request)
    {
        var posted = post.Rows.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        return new NestedGridChanges(request.Level, request.ParentId, post.Columns, post.Rows,
            LoadedIds(form, request.Level).Where(id => !posted.Contains(id)).ToList())
        { Ordered = request.SortField is null };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
