using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Ui.Abstractions.State;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// The "Views" dropdown of a page (Odoo's favourites), the same markup as the Blazor <c>AppSavedViews</c>
/// (<see cref="SavedViewsHtml"/>): the signed-in user's saved views of the page as links, a star for the default and
/// "Save current view". It saves the current query string, so it suits every page that keeps its layout in the URL
/// (lists, grids, the pivot). The form posts to <see cref="SavedViewsHtml.Endpoint"/>: map it with
/// <c>app.MapModulusSavedViews()</c>. Do not put it inside another form. To open a page in its default view, redirect
/// to <see cref="SavedViews.DefaultUrlAsync"/> in the action.
/// <code>&lt;app-saved-views page="/tables/pivot" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-saved-views", TagStructure = TagStructure.WithoutEndTag)]
public class SavedViewsTagHelper : TagHelper
{
    /// <summary>The key the views are kept under; the request path when not set.</summary>
    public string? Page { get; set; }

    public string Id { get; set; } = "views";

    [ViewContext, HtmlAttributeNotBound] public ViewContext? ViewContext { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        var http = ViewContext?.HttpContext;
        if (http is null)
        {
            output.SuppressOutput();
            return;
        }
        var request = http.Request;
        var path = (request.PathBase + request.Path).Value ?? "/";
        var query = request.Query.SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string?>(q.Key, v))).ToList();
        var user = UiUser.Key(http.User);
        var views = http.RequestServices.GetRequiredService<SavedViews>();
        var page = string.IsNullOrEmpty(Page) ? path : Page;
        var model = new SavedViewsModel(Id, page, path, query, await views.ListAsync(user, page, http.RequestAborted), user.Length > 0,
            request.Query[SavedViewsHtml.ErrorKey].FirstOrDefault() is { Length: > 0 } error ? error[..Math.Min(error.Length, 200)] : null);
        (string, string)? token = null;
        if (model.CanSave && http.RequestServices.GetService<IAntiforgery>() is { } antiforgery)
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            if (tokens.RequestToken is not null) token = (tokens.FormFieldName, tokens.RequestToken);
        }
        output.Content.SetHtmlContent(SavedViewsHtml.Render(model, token));
    }
}
