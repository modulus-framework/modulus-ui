using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Divider: <c>&lt;app-divider /&gt;</c>, <c>&lt;app-divider orientation="left"&gt;Billing&lt;/app-divider&gt;</c>,
/// <c>&lt;app-divider type="vertical" /&gt;</c>.
/// </summary>
[HtmlTargetElement("app-divider")]
public class DividerTagHelper : TagHelper
{
    /// <summary>horizontal | vertical</summary>
    public string Type { get; set; } = "horizontal";

    /// <summary>left | center | right (text position)</summary>
    public string Orientation { get; set; } = "center";

    public bool Dashed { get; set; }

    /// <summary>Regular-weight text instead of a heading.</summary>
    public bool Plain { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var text = (await output.GetChildContentAsync()).GetContent();
        var hasText = !string.IsNullOrWhiteSpace(text) && Type != "vertical";
        output.TagName = Type == "vertical" ? "span" : "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("role", "separator");
        output.AddClasses("ant-divider", $"ant-divider-{Type}",
            hasText ? "ant-divider-with-text" : null,
            hasText && Orientation != "center" ? $"ant-divider-with-text-{Orientation}" : null,
            hasText && Orientation == "center" ? "ant-divider-with-text-center" : null,
            Dashed ? "ant-divider-dashed" : null, Plain && hasText ? "ant-divider-plain" : null);
        output.Content.SetHtmlContent(hasText ? $"""<span class="ant-divider-inner-text">{text}</span>""" : "");
    }
}

/// <summary>
/// Evenly spaced inline items: <c>&lt;app-space size="middle" wrap&gt;…&lt;/app-space&gt;</c>.
/// Children are laid out directly (flex gap), so any element can be an item.
/// </summary>
[HtmlTargetElement("app-space")]
public class SpaceTagHelper : TagHelper
{
    /// <summary>horizontal | vertical</summary>
    public string Direction { get; set; } = "horizontal";

    /// <summary>small (8) | middle (16) | large (24) | a number of pixels</summary>
    public string Size { get; set; } = "small";

    /// <summary>start | end | center | baseline</summary>
    public string? Align { get; set; }

    public bool Wrap { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var gap = Gap(Size);
        var align = Align ?? (Direction == "horizontal" ? "center" : null);
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-space", $"ant-space-{Direction}", align is null ? null : $"ant-space-align-{align}", "m-space");
        output.AddStyle($"column-gap:{gap}px;row-gap:{gap}px{(Wrap ? ";flex-wrap:wrap" : "")}");
    }

    internal static int Gap(string size) => size switch
    {
        "small" => 8,
        "middle" => 16,
        "large" => 24,
        _ => int.TryParse(size, out var px) ? px : 8,
    };
}

/// <summary>
/// 24-column grid row: <c>&lt;app-row gutter="16"&gt;&lt;app-col span="8"&gt;…&lt;/app-col&gt;&lt;/app-row&gt;</c>.
/// </summary>
[HtmlTargetElement("app-row")]
public class RowTagHelper : TagHelper
{
    /// <summary>Horizontal gutter in pixels.</summary>
    public int Gutter { get; set; }

    /// <summary>Vertical gap between wrapped rows in pixels.</summary>
    public int VerticalGutter { get; set; }

    /// <summary>start | end | center | space-around | space-between | space-evenly</summary>
    public string? Justify { get; set; }

    /// <summary>top | middle | bottom</summary>
    public string? Align { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        context.Items[typeof(RowTagHelper)] = Gutter;
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-row", Justify is null ? null : $"ant-row-{Justify}", Align is null ? null : $"ant-row-{Align}");
        var half = Gutter / 2.0;
        var style = (Gutter > 0 ? $"margin-left:-{half}px;margin-right:-{half}px;" : "") + (VerticalGutter > 0 ? $"row-gap:{VerticalGutter}px" : "");
        if (style.Length > 0) output.AddStyle(style);
    }
}

/// <summary>Grid column. Responsive spans: <c>&lt;app-col xs="24" md="12" xl="8"&gt;</c>.</summary>
[HtmlTargetElement("app-col")]
public class ColTagHelper : TagHelper
{
    public int? Span { get; set; }
    public int? Offset { get; set; }
    public int? Push { get; set; }
    public int? Pull { get; set; }
    [HtmlAttributeName("order")] public int? ColOrder { get; set; }

    /// <summary>CSS flex value, e.g. <c>auto</c> or <c>1</c> or <c>200px</c>.</summary>
    public string? Flex { get; set; }

    public int? Xs { get; set; }
    public int? Sm { get; set; }
    public int? Md { get; set; }
    public int? Lg { get; set; }
    public int? Xl { get; set; }
    public int? Xxl { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-col",
            Span is { } s ? $"ant-col-{s}" : null,
            Offset is { } o ? $"ant-col-offset-{o}" : null,
            Push is { } p ? $"ant-col-push-{p}" : null,
            Pull is { } pl ? $"ant-col-pull-{pl}" : null,
            ColOrder is { } or ? $"ant-col-order-{or}" : null,
            Xs is { } xs ? $"ant-col-xs-{xs}" : null,
            Sm is { } sm ? $"ant-col-sm-{sm}" : null,
            Md is { } md ? $"ant-col-md-{md}" : null,
            Lg is { } lg ? $"ant-col-lg-{lg}" : null,
            Xl is { } xl ? $"ant-col-xl-{xl}" : null,
            Xxl is { } xxl ? $"ant-col-xxl-{xxl}" : null);
        var gutter = context.Items.TryGetValue(typeof(RowTagHelper), out var g) ? (int)g : 0;
        var style = (gutter > 0 ? $"padding-left:{gutter / 2.0}px;padding-right:{gutter / 2.0}px;" : "") + (Flex is null ? "" : $"flex:{FlexValue(Flex)}");
        if (style.Length > 0) output.AddStyle(style);
    }

    private static string FlexValue(string flex) =>
        int.TryParse(flex, out var n) ? $"{n} {n} auto" : flex.EndsWith("px") || flex.EndsWith('%') ? $"0 0 {flex}" : flex;
}

/// <summary>Flex container: <c>&lt;app-flex gap="middle" justify="space-between" align="center"&gt;</c>.</summary>
[HtmlTargetElement("app-flex")]
public class FlexTagHelper : TagHelper
{
    public bool Vertical { get; set; }
    public bool Wrap { get; set; }

    /// <summary>small | middle | large | pixels</summary>
    public string? Gap { get; set; }

    /// <summary>Any CSS justify-content value.</summary>
    public string? Justify { get; set; }

    /// <summary>Any CSS align-items value.</summary>
    public string? Align { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-flex", Vertical ? "ant-flex-vertical" : null, Wrap ? "ant-flex-wrap-wrap" : null);
        var style = "display:flex;" + (Vertical ? "flex-direction:column;" : "") + (Wrap ? "flex-wrap:wrap;" : "") +
                    (Gap is null ? "" : $"gap:{SpaceTagHelper.Gap(Gap)}px;") +
                    (Justify is null ? "" : $"justify-content:{Justify};") + (Align is null ? "" : $"align-items:{Align};");
        output.AddStyle(style);
    }
}
