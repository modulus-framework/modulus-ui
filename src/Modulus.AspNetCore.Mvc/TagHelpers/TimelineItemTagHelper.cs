using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

[HtmlTargetElement("app-timeline-item", ParentTag = "app-timeline")]
public class TimelineItemTagHelper : TagHelper
{
    private static readonly HashSet<string> Presets = ["blue", "red", "green", "gray"];

    /// <summary>blue | red | green | gray | any CSS colour</summary>
    public string Color { get; set; } = "blue";

    /// <summary>Icon instead of the dot.</summary>
    public string? Icon { get; set; }

    /// <summary>Text shown before the item (time, date).</summary>
    public string? Label { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = (await output.GetChildContentAsync()).GetContent();
        var preset = Presets.Contains(Color);
        var head = Html.Cx("ant-timeline-item-head", preset ? $"ant-timeline-item-head-{Color}" : null, Icon is null ? null : "ant-timeline-item-head-custom");
        var style = preset ? "" : $" style=\"border-color:{Html.E(Color)};color:{Html.E(Color)}\"";
        var label = Label is null ? "" : $"""<div class="ant-timeline-item-label m-timeline-label">{Html.E(Label)}</div>""";
        var html = $"""<li class="ant-timeline-item">{label}<div class="ant-timeline-item-tail"></div><div class="{head}"{style}>{(Icon is null ? "" : AntIcons.Span(Icon))}</div><div class="ant-timeline-item-content">{content}</div></li>""";
        if (context.Items.TryGetValue(typeof(TimelineTagHelper), out var items))
            ((ItemList<string>)items).Add(html);
        output.SuppressOutput();
    }
}
