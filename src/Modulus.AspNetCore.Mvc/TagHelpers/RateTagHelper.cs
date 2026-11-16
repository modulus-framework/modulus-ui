using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>Star rating that posts a number: <c>&lt;app-rate asp-for="Rating" count="5" allow-clear /&gt;</c>.</summary>
[HtmlTargetElement("app-rate", TagStructure = TagStructure.WithoutEndTag)]
public class RateTagHelper : BoundControlTagHelper
{
    public int Count { get; set; } = 5;

    /// <summary>Clicking the current value again clears it.</summary>
    public bool AllowClear { get; set; } = true;

    /// <summary>Accessible names per star, e.g. "Terrible,Bad,Normal,Good,Wonderful".</summary>
    public string? Tooltips { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, raw) = Bound();
        var value = (int)Math.Round(decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0);
        var tips = Tooltips?.Split(',', StringSplitOptions.TrimEntries) ?? [];
        var star = AntIcons.Span("star", IconThemeType.Fill);
        output.TakeAttributes();
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-rate");
        output.Attributes.SetAttribute("x-data", $"{{ v: {value}, h: 0 }}");
        var sb = new StringBuilder($"""<ul class="{Html.Cx("ant-rate", Disabled ? "ant-rate-disabled" : null)}" role="radiogroup" @mouseleave="h = 0">""");
        for (var i = 1; i <= Count; i++)
        {
            var label = tips.ElementAtOrDefault(i - 1) ?? $"{i} star{(i == 1 ? "" : "s")}";
            var click = Disabled ? "" : $""" @click="v = {(AllowClear ? $"v === {i} ? 0 : {i}" : i.ToString(CultureInfo.InvariantCulture))}" @mouseenter="h = {i}" @keydown.enter.prevent="v = {i}" @keydown.space.prevent="v = {i}" """;
            sb.Append($"""<li class="ant-rate-star {(value >= i ? "ant-rate-star-full" : "ant-rate-star-zero")}" :class="(h || v) >= {i} ? 'ant-rate-star-full' : 'ant-rate-star-zero'">""")
              .Append($"""<div role="radio" aria-label="{Html.E(label)}" aria-checked="{(value == i ? "true" : "false")}" :aria-checked="v === {i}" aria-posinset="{i}" aria-setsize="{Count}" tabindex="{(Disabled ? -1 : 0)}"{click.TrimEnd()}>""")
              .Append($"""<div class="ant-rate-star-first">{star}</div><div class="ant-rate-star-second">{star}</div></div></li>""");
        }
        sb.Append("</ul>");
        if (name.Length > 0) sb.Append($"""<input type="hidden" name="{Html.E(name)}" value="{value}" :value="v" />""");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
