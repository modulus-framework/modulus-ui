using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

[HtmlTargetElement("list-item-actions", ParentTag = "app-list-item")]
public class ListItemActionsTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(ListItemTagHelper), out var slots))
            ((Dictionary<string, string>)slots)["list-item-actions"] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}
