using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>Heading: <c>&lt;app-title level="3"&gt;Quarterly report&lt;/app-title&gt;</c>.</summary>
[HtmlTargetElement("app-title")]
public class TitleTagHelper : TagHelper
{
    /// <summary>1–5</summary>
    public int Level { get; set; } = 1;

    /// <summary>secondary | success | warning | danger</summary>
    public string? Type { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = $"h{Math.Clamp(Level, 1, 5)}";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-typography", Type is null ? null : $"ant-typography-{Type}");
    }
}

/// <summary>
/// Inline text with Ant Design's semantic colours and marks:
/// <c>&lt;app-text type="secondary" strong&gt;…&lt;/app-text&gt;</c>.
/// </summary>
[HtmlTargetElement("app-text")]
public class TextTagHelper : TagHelper
{
    /// <summary>secondary | success | warning | danger</summary>
    public string? Type { get; set; }

    public bool Strong { get; set; }
    public bool Code { get; set; }
    public bool Mark { get; set; }
    public bool Underline { get; set; }
    public bool Delete { get; set; }
    public bool Italic { get; set; }
    public bool Keyboard { get; set; }
    public bool Disabled { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = (await output.GetChildContentAsync()).GetContent();
        // Same nesting order as Ant Design's Typography.
        if (Strong) content = $"<strong>{content}</strong>";
        if (Underline) content = $"<u>{content}</u>";
        if (Delete) content = $"<del>{content}</del>";
        if (Code) content = $"<code>{content}</code>";
        if (Mark) content = $"<mark>{content}</mark>";
        if (Keyboard) content = $"<kbd>{content}</kbd>";
        if (Italic) content = $"<i>{content}</i>";

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-typography", Type is null ? null : $"ant-typography-{Type}", Disabled ? "ant-typography-disabled" : null);
        output.Content.SetHtmlContent(content);
    }
}

/// <summary>Paragraph: <c>&lt;app-paragraph&gt;…&lt;/app-paragraph&gt;</c>. <c>ellipsis</c> clamps to <c>rows</c> lines.</summary>
[HtmlTargetElement("app-paragraph")]
public class ParagraphTagHelper : TagHelper
{
    /// <summary>secondary | success | warning | danger</summary>
    public string? Type { get; set; }

    public bool Ellipsis { get; set; }
    public int Rows { get; set; } = 1;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-typography", Type is null ? null : $"ant-typography-{Type}",
            Ellipsis ? (Rows > 1 ? "ant-typography-ellipsis ant-typography-ellipsis-multiple-line" : "ant-typography-ellipsis ant-typography-ellipsis-single-line") : null);
        if (Ellipsis && Rows > 1) output.AddStyle($"-webkit-line-clamp:{Rows}");
    }
}

/// <summary>Text link: <c>&lt;app-link href="…"&gt;Docs&lt;/app-link&gt;</c>.</summary>
[HtmlTargetElement("app-link")]
public class LinkTagHelper : TagHelper
{
    /// <summary>secondary | success | warning | danger</summary>
    public string? Type { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "a";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-typography", Type is null ? null : $"ant-typography-{Type}");
    }
}
