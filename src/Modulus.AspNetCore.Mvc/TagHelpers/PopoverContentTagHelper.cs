using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

[HtmlTargetElement("popover-content", ParentTag = "app-popover")]
public class PopoverContentTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(PopoverTagHelper), out var slots))
            ((Dictionary<string, string>)slots)["popover-content"] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}
