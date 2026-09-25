using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// TagHelper for antd Card: &lt;app-card title="Title"&gt;...&lt;/app-card&gt;
/// </summary>
[HtmlTargetElement("app-card")]
public class AppCardTagHelper : TagHelper
{
    [HtmlAttributeName("title")]
    public string? Title { get; set; }

    [HtmlAttributeName("bordered")]
    public bool Bordered { get; set; } = true;

    [HtmlAttributeName("hoverable")]
    public bool Hoverable { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;

        var classes = new List<string> { "ant-card" };
        if (Bordered)
            classes.Add("ant-card-bordered");
        if (Hoverable)
            classes.Add("ant-card-hoverable");

        output.Attributes.SetAttribute("class", string.Join(" ", classes));
        output.Attributes.SetAttribute("style", "margin-bottom: 16px;");

        if (!string.IsNullOrEmpty(Title))
        {
            var titleHtml = $"""<div class="ant-card-head"><div class="ant-card-head-title">{Title}</div></div>""";
            output.PreContent.AppendHtml(titleHtml);
        }

        output.PreContent.AppendHtml("""<div class="ant-card-body">""");
        output.PostContent.AppendHtml("""</div>""");
    }
}
