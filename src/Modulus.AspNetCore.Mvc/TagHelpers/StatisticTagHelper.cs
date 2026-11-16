using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Headline number: <c>&lt;app-statistic title="Active users" value="112893" /&gt;</c>,
/// <c>precision="1" suffix="%" prefix-icon="arrow-up" value-color="var(--m-success)"</c>.
/// </summary>
[HtmlTargetElement("app-statistic", TagStructure = TagStructure.WithoutEndTag)]
public class StatisticTagHelper : TagHelper
{
    public string? Title { get; set; }

    /// <summary>A number (formatted with group separators) or any text.</summary>
    public string? Value { get; set; }

    public int? Precision { get; set; }
    public string? Prefix { get; set; }
    public string? PrefixIcon { get; set; }
    public string? Suffix { get; set; }
    public string? ValueColor { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        string inner;
        if (decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            var text = Precision is { } p ? number.ToString($"N{p}", CultureInfo.CurrentCulture) : number.ToString("#,0.##########", CultureInfo.CurrentCulture);
            var sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            var i = text.IndexOf(sep, StringComparison.Ordinal);
            inner = i < 0
                ? $"""<span class="ant-statistic-content-value-int">{Html.E(text)}</span>"""
                : $"""<span class="ant-statistic-content-value-int">{Html.E(text[..i])}</span><span class="ant-statistic-content-value-decimal">{Html.E(text[i..])}</span>""";
        }
        else
        {
            inner = Html.E(Value);
        }
        var prefix = PrefixIcon is not null ? AntIcons.Span(PrefixIcon) : Prefix is not null ? Html.E(Prefix) : null;
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-statistic");
        output.Content.SetHtmlContent(
            (Title is null ? "" : $"""<div class="ant-statistic-title">{Html.E(Title)}</div>""") +
            $"""<div class="ant-statistic-content"{(ValueColor is null ? "" : $" style=\"color:{Html.E(ValueColor)}\"")}>""" +
            (prefix is null ? "" : $"""<span class="ant-statistic-content-prefix">{prefix}</span>""") +
            $"""<span class="ant-statistic-content-value">{inner}</span>""" +
            (Suffix is null ? "" : $"""<span class="ant-statistic-content-suffix">{Html.E(Suffix)}</span>""") + "</div>");
    }
}
