using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Image with a click-to-zoom preview: <c>&lt;app-image src="/img/product.jpg" alt="Desk lamp" width="160" /&gt;</c>.
/// </summary>
[HtmlTargetElement("app-image", TagStructure = TagStructure.WithoutEndTag)]
public class ImageTagHelper : TagHelper
{
    public string Src { get; set; } = "";
    public string Alt { get; set; } = "";
    public int? Width { get; set; }
    public int? Height { get; set; }
    public bool Preview { get; set; } = true;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var size = (Width is null ? "" : $" width=\"{Width}\"") + (Height is null ? "" : $" height=\"{Height}\"");
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-image", "m-image");
        if (Width is not null) output.AddStyle($"width:{Width}px");
        var img = $"""<img class="ant-image-img" src="{Html.E(Src)}" alt="{Html.E(Alt)}"{size} />""";
        if (!Preview)
        {
            output.Content.SetHtmlContent(img);
            return;
        }
        // Inside <app-image-group> the group owns the preview (and pages through its images);
        // on its own the image is a group of one, with the same preview minus the paging.
        var group = context.Items.TryGetValue(typeof(ImageGroupContext), out var g) ? g as ImageGroupContext : null;
        var grouped = group is not null;
        group ??= new ImageGroupContext();
        var index = group.Images.Count;
        group.Images.Add(new LightboxItem(Src, Alt));
        var mask = $"""<button type="button" class="ant-image-mask m-image-mask" data-lightbox-index="{index}" aria-label="Preview {Html.E(Alt)}"><span class="ant-image-mask-info">{AntIcons.Span("eye")} Preview</span></button>""";
        if (grouped)
        {
            output.Content.SetHtmlContent(img + mask);
            return;
        }
        Lightbox.Root(output, group.Images);
        output.Content.SetHtmlContent(img + mask + Lightbox.Overlay);
    }
}
