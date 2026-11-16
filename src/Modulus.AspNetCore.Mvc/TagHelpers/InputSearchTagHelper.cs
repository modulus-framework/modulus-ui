using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Search box with a search button: <c>&lt;app-input-search name="q" placeholder="Search" enter-button /&gt;</c>.
/// The button submits the enclosing form (or triggers htmx on the form).
/// </summary>
[HtmlTargetElement("app-input-search", TagStructure = TagStructure.WithoutEndTag)]
public class InputSearchTagHelper : BoundControlTagHelper
{
    /// <summary>Primary search button (true), a button with this text, or an icon-only default button (false).</summary>
    public string? EnterButton { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var attrs = ControlAttributes(output);
        var primary = EnterButton is not null && EnterButton != "false";
        var text = primary && EnterButton is not ("" or "true") ? $"<span>{Html.E(EnterButton)}</span>" : null;
        var button = $"""<button type="submit" aria-label="Search" class="{Html.Cx("ant-btn", primary ? "ant-btn-primary" : "ant-btn-default", SizeClass("ant-btn"), text is null ? "ant-btn-icon-only" : null, "ant-input-search-button")}">{AntIcons.Span("search")}{text}</button>""";
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-input-group-wrapper", "ant-input-search", primary ? "ant-input-search-enter-button" : null, SizeClass("ant-input-search"), SizeClass("ant-input-group-wrapper"));
        output.Content.SetHtmlContent(
            $"""<span class="ant-input-wrapper ant-input-group"><input type="search" class="{Html.Cx("ant-input", SizeClass("ant-input"), StatusClass("ant-input"))}"{attrs} /><span class="ant-input-group-addon">{button}</span></span>""");
    }
}
