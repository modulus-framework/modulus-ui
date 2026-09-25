using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// TagHelper for antd Table: &lt;app-table striped="true" bordered="true"&gt;...&lt;/app-table&gt;
/// </summary>
[HtmlTargetElement("app-table")]
public class AppTableTagHelper : TagHelper
{
    [HtmlAttributeName("striped")]
    public bool Striped { get; set; }

    [HtmlAttributeName("bordered")]
    public bool Bordered { get; set; } = true;

    [HtmlAttributeName("size")]
    public string Size { get; set; } = "middle"; // small, middle, large

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "table";
        output.TagMode = TagMode.StartTagAndEndTag;

        var classes = new List<string> { "ant-table" };
        if (Striped)
            classes.Add("ant-table-striped");
        if (Bordered)
            classes.Add("ant-table-bordered");

        if (Size == "small")
            classes.Add("ant-table-small");
        else if (Size == "large")
            classes.Add("ant-table-large");

        output.Attributes.SetAttribute("class", string.Join(" ", classes));
        output.Attributes.SetAttribute("style", "width: 100%;");
    }
}

/// <summary>
/// TableHeadRow: &lt;app-table-head&gt;&lt;th&gt;Name&lt;/th&gt;...&lt;/app-table-head&gt;
/// </summary>
[HtmlTargetElement("app-table-head")]
public class AppTableHeadTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "thead";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.PreContent.AppendHtml("""<tr>""");
        output.PostContent.AppendHtml("""</tr>""");
    }
}

/// <summary>
/// TableBodyRows: &lt;app-table-body&gt;...&lt;/app-table-body&gt;
/// </summary>
[HtmlTargetElement("app-table-body")]
public class AppTableBodyTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "tbody";
        output.TagMode = TagMode.StartTagAndEndTag;
    }
}
