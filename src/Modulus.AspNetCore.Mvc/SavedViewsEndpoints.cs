using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Ui.Abstractions.State;

namespace Modulus.AspNetCore.Mvc;

/// <summary>The endpoint the MVC "Views" dropdown (<c>&lt;app-saved-views&gt;</c>) posts to.</summary>
public static class SavedViewsEndpoints
{
    /// <summary>
    /// Maps <c>POST</c> <see cref="SavedViewsHtml.Endpoint"/>: save, delete and make-default for the signed-in user, then
    /// back to the page. It checks the antiforgery token itself and only redirects to a local path.
    /// </summary>
    public static IEndpointConventionBuilder MapModulusSavedViews(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(SavedViewsHtml.Endpoint, (Delegate)Handle).DisableAntiforgery(); // validated below, with the htmx header too

    private static async Task<IResult> Handle(HttpContext http)
    {
        var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();
        if (!await antiforgery.IsRequestValidAsync(http)) return Results.BadRequest();
        var form = await http.Request.ReadFormAsync(http.RequestAborted);
        var path = form["path"].ToString();
        if (!IsLocalPath(path)) return Results.BadRequest();
        var page = form["page"].ToString();
        if (page.Length is 0 or > 200) return Results.BadRequest();
        var query = SavedViews.Parse(form["query"].ToString());
        var views = http.RequestServices.GetRequiredService<SavedViews>();
        var user = UiUser.Key(http.User);
        var op = form["op"].ToString();
        string? error = null;

        if (op == "save")
            error = await views.SaveAsync(user, page, form["name"], query, form["default"] == "true", http.RequestAborted);
        else if (op.StartsWith("delete:", StringComparison.Ordinal))
            await views.DeleteAsync(user, page, op["delete:".Length..], http.RequestAborted);
        else if (op.StartsWith("default:", StringComparison.Ordinal))
        {
            var name = op["default:".Length..];
            var current = (await views.ListAsync(user, page, http.RequestAborted)).FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
            await views.SetDefaultAsync(user, page, current is { IsDefault: false } ? current.Name : null, http.RequestAborted);
        }
        else return Results.BadRequest();

        var back = SavedViews.Canonical(query);
        if (error is not null) back += (back.Length > 0 ? "&" : "") + SavedViewsHtml.ErrorKey + "=" + Uri.EscapeDataString(error);
        return Results.LocalRedirect(back.Length > 0 ? path + "?" + back : path);
    }

    private static bool IsLocalPath(string path) =>
        path.Length > 0 && path[0] == '/' && (path.Length == 1 || (path[1] != '/' && path[1] != '\\')) && !path.Contains('?') && !path.Contains('#');
}
