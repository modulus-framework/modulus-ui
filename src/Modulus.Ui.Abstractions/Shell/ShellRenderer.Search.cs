using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.Ui.Abstractions.Shell;
public static partial class ShellRenderer
{
    // Header ---------------------------------------------------------------------------------------

    /// <summary>Where the header search (<see cref="ShellView.ServerSearch"/>) asks for its results; mapped by <c>MapModulusShellSearch</c>.</summary>
    public const string SearchEndpoint = "/_ui/search";

    // The search box of an htmx + Alpine shell: htmx fetches the results for what is typed, Alpine (modulusSearch in
    // modulus-mvc.js) keeps the open state, the arrow keys, Enter, Escape and the shortcuts.
    private static string ServerSearchBox(string placeholder) =>
        $"""<div class="m-search" data-m-search data-m-search-server role="search" x-data="modulusSearch" @keydown.window="shortcut($event)" @modulus-search-open.window="focus()" @click.outside="close(false)">""" +
        Icon("search", "m-search-icon") +
        $"""<input type="search" name="q" class="m-search-input" x-ref="input" placeholder="{E(placeholder)}" aria-label="Search pages" autocomplete="off" spellcheck="false" aria-controls="m-search-results" :aria-expanded="open" hx-get="{SearchEndpoint}" hx-trigger="input changed delay:120ms, search" hx-target="#m-search-results" hx-sync="this:replace" @input="open = true" @focus="open = true" @keydown.down.prevent="move(1)" @keydown.up.prevent="move(-1)" @keydown.enter.prevent="choose()" @keydown.escape.stop="close(true)" />""" +
        """<span class="m-search-kbd" aria-hidden="true"><kbd data-m-mod-key>Ctrl</kbd><kbd>K</kbd></span>""" +
        """<div class="m-search-results" id="m-search-results" role="listbox" x-ref="results" x-show="open" x-cloak @click="if ($event.target.closest('a')) close(true)"></div></div>""";

    // The search box of the Blazor shell: modulus-shell.js filters the navigation links it finds on the page.
    private static string ClientSearchBox(string placeholder) =>
        """<div class="m-search" data-m-search role="search">""" +
        Icon("search", "m-search-icon") +
        $"""<input type="search" class="m-search-input" placeholder="{E(placeholder)}" aria-label="Search pages" autocomplete="off" spellcheck="false" aria-controls="m-search-results" aria-expanded="false" />""" +
        """<span class="m-search-kbd" aria-hidden="true"><kbd data-m-mod-key>Ctrl</kbd><kbd>K</kbd></span>""" +
        """<div class="m-search-results" id="m-search-results" role="listbox" hidden></div></div>""";

    /// <summary>
    /// The header search's results for <paramref name="query"/>: the pages of the caller's menu (the tree the sidebar draws) whose name or
    /// group trail contains the query, ranked exact, then prefix, then contains; then each provider group (records). Fewer than two characters
    /// give no results. The first result is active, so Enter opens it.
    /// </summary>
    public static string SearchResults(IReadOnlyList<MenuItemDefinition> menu, string query) => SearchResults(menu, query, []);

    /// <summary>The header search's results, with the groups the search providers answered (see <see cref="IShellSearchProvider"/>).</summary>
    public static string SearchResults(IReadOnlyList<MenuItemDefinition> menu, string query, IReadOnlyList<ShellSearchGroup> groups)
    {
        var q = query.Trim();
        if (q.Length < 2) return "";
        q = q[..Math.Min(q.Length, 100)];
        const int pageCap = 12, groupCap = 6, total = 14;

        var matches = new List<(MenuItemDefinition Item, string Trail)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in menu) CollectMatches(item, "", q, matches, seen);
        // Exact label first, then labels that start with the query; the menu's own order breaks ties (OrderBy is stable).
        var ranked = matches.OrderBy(m => Rank(m.Item.Label, q)).Take(pageCap).ToList();

        var sections = new List<(string? Title, IReadOnlyList<(string Label, string Href, string? Detail, string? Icon)> Rows)>();
        // A heading names a section only when there is another one to tell apart; a lone page list starts with its first result.
        var pagesTitle = groups.Any(g => g.Items.Count > 0) ? "Pages" : null;
        if (ranked.Count > 0)
            sections.Add((pagesTitle, ranked.Select(m => (m.Item.Label, Href(m.Item), m.Trail.Length == 0 ? null : m.Trail, m.Item.Icon)).ToList()));
        foreach (var group in groups)
            if (group.Items.Count > 0)
                sections.Add((group.Title, group.Items.Take(groupCap).Select(i => (i.Label, E("/" + i.Href.TrimStart('/')), i.Detail, (string?)null)).ToList()));

        if (sections.Count == 0) return $"""<div class="m-search-empty">No pages match “{E(q)}”</div>""";

        var sb = new StringBuilder();
        var shown = 0;
        var first = true;
        foreach (var (title, rows) in sections)
        {
            if (shown >= total) break;
            if (title is not null) sb.Append($"""<div class="m-search-group" role="presentation">{E(title)}</div>""");
            foreach (var (label, href, detail, icon) in rows)
            {
                if (shown++ >= total) break;
                sb.Append($"""<a class="m-search-item{(first ? " active" : "")}" href="{href}" role="option">""")
                  .Append(icon is { } name ? Icon(name) : "")
                  .Append($"""<span>{E(label)}</span>""")
                  .Append(detail is null ? "" : $"""<span class="m-search-item-section">{E(detail)}</span>""")
                  .Append("</a>");
                first = false;
            }
        }
        return sb.ToString();
    }

    private static int Rank(string label, string query) =>
        string.Equals(label, query, StringComparison.OrdinalIgnoreCase) ? 0
        : label.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1
        : 2;

    // Leaves only, like the sidebar: a group opens to its pages and has no link of its own.
    private static void CollectMatches(MenuItemDefinition item, string trail, string query, List<(MenuItemDefinition Item, string Trail)> matches, HashSet<string> seen)
    {
        if (item.Children.Count > 0)
        {
            foreach (var child in item.Children) CollectMatches(child, Join(trail, item.Label), query, matches, seen);
            return;
        }
        if (item.Href is null || !seen.Add(item.Href)) return;
        if ((item.Label + " " + trail).Contains(query, StringComparison.OrdinalIgnoreCase)) matches.Add((item, trail));
    }

}
