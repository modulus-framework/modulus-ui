using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Ant Design tag. <c>color</c> accepts presets (blue, geekblue, cyan, purple…), statuses
/// (success, processing, error, warning, default) or any CSS colour.
/// </summary>
[HtmlTargetElement("app-tag")]
public class TagTagHelper : TagHelper
{
    private static readonly HashSet<string> Presets =
    [
        "magenta", "red", "volcano", "orange", "gold", "lime", "green", "cyan", "blue", "geekblue", "purple",
        "success", "processing", "error", "warning", "default",
    ];

    public string? Color { get; set; }

    /// <summary>Icon before the text.</summary>
    public string? Icon { get; set; }

    /// <summary>Shows a close button that removes the tag (client-side).</summary>
    public bool Closable { get; set; }

    /// <summary>Borderless tag.</summary>
    public bool Bordered { get; set; } = true;

    internal static bool IsPreset(string color) => Presets.Contains(color);

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        if (Icon is not null) output.PreContent.SetHtmlContent(AntIcons.Span(Icon));
        if (Closable)
        {
            output.Attributes.SetAttribute("x-data", "{ show: true }");
            output.Attributes.SetAttribute("x-show", "show");
            output.PostContent.SetHtmlContent($"""<button type="button" class="ant-tag-close-icon m-tag-close" aria-label="Remove" @click="show = false">{AntIcons.Span("close")}</button>""");
        }
        if (!Bordered) output.AddClasses("ant-tag-borderless");
        if (Color is null || Color == "default")
        {
            output.AddClasses("ant-tag");
        }
        else if (Presets.Contains(Color))
        {
            output.AddClasses("ant-tag", $"ant-tag-{Color}");
        }
        else
        {
            output.AddClasses("ant-tag", "ant-tag-has-color");
            output.AddStyle($"background-color:{Color}");
        }
    }
}

/// <summary>
/// Badge. Three forms, as in Ant Design:
/// <code>
/// &lt;app-badge status="success" text="Active" /&gt;                        status dot with text
/// &lt;app-badge count="5"&gt;&lt;app-avatar icon="user" shape="square" /&gt;&lt;/app-badge&gt;  count on a child
/// &lt;app-badge dot&gt;&lt;app-icon type="bell" /&gt;&lt;/app-badge&gt;                   red dot on a child
/// </code>
/// </summary>
[HtmlTargetElement("app-badge")]
public class BadgeTagHelper : TagHelper
{
    /// <summary>success | processing | default | error | warning</summary>
    public string? Status { get; set; }

    public string? Text { get; set; }

    public int? Count { get; set; }
    public int OverflowCount { get; set; } = 99;
    public bool ShowZero { get; set; }
    public bool Dot { get; set; }

    /// <summary>small | default</summary>
    public string Size { get; set; } = "default";

    /// <summary>Preset (blue, green, …) or CSS colour for the count/dot.</summary>
    public string? Color { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var child = (await output.GetChildContentAsync()).GetContent();
        var wrapper = !string.IsNullOrWhiteSpace(child);
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;

        if (Status is not null || (Color is not null && Count is null && !Dot))
        {
            var preset = Color is not null && TagTagHelper.IsPreset(Color);
            var dotClass = Html.Cx("ant-badge-status-dot", Status is null ? null : $"ant-badge-status-{Html.E(Status)}", preset ? $"ant-badge-status-{Html.E(Color)}" : null);
            var dotStyle = Color is not null && !preset ? $" style=\"background:{Html.E(Color)}\"" : "";
            output.AddClasses("ant-badge", "ant-badge-status", "ant-badge-not-a-wrapper");
            output.Content.SetHtmlContent(
                $"""<span class="{dotClass}"{dotStyle}></span><span class="ant-badge-status-text">{Html.E(Text)}</span>""");
            return;
        }

        output.AddClasses("ant-badge", wrapper ? null : "ant-badge-not-a-wrapper");
        var colorClass = Color is not null && TagTagHelper.IsPreset(Color) ? $"ant-badge-color-{Color}" : null;
        var style = Color is not null && colorClass is null ? $" style=\"background:{Html.E(Color)}\"" : "";
        string sup;
        if (Dot)
        {
            sup = $"""<sup class="{Html.Cx("ant-scroll-number", "ant-badge-dot", colorClass)}"{style}></sup>""";
        }
        else if (Count is { } n && (n > 0 || ShowZero))
        {
            var text = n > OverflowCount ? $"{OverflowCount}+" : n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            sup = $"""<sup class="{Html.Cx("ant-scroll-number", "ant-badge-count", Size == "small" ? "ant-badge-count-sm" : null, text.Length > 1 ? "ant-badge-multiple-words" : null, colorClass)}" title="{n}"{style}><span class="ant-scroll-number-only"><span class="ant-scroll-number-only-unit current">{text}</span></span></sup>""";
        }
        else
        {
            sup = "";
        }
        output.Content.SetHtmlContent(child + sup);
    }
}

/// <summary>
/// Ribbon on a card or box: <c>&lt;app-badge-ribbon text="Recommended" color="purple"&gt;&lt;app-card&gt;…&lt;/app-card&gt;&lt;/app-badge-ribbon&gt;</c>.
/// </summary>
[HtmlTargetElement("app-badge-ribbon")]
public class BadgeRibbonTagHelper : TagHelper
{
    public string? Text { get; set; }

    /// <summary>Preset or CSS colour.</summary>
    public string? Color { get; set; }

    /// <summary>start | end</summary>
    public string Placement { get; set; } = "end";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var preset = Color is not null && TagTagHelper.IsPreset(Color);
        var cls = Html.Cx("ant-ribbon", $"ant-ribbon-placement-{Html.E(Placement)}", preset ? $"ant-ribbon-color-{Html.E(Color)}" : null);
        var style = Color is not null && !preset ? $" style=\"background:{Html.E(Color)}\"" : "";
        var corner = Color is not null && !preset ? $" style=\"color:{Html.E(Color)}\"" : "";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-ribbon-wrapper");
        output.PostContent.SetHtmlContent($"""<div class="{cls}"{style}><span class="ant-ribbon-text">{Html.E(Text)}</span><div class="ant-ribbon-corner"{corner}></div></div>""");
    }
}

/// <summary>Avatar with an image or up to two initials.</summary>
[HtmlTargetElement("app-avatar", TagStructure = TagStructure.WithoutEndTag)]
public class AvatarTagHelper : TagHelper
{
    public string? Src { get; set; }

    /// <summary>Name to derive initials from when there is no image.</summary>
    public string? Name { get; set; }

    /// <summary>small | default | large</summary>
    public string Size { get; set; } = "default";

    /// <summary>Background colour for initials.</summary>
    public string? Color { get; set; }

    /// <summary>circle | square</summary>
    public string Shape { get; set; } = "circle";

    /// <summary>Icon instead of initials.</summary>
    public string? Icon { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-avatar", $"ant-avatar-{Shape}", Size switch { "small" => "ant-avatar-sm", "large" => "ant-avatar-lg", _ => null }, Src is null ? null : "ant-avatar-image", Icon is null ? null : "ant-avatar-icon");
        if (Icon is not null && Src is null)
        {
            if (Color is not null) output.AddStyle($"background:{Color};flex:none");
            output.Content.SetHtmlContent(AntIcons.Span(Icon));
            return;
        }
        if (Src is not null)
        {
            output.Content.SetHtmlContent($"""<img src="{Html.E(Src)}" alt="{Html.E(Name)}" />""");
            return;
        }
        if (Color is not null) output.AddStyle($"background:{Color};flex:none");
        var initials = string.Concat((Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
        output.Content.SetHtmlContent($"""<span class="ant-avatar-string">{Html.E(initials)}</span>""");
    }
}

/// <summary>Change indicator: <c>&lt;app-trend direction="up"&gt;Weekly 12%&lt;/app-trend&gt;</c>.</summary>
[HtmlTargetElement("app-trend")]
public class TrendTagHelper : TagHelper
{
    /// <summary>up | down</summary>
    public string Direction { get; set; } = "up";

    /// <summary>Treat "up" as bad (cost, churn).</summary>
    public bool Inverse { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var up = Direction == "up";
        var content = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-trend", up ? "m-trend-up" : "m-trend-down", Inverse ? "m-trend-inverse" : null);
        output.Content.SetHtmlContent(
            content + AntIcons.Span(up ? "caret-up" : "caret-down", AntDesign.IconThemeType.Fill) +
            $"""<span class="m-sr-only">{(up ? "increase" : "decrease")}</span>""");
    }
}
