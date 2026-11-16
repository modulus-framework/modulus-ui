using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Events in time order:
/// <code>&lt;app-timeline&gt;&lt;app-timeline-item color="green" label="09:12"&gt;Paid&lt;/app-timeline-item&gt;…&lt;/app-timeline&gt;</code>
/// </summary>
[HtmlTargetElement("app-timeline")]
public class TimelineTagHelper : TagHelper
{
    /// <summary>Last item shows a spinner with this text (work in progress).</summary>
    public string? Pending { get; set; }

    public bool Reverse { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var items = new ItemList<string>();
        context.Items[typeof(TimelineTagHelper)] = items;
        await output.GetChildContentAsync();
        if (Reverse) items.Reverse();
        if (Pending is not null)
            items.Add($"""<li class="ant-timeline-item ant-timeline-item-pending"><div class="ant-timeline-item-tail"></div><div class="ant-timeline-item-head ant-timeline-item-head-custom ant-timeline-item-head-blue">{AntIcons.Span("loading", IconThemeType.Outline, "anticon-spin")}</div><div class="ant-timeline-item-content">{Html.E(Pending)}</div></li>""");

        output.TagName = "ul";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-timeline", Pending is null ? null : "ant-timeline-pending", Reverse ? "ant-timeline-reverse" : null);
        var sb = new StringBuilder();
        for (var i = 0; i < items.Count; i++)
            sb.Append(i == items.Count - 1 ? MarkLast(items[i]) : items[i]);
        output.Content.SetHtmlContent(sb.ToString());
    }

    // Every item starts with <li class="ant-timeline-item…"; tag the last one so its tail is hidden.
    private static string MarkLast(string item) => item.Insert(item.IndexOf('"') + 1, "ant-timeline-item-last ");
}
