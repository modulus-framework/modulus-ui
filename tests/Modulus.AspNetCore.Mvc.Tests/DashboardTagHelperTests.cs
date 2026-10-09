using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Dashboard;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-dashboard: a form per tile carrying the layout, the tile's buttons with the ends off, the bodies, and the reset.</summary>
public class DashboardTagHelperTests
{
    private static readonly DashboardTileSpec[] Specs =
    [
        new("revenue", "Revenue", 3, 2),
        new("orders", "Orders", 3, 2),
    ];

    private static async Task<string> RenderAsync(DashboardView view, IDictionary<string, IHtmlContent>? bodies = null)
    {
        var http = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        var viewContext = new ViewContext { HttpContext = http, RouteData = new RouteData(), ActionDescriptor = new ActionDescriptor() };
        var helper = new DashboardTagHelper { ViewContext = viewContext, View = view, Path = "/dashboards/tiles", Bodies = bodies };
        var output = new TagHelperOutput("app-dashboard", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-dashboard", [], new Dictionary<object, object>(), "d1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task DrawsEachTile_WithItsBody_AndTheLayoutInEachForm()
    {
        var view = DashboardModel.View("home", Specs, DashboardModel.Default(Specs));
        var bodies = new Dictionary<string, IHtmlContent> { ["revenue"] = new HtmlString("<b>$1</b>") };

        var html = await RenderAsync(view, bodies);

        Assert.Equal(2, Count(html, "class=\"m-dash-tile\""));
        Assert.Contains("<b>$1</b>", html);
        Assert.Contains("--w:3;--h:2", html);
        Assert.Contains("<input type=\"hidden\" name=\"tile\" value=\"revenue\" />", html);
        Assert.Contains("name=\"layout\" value=\"{&quot;tiles&quot;:[{&quot;id&quot;:&quot;revenue&quot;", html);
        Assert.Contains("action=\"/_modulus/dashboard\"", html);
    }

    [Fact]
    public async Task TheButtons_AreOffAtTheEnds()
    {
        var view = DashboardModel.View("home", Specs, DashboardModel.Default(Specs));

        var html = await RenderAsync(view);

        Assert.Contains("form=\"dash-revenue\" name=\"op\" value=\"earlier\" class=\"ant-btn ant-btn-text ant-btn-sm\" aria-label=\"Move Revenue earlier\" disabled", html);
        Assert.Contains("form=\"dash-orders\" name=\"op\" value=\"later\" class=\"ant-btn ant-btn-text ant-btn-sm\" aria-label=\"Move Orders later\" disabled", html);
        Assert.Contains("value=\"wider\" class=\"ant-btn ant-btn-text ant-btn-sm\" aria-label=\"Make Revenue wider\">", html);
    }

    [Fact]
    public async Task TheResetForm_PostsToTheEndpoint()
    {
        var view = DashboardModel.View("home", Specs, DashboardModel.Default(Specs));

        var html = await RenderAsync(view);

        Assert.Contains("name=\"op\" value=\"reset\"", html);
        Assert.Contains("Reset layout", html);
    }

    private static int Count(string text, string needle) => (text.Length - text.Replace(needle, "", StringComparison.Ordinal).Length) / needle.Length;
}
