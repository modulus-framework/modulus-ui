using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Qr;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>One step of an <c>app-tour</c>.</summary>
[HtmlTargetElement("tour-step", ParentTag = "app-tour", TagStructure = TagStructure.WithoutEndTag)]
public class TourStepTagHelper : TagHelper
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>CSS selector of the element to highlight; unset centres the step.</summary>
    public string? Target { get; set; }

    public string? Cover { get; set; }

    /// <summary>top | bottom | left | right</summary>
    public string Placement { get; set; } = "bottom";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(TourContext), out var t) && t is TourContext tour)
            tour.Steps.Add(new TourStep(Title, Description, Target, Cover, Placement));
        output.SuppressOutput();
    }
}
