using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Modulus.AspNetCore.Mvc.Htmx;

/// <summary>
/// Server side of SPA navigation (<see cref="Modulus.Ui.Abstractions.Theming.ModulusThemeOptions.SpaNavigation"/>). A boosted
/// request gets only the page body (<c>#main</c>) and the navigation around it, which <c>modulus-spa.js</c> swaps in. The
/// shell, scripts and Alpine state stay loaded. <c>_ModulusLayout</c> calls this; a custom layout can do the same.
/// </summary>
public static class SpaNavigation
{
    /// <summary>Response header that marks a shell fragment. A boosted response without it (a page outside the shell, a file) loads as a whole page.</summary>
    public const string FragmentHeader = "X-Modulus-Fragment";

    /// <summary>Request header with the app (<c>ShellApp.MenuKey</c>) of the page on screen, so the server resends the app launcher only when it changes.</summary>
    public const string AppHeader = "X-Modulus-App";

    /// <summary>
    /// Headers for every response of a layout with SPA navigation on: full pages and fragments share URLs, so caches must
    /// key on the htmx headers.
    /// </summary>
    public static void AddVary(HttpResponse response) =>
        response.Headers.Append(HeaderNames.Vary, "HX-Request, HX-Boosted");

    /// <summary>
    /// Marks the response as a page fragment. <c>modulus-spa.js</c> swaps a page navigation (a link or form whose nearest
    /// <c>hx-boost</c> is &lt;body&gt;) into <c>#main</c>. A boost area of its own (a list's filters and pager with their
    /// <c>hx-target</c> and <c>hx-select</c>) keeps its targeting and only takes what it selects.
    /// </summary>
    public static void PrepareFragment(HttpResponse response) => response.Headers[FragmentHeader] = "1";

    /// <summary>True when the page on screen belongs to a different app than <paramref name="appKey"/> (or its app is unknown).</summary>
    public static bool AppChanged(HttpRequest request, string appKey) =>
        !request.Headers.TryGetValue(AppHeader, out var current) || current.ToString() != appKey;

    /// <summary>
    /// Marks the element with <c>id="{id}"</c> in <paramref name="html"/> as an htmx out-of-band swap, so it replaces the
    /// element with that id on the page. Returns <paramref name="html"/> unchanged when the id isn't in it.
    /// </summary>
    public static string Oob(string html, string id)
    {
        var marker = $"id=\"{id}\"";
        var at = html.IndexOf(marker, StringComparison.Ordinal);
        return at < 0 ? html : html.Insert(at + marker.Length, " hx-swap-oob=\"true\"");
    }
}
