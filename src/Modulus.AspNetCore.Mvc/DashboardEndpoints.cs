using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Ui.Abstractions.Dashboard;
using Modulus.Ui.Abstractions.State;

namespace Modulus.AspNetCore.Mvc;

/// <summary>The endpoint the MVC dashboard (<c>&lt;app-dashboard&gt;</c>) posts its tile buttons to.</summary>
public static class DashboardEndpoints
{
    /// <summary>The path the dashboard's forms post to.</summary>
    public const string Endpoint = "/_modulus/dashboard";

    /// <summary>
    /// Maps <c>POST</c> <see cref="Endpoint"/>: a tile's move or size change (<c>op</c>, on <c>tile</c>) applied to the layout the
    /// page drew (<c>layout</c>, JSON), saved for the signed-in user; or <c>op=reset</c> to forget the saved layout. Then back to
    /// the page. It checks the antiforgery token itself and only redirects to a local path.
    /// </summary>
    public static IEndpointConventionBuilder MapModulusDashboard(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(Endpoint, (Delegate)Handle).DisableAntiforgery(); // validated below

    private static readonly HashSet<string> Ops = ["earlier", "later", "wider", "narrower", "taller", "shorter"];

    private static async Task<IResult> Handle(HttpContext http)
    {
        var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();
        if (!await antiforgery.IsRequestValidAsync(http)) return Results.BadRequest();
        var form = await http.Request.ReadFormAsync(http.RequestAborted);
        var path = form["path"].ToString();
        if (!IsLocalPath(path)) return Results.BadRequest();
        var dashboard = form["dashboard"].ToString();
        if (!DashboardModel.IsValidId(dashboard)) return Results.BadRequest();
        var op = form["op"].ToString();
        var store = http.RequestServices.GetRequiredService<IUiStateStore>();
        var user = UiUser.Key(http.User);

        if (op == "reset")
            await DashboardModel.ClearAsync(store, user, dashboard, http.RequestAborted);
        else if (Ops.Contains(op))
        {
            var tile = form["tile"].ToString();
            if (!DashboardModel.IsValidId(tile)) return Results.BadRequest();
            var layout = DashboardModel.ParseJson(form["layout"].ToString());
            if (layout is { Count: > 0 })
                await DashboardModel.SaveAsync(store, user, dashboard, DashboardModel.Apply(layout, op, tile), http.RequestAborted);
        }
        else return Results.BadRequest();

        return Results.LocalRedirect(path);
    }

    private static bool IsLocalPath(string path) =>
        path.Length > 0 && path[0] == '/' && (path.Length == 1 || (path[1] != '/' && path[1] != '\\')) && !path.Contains('?') && !path.Contains('#');
}
