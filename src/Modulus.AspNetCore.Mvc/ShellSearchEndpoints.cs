using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.AspNetCore.Mvc;

/// <summary>The endpoint the header search (htmx) asks for its results.</summary>
public static class ShellSearchEndpoints
{
    /// <summary>How long the record providers may take. A provider that is slower is left out of that answer; the menu results never wait for it.</summary>
    public static readonly TimeSpan ProviderBudget = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// Maps <c>GET</c> <see cref="ShellRenderer.SearchEndpoint"/>: an HTML fragment with the pages of the caller's menu that match <c>q</c>
    /// (ranked), and the groups of every <see cref="IShellSearchProvider"/> (records). Fewer than two characters answer nothing. Anonymous
    /// callers get the menu pages they may open, so the endpoint is open; the providers see the caller's principal and apply its permissions.
    /// </summary>
    public static IEndpointConventionBuilder MapModulusShellSearch(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(ShellRenderer.SearchEndpoint, Handle).AllowAnonymous();

    /// <summary>How long a person's filtered menu is reused by the search: a burst of keystrokes asks once, and a permission change shows within this time.</summary>
    public static readonly TimeSpan MenuCacheLifetime = TimeSpan.FromSeconds(15);

    private static async Task<IResult> Handle(string? q, IMenuProvider menus, IAuthorizationService authorization,
        IEnumerable<IShellSearchProvider> providers, HttpContext http, CancellationToken ct)
    {
        var query = (q ?? "").Trim();
        if (query.Length < 2)
            return Results.Content("", "text/html");

        var menu = await PersonMenu(menus, authorization, http);

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(ProviderBudget);
        var answers = await Task.WhenAll(providers.Select(provider => Ask(provider, query, http, budget.Token)));
        var groups = answers.SelectMany(g => g).ToList();

        return Results.Content(ShellRenderer.SearchResults(menu, query, groups), "text/html");
    }

    // The filtered menu of this person, company and culture, reused for MenuCacheLifetime when the host registers a memory cache.
    // Nothing is shared between people: the key carries the caller's identifier, and anonymous callers share only the public menu.
    private static async Task<IReadOnlyList<MenuItemDefinition>> PersonMenu(IMenuProvider menus, IAuthorizationService authorization, HttpContext http)
    {
        async Task<IReadOnlyList<MenuItemDefinition>> Build() =>
            await MenuFilter.FilterAsync(await menus.GetMainMenuAsync(),
                async policy => (await authorization.AuthorizeAsync(http.User, policy)).Succeeded);

        var cache = http.RequestServices.GetService<IMemoryCache>();
        if (cache is null)
            return await Build();

        var who = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        var company = http.Request.Cookies[ShellRenderer.TenantCookie] ?? "";
        var key = $"modulus-shell-search-menu:{who}:{company}:{System.Globalization.CultureInfo.CurrentUICulture.Name}";
        return await cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = MenuCacheLifetime;
            return Build();
        }) ?? [];
    }

    // A provider that fails or runs past the budget answers nothing; the rest of the results still show.
    private static async Task<IReadOnlyList<ShellSearchGroup>> Ask(IShellSearchProvider provider, string query, HttpContext http, CancellationToken ct)
    {
        try
        {
            return await provider.SearchAsync(query, http.User, ct);
        }
        catch (Exception)
        {
            return [];
        }
    }
}
