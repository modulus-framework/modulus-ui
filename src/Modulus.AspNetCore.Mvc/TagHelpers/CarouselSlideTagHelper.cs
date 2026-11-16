using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>One slide of an <c>app-carousel</c>.</summary>
[HtmlTargetElement("carousel-slide", ParentTag = "app-carousel")]
public class CarouselSlideTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(CarouselTagHelper), out var slides))
            ((List<string>)slides).Add((await output.GetChildContentAsync()).GetContent());
        output.SuppressOutput();
    }
}
