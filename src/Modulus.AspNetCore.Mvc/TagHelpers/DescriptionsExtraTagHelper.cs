using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

[HtmlTargetElement("descriptions-extra", ParentTag = "app-descriptions")]
public class DescriptionsExtraTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(DescriptionsTagHelper), out var slots))
            ((DescriptionsTagHelper.DescriptionsSlots)slots).Extra = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}
