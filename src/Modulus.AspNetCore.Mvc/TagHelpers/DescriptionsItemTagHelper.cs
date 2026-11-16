using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

[HtmlTargetElement("app-descriptions-item", ParentTag = "app-descriptions")]
public class DescriptionsItemTagHelper : TagHelper
{
    public string Label { get; set; } = "";
    public int Span { get; set; } = 1;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = (await output.GetChildContentAsync()).GetContent();
        if (context.Items.TryGetValue(typeof(DescriptionsTagHelper), out var slots))
            ((DescriptionsTagHelper.DescriptionsSlots)slots).Items.Add((Label, content, Span));
        output.SuppressOutput();
    }
}
