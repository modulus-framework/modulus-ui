using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Plugins;
using SortDirection = Modulus.Ui.Abstractions.SortDirection;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

public abstract class GridSlotTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(EditableGridTagHelper), out var slots))
            ((Dictionary<string, string>)slots)[context.TagName] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}
