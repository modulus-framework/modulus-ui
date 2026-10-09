using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Ui.Abstractions.Dashboard;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// A dashboard of tiles (<see cref="DashboardView"/>), the MVC front end of the Blazor <c>DashboardGrid</c>:
/// <code>&lt;app-dashboard view="@Model" path="/dashboards/tiles" bodies="@Bodies" /&gt;</code>
/// Each tile's buttons are a form that posts to <see cref="DashboardEndpoints.Endpoint"/> with the tile and the layout the page
/// drew, and the endpoint saves the change and redirects back. <c>bodies</c> maps a tile's id to the HTML drawn in it.
/// </summary>
[HtmlTargetElement("app-dashboard", TagStructure = TagStructure.WithoutEndTag)]
public class DashboardTagHelper : TagHelper
{
    [ViewContext]
    public ViewContext ViewContext { get; set; } = default!;

    /// <summary>The dashboard to draw.</summary>
    public DashboardView? View { get; set; }

    /// <summary>The page the dashboard is on (a local path); the endpoint redirects back to it.</summary>
    public string Path { get; set; } = "";

    /// <summary>The HTML of each tile's body, by tile id.</summary>
    public IDictionary<string, IHtmlContent>? Bodies { get; set; }

    private static readonly (string Op, string Label, string Title)[] Moves =
    [
        ("earlier", "Earlier", "Move {0} earlier"),
        ("later", "Later", "Move {0} later"),
        ("narrower", "Narrower", "Make {0} narrower"),
        ("wider", "Wider", "Make {0} wider"),
        ("shorter", "Shorter", "Make {0} shorter"),
        ("taller", "Taller", "Make {0} taller"),
    ];

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var view = View ?? throw new InvalidOperationException("app-dashboard needs a view.");
        var path = Html.E(Path);
        var dashboard = Html.E(view.Id);
        var layout = Html.E(DashboardModel.ToJson(view.Tiles.Select(t => new DashboardPlacement(t.Id, t.Width, t.Height))));
        var token = Token();
        var sb = new StringBuilder();

        foreach (var tile in view.Tiles)
        {
            var id = Html.E(tile.Id);
            sb.Append($"""<form id="dash-{id}" class="m-dash-form" method="post" action="{DashboardEndpoints.Endpoint}" hidden><input type="hidden" name="path" value="{path}" /><input type="hidden" name="dashboard" value="{dashboard}" /><input type="hidden" name="layout" value="{layout}" /><input type="hidden" name="tile" value="{id}" />{token}</form>""");
        }

        sb.Append($"""<div id="{dashboard}" class="m-dash" data-m-dashboard>""");
        foreach (var tile in view.Tiles)
        {
            var id = Html.E(tile.Id);
            var title = Html.E(tile.Title);
            sb.Append($"""<section class="m-dash-tile" style="--w:{tile.Width.ToString(CultureInfo.InvariantCulture)};--h:{tile.Height.ToString(CultureInfo.InvariantCulture)}" aria-label="{title}">""");
            sb.Append($"""<header class="m-dash-head"><h3 class="m-dash-title">{title}</h3><div class="m-dash-tools" role="group" aria-label="Arrange {title}">""");
            foreach (var (op, label, format) in Moves)
            {
                var disabled = op switch
                {
                    "earlier" => tile.First,
                    "later" => tile.Last,
                    "narrower" => tile.Width <= 1,
                    "wider" => tile.Width >= DashboardModel.Columns,
                    "shorter" => tile.Height <= 1,
                    _ => tile.Height >= DashboardModel.MaxHeight,
                };
                sb.Append($"""<button type="submit" form="dash-{id}" name="op" value="{op}" class="ant-btn ant-btn-text ant-btn-sm" aria-label="{Html.E(string.Format(CultureInfo.InvariantCulture, format, tile.Title))}"{(disabled ? " disabled" : "")}>{label}</button>""");
            }
            sb.Append("</div></header>");
            sb.Append("""<div class="m-dash-body">""");
            if (Bodies is not null && Bodies.TryGetValue(tile.Id, out var body)) sb.Append(Write(body));
            sb.Append("</div></section>");
        }
        sb.Append("</div>");

        sb.Append($"""<form class="m-dash-foot" method="post" action="{DashboardEndpoints.Endpoint}"><input type="hidden" name="path" value="{path}" /><input type="hidden" name="dashboard" value="{dashboard}" />{token}<button type="submit" name="op" value="reset" class="ant-btn ant-btn-default ant-btn-sm">Reset layout</button></form>""");

        output.TagName = null;
        output.Content.SetHtmlContent(sb.ToString());
    }

    private string Token()
    {
        var http = ViewContext.HttpContext;
        if (http.RequestServices.GetService<IAntiforgery>() is not { } antiforgery) return "";
        var tokens = antiforgery.GetAndStoreTokens(http);
        return tokens.RequestToken is null ? "" : $"""<input type="hidden" name="{Html.E(tokens.FormFieldName)}" value="{Html.E(tokens.RequestToken)}" />""";
    }

    private static string Write(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }
}
