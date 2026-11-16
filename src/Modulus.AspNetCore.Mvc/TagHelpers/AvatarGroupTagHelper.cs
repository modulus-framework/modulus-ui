using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>Overlapping avatars: <c>&lt;app-avatar-group max-count="3"&gt;&lt;app-avatar name="…" /&gt;…&lt;/app-avatar-group&gt;</c>.</summary>
[HtmlTargetElement("app-avatar-group")]
public partial class AvatarGroupTagHelper : TagHelper
{
    /// <summary>Show at most this many avatars, then a "+N" avatar.</summary>
    public int? MaxCount { get; set; }

    /// <summary>small | default | large</summary>
    public string Size { get; set; } = "default";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var html = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-avatar-group");
        var avatars = AvatarSplit().Split(html).Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
        if (MaxCount is { } max && avatars.Count > max)
        {
            var sizeClass = Size switch { "small" => " ant-avatar-sm", "large" => " ant-avatar-lg", _ => "" };
            html = string.Concat(avatars.Take(max)) +
                   $"""<span class="ant-avatar ant-avatar-circle{sizeClass} m-avatar-more"><span class="ant-avatar-string">+{avatars.Count - max}</span></span>""";
        }
        output.Content.SetHtmlContent(html);
    }

    // Each <app-avatar> renders one top-level <span class="ant-avatar …">…</span>; split before each one.
    [GeneratedRegex(@"(?=<span class=""ant-avatar )")]
    private static partial Regex AvatarSplit();
}
