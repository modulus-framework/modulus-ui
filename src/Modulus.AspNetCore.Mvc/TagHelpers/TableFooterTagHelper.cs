using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>Content under an <c>app-table</c>, inside its chrome (the pager goes here).</summary>
[HtmlTargetElement("table-footer", ParentTag = "app-table")]
public class TableFooterTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(TableTagHelper), out var slots))
            ((Dictionary<string, string>)slots)["table-footer"] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}
