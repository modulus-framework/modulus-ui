using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.Ui.Abstractions.Navigation;

/// <summary>
/// One level of a breadcrumb trail: its label, where it leads (null for the current page), an icon (not drawn: levels are text, as in Odoo), and the related
/// pages at that level (its siblings), which open from the level's switcher. <paramref name="Selected"/> marks the related page
/// that is the one on screen; <paramref name="RelatedTitle"/> names the list (defaults to the level's label).
/// </summary>
public sealed record Crumb(string Label, string? Href = null, string? Icon = null, IReadOnlyList<Crumb>? Related = null, bool Selected = false, string? RelatedTitle = null);

/// <summary>
/// The breadcrumb trail in the Odoo manner: the levels from the app down to the page on screen, separated by slashes, the
/// current page bold and unlinked, an app icon first. A trail longer than <c>maxVisible</c> keeps its first and last levels and
/// the most recent middle ones inline; the rest, and on a phone every middle level, sit in a "…" menu that opens in place. The
/// menu is a native <c>details</c>, so it works without script. One renderer for both front ends: the shell's module bar, the
/// Blazor <c>AppCrumbs</c> and the MVC <c>app-crumbs</c>. Labels are text (encoded here).
/// </summary>
public static class CrumbHtml
{
    /// <summary>The trail as markup, or an empty string when there are no levels.</summary>
    public static string Trail(IReadOnlyList<Crumb> crumbs, int maxVisible = 4, string label = "You are here")
    {
        ArgumentNullException.ThrowIfNull(crumbs);
        if (crumbs.Count == 0) return "";

        var last = crumbs.Count - 1;
        var middle = crumbs.Skip(1).Take(crumbs.Count - 2).ToList();
        var inlineMiddle = Math.Max(0, Math.Min(middle.Count, maxVisible - 2));
        var hidden = middle.Count - inlineMiddle;

        var sb = new StringBuilder();
        sb.Append($"""<nav class="m-crumbs" aria-label="{E(label)}"><ol class="m-crumbs-list">""");
        if (crumbs.Count == 1)
        {
            sb.Append(Item(crumbs[0], current: true, extraClass: null));
        }
        else
        {
            sb.Append(Item(crumbs[0], current: false, extraClass: null));
            for (var i = 0; i < inlineMiddle; i++) sb.Append(Item(middle[i], current: false, extraClass: "m-crumb-mid"));
            if (middle.Count > 0) Overflow(sb, middle, hidden > 0);
            sb.Append(Item(crumbs[last], current: true, extraClass: null));
        }
        sb.Append("</ol></nav>");
        return sb.ToString();
    }

    private static string Item(Crumb crumb, bool current, string? extraClass)
    {
        var cls = "m-crumb" + (extraClass is null ? "" : " " + extraClass) + (current ? " m-crumb-current" : "");
        var text = $"""<span class="m-crumb-label">{E(crumb.Label)}</span>""";
        var body = current || crumb.Href is null
            ? text
            : $"""<a href="{E(crumb.Href)}">{text}</a>""";
        return $"""<li class="{cls}"{(current ? " aria-current=\"page\"" : "")}>{body}{Switcher(crumb)}</li>""";
    }

    // The level's related pages, opened from its caret: a dropdown (closes on outside click and Escape, like the module menus).
    private static string Switcher(Crumb crumb)
    {
        if (crumb.Related is not { Count: > 0 } related) return "";
        var title = crumb.RelatedTitle ?? crumb.Label;
        var sb = new StringBuilder();
        sb.Append($"""<details class="m-dropdown m-crumb-switch"><summary class="m-crumb-switch-btn" aria-label="Related pages in {E(title)}" title="Related pages in {E(title)}">{ShellRenderer.Icon("down", "m-crumb-caret")}</summary><div class="m-crumb-panel"><div class="m-crumb-panel-head">{E(title)}</div><ul class="m-crumb-menu">""");
        foreach (var r in related) sb.Append(RelatedItem(r));
        sb.Append("</ul></div></details>");
        return sb.ToString();
    }

    private static string RelatedItem(Crumb crumb)
    {
        var text = $"""<span>{E(crumb.Label)}</span>""";
        if (crumb.Href is null) return $"""<li><span class="m-crumb-menu-static">{text}</span></li>""";
        return $"""<li><a href="{E(crumb.Href)}" class="m-crumb-menu-item{(crumb.Selected ? " m-crumb-menu-selected" : "")}"{(crumb.Selected ? " aria-current=\"page\"" : "")}>{text}</a></li>""";
    }

    // The collapsed levels. Shown on a phone even when every middle level is inline; on a wide screen only when some are hidden.
    private static void Overflow(StringBuilder sb, IReadOnlyList<Crumb> middle, bool someHidden)
    {
        var cls = someHidden ? "m-crumb m-crumb-more" : "m-crumb m-crumb-more m-crumb-phone-only";
        sb.Append($"""<li class="{cls}"><details class="m-dropdown m-crumb-menu-wrap"><summary class="m-crumb-switch-btn" aria-label="Show {middle.Count} more levels" title="Show more levels">&hellip;</summary><div class="m-crumb-panel"><div class="m-crumb-panel-head">Hidden levels</div><ul class="m-crumb-menu">""");
        foreach (var crumb in middle) sb.Append(RelatedItem(crumb));
        sb.Append("</ul></div></details></li>");
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
}
