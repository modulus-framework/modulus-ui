using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Query;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// The visual filter (<see cref="QueryBuilder{T}"/>) as a GET form, the MVC front end of the Blazor <c>QueryBuilder</c>:
/// <code>&lt;app-query-builder schema="@Model.Schema" model="@Model.Filter" action="/tables/query" /&gt;</code>
/// The fields are named <c>f.…</c> (<see cref="QueryBuilderForm.DefaultName"/>); read them back with
/// <see cref="QueryBuilderForm.Read"/> and apply the buttons with <see cref="QueryBuilderForm.Edit"/>.
/// </summary>
[HtmlTargetElement("app-query-builder", TagStructure = TagStructure.WithoutEndTag)]
public class QueryBuilderTagHelper : TagHelper
{
    /// <summary>The fields the builder offers, in menu order (<see cref="QueryBuilder{T}.Schema"/>).</summary>
    public IReadOnlyList<QueryField> Schema { get; set; } = [];

    /// <summary>The filter the form shows.</summary>
    public FilterGroup? Model { get; set; }

    /// <summary>The prefix of the form's field names.</summary>
    public string Name { get; set; } = QueryBuilderForm.DefaultName;

    /// <summary>The page the form submits to (empty: this page).</summary>
    public string? Action { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.Content.SetHtmlContent(QueryBuilderHtml.Render(Schema, Model ?? FilterGroup.Empty, Name, Action));
    }
}
