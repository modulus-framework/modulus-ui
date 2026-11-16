using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.Ui.Abstractions.Shell;
/// <summary>Everything the shell needs for one render.</summary>
/// <param name="Options">Theme options with the user's theme-settings cookies applied (<see cref="ThemeCss.ResolveOptions"/>).</param>
/// <param name="Menu">Authorized menu tree.</param>
/// <param name="Path">Current app-relative path without a leading slash.</param>
/// <param name="Mode">"light" or "dark" when known on the server, otherwise null.</param>
/// <param name="TenantId">Value of the <see cref="ShellRenderer.TenantCookie"/> cookie, used when the content names no current tenant.</param>
/// <param name="TenantIds">Value of the <see cref="ShellRenderer.TenantsCookie"/> cookie (active tenants in multi-company mode).</param>
/// <param name="ServerSearch">
/// The header search asks the server for its results (<see cref="ShellRenderer.SearchEndpoint"/>, through htmx) and Alpine
/// runs it. Set by the MVC and Razor Pages layout, which load htmx and Alpine; Blazor searches the rendered links.
/// </param>
public sealed record ShellView(ModulusThemeOptions Options, IReadOnlyList<MenuItemDefinition> Menu, string Path, ShellContent Content, string? Mode = null, string? TenantId = null, string? TenantIds = null, bool ServerSearch = false);

/// <summary>
/// Renders the Tabler-style application shell (sidebar, header, horizontal navbar, footer and the
/// theme settings panel) as HTML. Blazor and MVC / Razor Pages both output these strings, so the
/// shell DOM is identical in every flavor. Behaviour lives in <c>modulus-shell.js</c>.
/// <para>
/// Menu depth: a top-level item with children becomes a dropdown (horizontal) or a collapsible group
/// (vertical). Menus nest to any depth: below that, a child with children is a branch that folds open in place,
/// in the dropdown and in the sidebar alike (a collapsible tree).
/// </para>
/// <para>
/// Apps: a <see cref="ShellApp"/> with a <see cref="ShellApp.MenuKey"/> owns that menu item. While one of its pages
/// is open, the navbar and sidebar are rendered twice, the whole menu and the app's own menu, and the
/// <c>data-menu</c> attribute on &lt;html&gt; (theme settings) shows one of them.
/// </para>
/// </summary>
public static partial class ShellRenderer
{
    /// <summary>Cookie the tenant switcher writes when a tenant has no link of its own.</summary>
    public const string TenantCookie = "modulus-tenant";

    /// <summary>Cookie holding the active tenants in multi-company mode, as comma-separated ids.</summary>
    public const string TenantsCookie = "modulus-tenants";

    /// <summary>Optional prefix of an Ant Design icon name, as in <c>"ant:api"</c>; <c>"api"</c> draws the same icon.</summary>
    public const string AntPrefix = "ant:";

    /// <summary>
    /// Prefix of a Tabler icon name, as in <c>"tb:fingerprint"</c>: the concepts Ant Design has no icon for, drawn as
    /// outline icons at Ant Design's stroke weight (MIT License, https://tabler.io/icons), bundled in <see cref="ExtraIconSvg"/>.
    /// </summary>
    public const string TablerPrefix = "tb:";

    /// <summary>
    /// Ant Design icon, drawn inline: <c>&lt;svg class="m-ti m-ai" data-m-icon="name"&gt;…&lt;/svg&gt;</c>. The name is an
    /// Ant Design outline icon name (<c>"team"</c>, <c>"setting"</c>, <c>"bar-chart"</c>) or a <see cref="TablerPrefix"/> name.
    /// An unknown name renders an empty icon of the same size.
    /// </summary>
    public static string Icon(string name, string? cssClass = null)
    {
        var css = "m-ti" + (cssClass is null ? "" : " " + cssClass);
        var svg = AntIconSvg.Svg(name.StartsWith(AntPrefix, StringComparison.Ordinal) ? name[AntPrefix.Length..] : name);
        return svg is null
            ? $"""<svg class="{css} m-ai" data-m-icon="{E(name)}" aria-hidden="true" focusable="false"></svg>"""
            : svg.Replace("<svg ", $"""<svg class="{css} m-ai" data-m-icon="{E(name)}" """, StringComparison.Ordinal);
    }

}
