using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Printing;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// A sheet of labels (<see cref="LabelHtml"/>), one sheet per page when printed:
/// <code>&lt;app-label-sheet items="@Model.Labels" layout="@Model.Layout" /&gt;</code>
/// The layout defaults to the common A4 sheet of 21 labels (<see cref="LabelLayout.A4ThreeBySeven"/>).
/// </summary>
[HtmlTargetElement("app-label-sheet", TagStructure = TagStructure.WithoutEndTag)]
public class LabelSheetTagHelper : TagHelper
{
    /// <summary>The labels, in print order.</summary>
    public IReadOnlyList<LabelItem> Items { get; set; } = [];

    /// <summary>The sheet the labels print on.</summary>
    public LabelLayout Layout { get; set; } = LabelLayout.A4ThreeBySeven;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.Content.SetHtmlContent(LabelHtml.Render(Items, Layout));
    }
}

/// <summary>
/// A table for a printed page (<see cref="PrintTableHtml"/>): its header repeats on every page:
/// <code>&lt;app-print-table columns="@Model.Columns" rows="@Model.Rows" total="@Model.Total" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-print-table", TagStructure = TagStructure.WithoutEndTag)]
public class PrintTableTagHelper : TagHelper
{
    /// <summary>The columns, in order.</summary>
    public IReadOnlyList<PrintColumn> Columns { get; set; } = [];

    /// <summary>The rows; each cell is text.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; set; } = [];

    /// <summary>The total row under the last row, if the table has one.</summary>
    public IReadOnlyList<string>? Total { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.Content.SetHtmlContent(PrintTableHtml.Render(Columns, Rows, Total));
    }
}
