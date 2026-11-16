using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// In-page navigation that highlights the section in view (scroll-spy):
/// <code>&lt;app-anchor&gt;&lt;app-anchor-link href="#general" title="General" /&gt;…&lt;/app-anchor&gt;</code>
/// Links can nest. <c>offset-top</c> is the distance from the viewport top at which a section counts as current.
/// </summary>
[HtmlTargetElement("app-anchor")]
public class AnchorTagHelper : TagHelper
{
    public int OffsetTop { get; set; } = 128;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var links = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-anchor-wrapper");
        output.AddStyle("max-height: 100vh;");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusAnchor({OffsetTop})"));
        output.Content.SetHtmlContent($$"""<div class="ant-anchor"><div class="ant-anchor-ink"><span class="ant-anchor-ink-ball" :class="{ visible: active }" :style="ball"></span></div>{{links}}</div>""");
    }
}
