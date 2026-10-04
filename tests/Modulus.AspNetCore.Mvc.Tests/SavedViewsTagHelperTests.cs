using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.State;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>&lt;app-saved-views&gt;: the same markup as the Blazor AppSavedViews (SavedViewsHtml), for the request's user.</summary>
public class SavedViewsTagHelperTests
{
    private static async Task<(string Html, SavedViews Views)> RenderAsync(string query, ClaimsPrincipal user, Func<SavedViews, Task>? seed = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<IUiStateStore, MemoryUiStateStore>()
            .AddScoped<SavedViews>()
            .BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services, User = user };
        http.Request.Path = "/tables/pivot";
        http.Request.QueryString = new QueryString(query);
        var views = services.GetRequiredService<SavedViews>();
        if (seed is not null) await seed(views);

        var view = new ViewContext { HttpContext = http, RouteData = new RouteData(), ActionDescriptor = new ActionDescriptor() };
        var helper = new SavedViewsTagHelper { Page = "/p", Id = "pv", ViewContext = view };
        var output = new TagHelperOutput("app-saved-views", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-saved-views", [], new Dictionary<object, object>(), "x"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return (writer.ToString(), views);
    }

    private static ClaimsPrincipal Ana => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana")], "test"));

    [Fact]
    public async Task SavedViews_IsTheSharedMarkup_ForTheUsersViews_WithTheError()
    {
        var (html, views) = await RenderAsync("?rows=city&views-error=Name+the+view.", Ana,
            v => v.SaveAsync("ana", "/p", "Cities", SavedViews.Parse("rows=city")));

        var model = new SavedViewsModel("pv", "/p", "/tables/pivot", SavedViews.Parse("rows=city&views-error=Name the view."),
            await views.ListAsync("ana", "/p"), CanSave: true, Error: "Name the view.");
        Assert.Equal(SavedViewsHtml.Render(model), html);
        Assert.Contains("aria-current=\"true\"", html);
        Assert.Contains("name=\"query\" value=\"rows=city\"", html);
    }

    [Fact]
    public async Task SavedViews_ForAnonymous_ListsNothing_AndOffersNoSave()
    {
        var (html, _) = await RenderAsync("", new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Contains("Sign in to save views.", html);
        Assert.DoesNotContain("m-views-save", html);
    }
}
