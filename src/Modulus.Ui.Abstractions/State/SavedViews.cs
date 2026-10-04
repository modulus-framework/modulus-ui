using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions.State;

/// <summary>
/// A named view of a page (Odoo's favourites): its query string, which on Modulus pages holds the whole layout —
/// filters, sort, hidden columns, a pivot's group-bys. <paramref name="IsDefault"/> opens the page in this view when its
/// URL names none.
/// </summary>
public sealed record SavedView(string Name, string Query, bool IsDefault = false);

/// <summary>
/// A user's saved views of each page, kept in the <see cref="IUiStateStore"/> under <c>views:{page}</c>. Queries are
/// stored in a canonical form (<see cref="Canonical"/>), so the view that matches the current URL can be marked.
/// </summary>
public sealed class SavedViews(IUiStateStore store)
{
    /// <summary>Views one user keeps per page.</summary>
    public const int MaxViews = 20;

    public const int MaxName = 60;

    /// <summary>Longest query a view keeps (a URL is the state, so it stays URL-sized).</summary>
    public const int MaxQuery = 2000;

    /// <summary>Query keys that are never part of a view: the position in the list, not its layout.</summary>
    public static readonly IReadOnlyList<string> Transient = ["page", "current", "cursor", "after", "before", "handler", "_", SavedViewsHtml.ErrorKey];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    private static string Key(string page) => "views:" + page;

    /// <summary>The views of <paramref name="page"/>, in the order they were saved.</summary>
    public async Task<IReadOnlyList<SavedView>> ListAsync(string user, string page, CancellationToken cancellationToken = default)
    {
        if (user.Length == 0) return [];
        var json = await store.GetAsync(user, Key(page), cancellationToken);
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            return (JsonSerializer.Deserialize<List<SavedView>>(json, Json) ?? [])
                .Where(v => !string.IsNullOrWhiteSpace(v.Name) && v.Query is not null).Take(MaxViews).ToList();
        }
        catch (JsonException)
        {
            return []; // a value an older version wrote: start again
        }
    }

    /// <summary>
    /// Saves <paramref name="query"/> as <paramref name="name"/>, replacing a view of the same name (case-insensitive).
    /// A default view clears the others' default. Returns an error message instead when the name is empty or too long,
    /// the user is anonymous, or the page has <see cref="MaxViews"/> views already.
    /// </summary>
    public async Task<string?> SaveAsync(string user, string page, string? name, IEnumerable<KeyValuePair<string, string?>> query,
        bool isDefault = false, CancellationToken cancellationToken = default)
    {
        if (user.Length == 0) return "Sign in to save views.";
        name = name?.Trim();
        if (string.IsNullOrEmpty(name)) return "Name the view.";
        if (name.Length > MaxName) return $"Use at most {MaxName} characters.";
        var canonical = Canonical(query);
        if (canonical.Length > MaxQuery) return "This view is too large to save.";

        var views = (await ListAsync(user, page, cancellationToken)).ToList();
        var at = views.FindIndex(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
        if (at < 0 && views.Count >= MaxViews) return $"A page keeps at most {MaxViews} views; delete one first.";
        if (isDefault) views = views.Select(v => v with { IsDefault = false }).ToList();
        var view = new SavedView(name, canonical, isDefault);
        if (at >= 0) views[at] = view; else views.Add(view);
        await WriteAsync(user, page, views, cancellationToken);
        return null;
    }

    /// <summary>Deletes the view named <paramref name="name"/>.</summary>
    public async Task DeleteAsync(string user, string page, string name, CancellationToken cancellationToken = default)
    {
        var views = (await ListAsync(user, page, cancellationToken)).ToList();
        if (views.RemoveAll(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)) > 0)
            await WriteAsync(user, page, views, cancellationToken);
    }

    /// <summary>Makes <paramref name="name"/> the page's default view; null (or an unknown name) clears the default.</summary>
    public async Task SetDefaultAsync(string user, string page, string? name, CancellationToken cancellationToken = default)
    {
        var views = await ListAsync(user, page, cancellationToken);
        if (views.Count == 0) return;
        await WriteAsync(user, page, views.Select(v => v with { IsDefault = name is not null && string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase) }).ToList(),
            cancellationToken);
    }

    /// <summary>The page's default view, if it has one.</summary>
    public async Task<SavedView?> DefaultAsync(string user, string page, CancellationToken cancellationToken = default) =>
        (await ListAsync(user, page, cancellationToken)).FirstOrDefault(v => v.IsDefault);

    /// <summary>
    /// Where a page whose URL names no view state should go instead: <paramref name="path"/> with the default view's
    /// query, or null to stay (no default, or the URL already has a state).
    /// </summary>
    public async Task<string?> DefaultUrlAsync(string user, string page, string path, IEnumerable<KeyValuePair<string, string?>> query,
        CancellationToken cancellationToken = default)
    {
        if (Canonical(query).Length > 0) return null;
        return await DefaultAsync(user, page, cancellationToken) is { Query.Length: > 0 } view ? Url(path, view) : null;
    }

    private ValueTask WriteAsync(string user, string page, IReadOnlyList<SavedView> views, CancellationToken cancellationToken) =>
        store.SetAsync(user, Key(page), views.Count == 0 ? null : JsonSerializer.Serialize(views, Json), cancellationToken);

    /// <summary>
    /// A query in the form views keep: empty values and <see cref="Transient"/> keys dropped, keys in ordinal order
    /// (values of one key keep theirs), each part escaped. Two URLs of the same layout give the same string.
    /// </summary>
    public static string Canonical(IEnumerable<KeyValuePair<string, string?>> query)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in query
                     .Where(q => !string.IsNullOrEmpty(q.Key) && !string.IsNullOrEmpty(q.Value) && !Transient.Contains(q.Key, StringComparer.OrdinalIgnoreCase))
                     .OrderBy(q => q.Key, StringComparer.Ordinal))
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value!));
        }
        return sb.ToString();
    }

    /// <summary>Parses a query string ("a=1&amp;b=2", with or without "?") into its pairs.</summary>
    public static IReadOnlyList<KeyValuePair<string, string?>> Parse(string? query)
    {
        var list = new List<KeyValuePair<string, string?>>();
        foreach (var part in (query ?? "").TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            string Decode(string s) => Uri.UnescapeDataString(s.Replace('+', ' '));
            list.Add(eq < 0 ? new(Decode(part), "") : new(Decode(part[..eq]), Decode(part[(eq + 1)..])));
        }
        return list;
    }

    /// <summary>The view whose query is the current one (<paramref name="query"/>), if any.</summary>
    public static SavedView? Current(IEnumerable<SavedView> views, IEnumerable<KeyValuePair<string, string?>> query)
    {
        var canonical = Canonical(query);
        return views.FirstOrDefault(v => v.Query == canonical);
    }

    /// <summary>The link of a view on <paramref name="path"/>.</summary>
    public static string Url(string path, SavedView view) => view.Query.Length == 0 ? path : path + "?" + view.Query;
}
