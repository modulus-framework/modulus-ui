using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Read-only details of one record:
/// <code>
/// &lt;app-descriptions title="Customer" bordered column="2"&gt;
///   &lt;app-descriptions-item label="Name"&gt;Acme&lt;/app-descriptions-item&gt;
///   &lt;app-descriptions-item label="Address" span="2"&gt;…&lt;/app-descriptions-item&gt;
/// &lt;/app-descriptions&gt;
/// </code>
/// </summary>
[HtmlTargetElement("app-descriptions")]
public class DescriptionsTagHelper : TagHelper
{
    public string? Title { get; set; }
    public bool Bordered { get; set; }
    public int Column { get; set; } = 3;

    /// <summary>default | middle | small</summary>
    public string Size { get; set; } = "default";

    /// <summary>horizontal | vertical</summary>
    public string Layout { get; set; } = "horizontal";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new DescriptionsSlots();
        context.Items[typeof(DescriptionsTagHelper)] = slots;
        await output.GetChildContentAsync();

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-descriptions", Bordered ? "ant-descriptions-bordered" : null, Size == "default" ? null : $"ant-descriptions-{Size}");

        // Fill rows left to right; the last item of a row stretches to the end, as in Ant Design.
        var rows = new List<List<(string Label, string Content, int Span)>>();
        var row = new List<(string, string, int)>();
        var used = 0;
        foreach (var item in slots.Items)
        {
            var span = Math.Min(Math.Max(1, item.Span), Column);
            if (used + span > Column && row.Count > 0) { rows.Add(row); row = []; used = 0; }
            row.Add((item.Label, item.Content, span));
            used += span;
        }
        if (row.Count > 0) rows.Add(row);

        var sb = new StringBuilder();
        if (Title is not null || slots.Extra is not null)
            sb.Append($"""<div class="ant-descriptions-header"><div class="ant-descriptions-title">{Html.E(Title)}</div>""")
              .Append(slots.Extra is null ? "" : $"""<div class="ant-descriptions-extra">{slots.Extra}</div>""").Append("</div>");
        sb.Append("""<div class="ant-descriptions-view"><table><tbody>""");
        foreach (var r in rows)
        {
            var filled = r.Sum(i => i.Span);
            if (filled < Column) r[^1] = r[^1] with { Span = r[^1].Span + Column - filled };
            if (Layout == "vertical")
            {
                sb.Append("""<tr class="ant-descriptions-row">""");
                foreach (var (label, _, span) in r)
                    sb.Append(Bordered
                        ? $"""<th class="ant-descriptions-item-label" colspan="{span}">{Html.E(label)}</th>"""
                        : $"""<th class="ant-descriptions-item" colspan="{span}"><div class="ant-descriptions-item-container"><span class="ant-descriptions-item-label">{Html.E(label)}</span></div></th>""");
                sb.Append("""</tr><tr class="ant-descriptions-row">""");
                foreach (var (_, content, span) in r)
                    sb.Append(Bordered
                        ? $"""<td class="ant-descriptions-item-content" colspan="{span}">{content}</td>"""
                        : $"""<td class="ant-descriptions-item" colspan="{span}"><div class="ant-descriptions-item-container"><span class="ant-descriptions-item-content">{content}</span></div></td>""");
                sb.Append("</tr>");
                continue;
            }
            sb.Append("""<tr class="ant-descriptions-row">""");
            foreach (var (label, content, span) in r)
                sb.Append(Bordered
                    ? $"""<th class="ant-descriptions-item-label" scope="row">{Html.E(label)}</th><td class="ant-descriptions-item-content" colspan="{span * 2 - 1}">{content}</td>"""
                    : $"""<td class="ant-descriptions-item" colspan="{span}"><div class="ant-descriptions-item-container"><span class="ant-descriptions-item-label">{Html.E(label)}</span><span class="ant-descriptions-item-content">{content}</span></div></td>""");
            sb.Append("</tr>");
        }
        output.Content.SetHtmlContent(sb.Append("</tbody></table></div>").ToString());
    }

    internal sealed class DescriptionsSlots
    {
        public List<(string Label, string Content, int Span)> Items { get; } = [];
        public string? Extra { get; set; }
    }
}
