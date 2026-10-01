using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Pivot table in the manner of Odoo's pivot view, the same markup as the Blazor <c>AppPivotGrid</c> (<see cref="PivotHtml"/>).
/// Every control is a link to the next state, so put it in a boosted area (<c>hx-boost</c>, <c>hx-target</c>,
/// <c>hx-select</c>) and read the state back with <see cref="PivotQuery{T}.State"/>.
/// <code>&lt;app-pivot-grid result="@Model.Result" download-href="/tables/pivot/download?…" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-pivot-grid", TagStructure = TagStructure.WithoutEndTag)]
public class PivotGridTagHelper : TagHelper
{
    public PivotResult? Result { get; set; }

    /// <summary>The URL of a state; by default the current page with the state's query, its other query values kept.</summary>
    public Func<PivotState, string>? Href { get; set; }

    /// <summary>The drill-down link of a value cell, or null for none.</summary>
    [HtmlAttributeName("cell-href")] public Func<PivotCell, string?>? CellHref { get; set; }

    /// <summary>The xlsx download of the current state (<see cref="PivotXlsx"/>); no button without it.</summary>
    [HtmlAttributeName("download-href")] public string? DownloadHref { get; set; }

    public string? Id { get; set; }

    /// <summary>Accessible name of the table.</summary>
    public string? Label { get; set; }

    [ViewContext, HtmlAttributeNotBound] public ViewContext? ViewContext { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        if (Result is null)
        {
            output.SuppressOutput();
            return;
        }
        var href = Href ?? CurrentPage;
        output.Content.SetHtmlContent(PivotHtml.Render(Result, href, CellHref, DownloadHref, Id, Label));
    }

    private string CurrentPage(PivotState state)
    {
        var request = ViewContext?.HttpContext.Request;
        if (request is null) return "?" + state.ToQueryString();
        var query = request.Query.SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string?>(q.Key, v)));
        return PivotState.Link(request.PathBase + request.Path, query, state);
    }
}
