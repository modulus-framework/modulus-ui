using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.Ui.Abstractions.Navigation;

/// <summary>One level of a breadcrumb trail: its label, where it leads (null for the current page), and an optional icon.</summary>
public sealed record Crumb(string Label, string? Href = null, string? Icon = null);

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
        var icon = crumb.Icon is { Length: > 0 } name ? ShellRenderer.Icon(name, "m-crumb-icon") : "";
        var text = $"""{icon}<span class="m-crumb-label">{E(crumb.Label)}</span>""";
        var body = current || crumb.Href is null
            ? text
            : $"""<a href="{E(crumb.Href)}">{text}</a>""";
        return $"""<li class="{cls}"{(current ? " aria-current=\"page\"" : "")}>{body}</li>""";
    }

    // The collapsed levels. Shown on a phone even when every middle level is inline; on a wide screen only when some are hidden.
    private static void Overflow(StringBuilder sb, IReadOnlyList<Crumb> middle, bool someHidden)
    {
        var cls = someHidden ? "m-crumb m-crumb-more" : "m-crumb m-crumb-more m-crumb-phone-only";
        sb.Append($"""<li class="{cls}"><details class="m-crumb-menu-wrap"><summary aria-label="Show {middle.Count} more levels" title="Show more levels">&hellip;</summary><ul class="m-crumb-menu">""");
        foreach (var crumb in middle)
            sb.Append(crumb.Href is null
                ? $"""<li><span>{E(crumb.Label)}</span></li>"""
                : $"""<li><a href="{E(crumb.Href)}">{E(crumb.Label)}</a></li>""");
        sb.Append("</ul></details></li>");
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
}
