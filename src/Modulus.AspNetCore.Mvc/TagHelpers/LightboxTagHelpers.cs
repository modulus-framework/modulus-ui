using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Markup of the lightbox that modulus-shell.js drives, the same DOM as the Blazor <c>LightboxOverlay</c>:
/// a <c>[data-m-lightbox]</c> root with <c>data-items</c>, triggers with <c>data-lightbox-index</c>, and the overlay
/// (Ant Design's image preview plus a caption and a thumbnail strip) kept hidden until the script clones it.
/// </summary>
internal static class Lightbox
{
    public static void Root(TagHelperOutput output, IEnumerable<LightboxItem> items)
    {
        output.Attributes.SetAttribute("data-m-lightbox", "");
        output.Attributes.SetAttribute("data-items", LightboxItem.Json(items));
    }

    public static string Overlay { get; } = BuildOverlay();

    private static string BuildOverlay()
    {
        static string Operation(string action, string icon, string label) =>
            $"""<li><button type="button" class="ant-image-preview-operations-operation" data-lb="{action}" aria-label="{label}" title="{label}">{AntIcons.Span(icon, cssClass: "ant-image-preview-operations-icon")}</button></li>""";

        var spinDots = string.Concat(Enumerable.Repeat("""<i class="ant-spin-dot-item"></i>""", 4));
        return
            """<div class="m-lightbox-source" hidden><div class="ant-image-preview-root m-image-preview m-lightbox-overlay">""" +
            """<div class="ant-image-preview-mask"></div>""" +
            """<div class="ant-image-preview-wrap" role="dialog" aria-modal="true" tabindex="-1">""" +
            """<div class="ant-image-preview" role="document"><div class="ant-image-preview-content"><div class="ant-image-preview-body">""" +
            """<ul class="ant-image-preview-operations">""" +
            """<li class="ant-image-preview-operations-progress" data-lb="progress" aria-live="polite"></li>""" +
            Operation("close", "close", "Close (Esc)") +
            Operation("zoom-in", "zoom-in", "Zoom in (+)") +
            Operation("zoom-out", "zoom-out", "Zoom out (-)") +
            Operation("rotate-right", "rotate-right", "Rotate right") +
            Operation("rotate-left", "rotate-left", "Rotate left") +
            $"""<li><a class="ant-image-preview-operations-operation" data-lb="download" href="#" download aria-label="Download" title="Download">{AntIcons.Span("download", cssClass: "ant-image-preview-operations-icon")}</a></li>""" +
            "</ul>" +
            """<div class="ant-image-preview-img-wrapper" data-lb="pan"><img class="ant-image-preview-img" data-lb="img" alt="" draggable="false" /></div>""" +
            $"""<button type="button" class="ant-image-preview-switch-left" data-lb="prev" aria-label="Previous image">{AntIcons.Span("left")}</button>""" +
            $"""<button type="button" class="ant-image-preview-switch-right" data-lb="next" aria-label="Next image">{AntIcons.Span("right")}</button>""" +
            $"""<div class="ant-spin ant-spin-spinning m-image-preview-spin" data-lb="spin" role="status" aria-label="Loading image" hidden><span class="ant-spin-dot ant-spin-dot-spin">{spinDots}</span></div>""" +
            """<div class="m-lightbox-footer" data-lb="footer"><div class="m-lightbox-text" data-lb="text" aria-live="polite">""" +
            """<div class="m-lightbox-title" data-lb="title"></div><div class="m-lightbox-caption" data-lb="caption"></div></div>""" +
            """<div class="m-lightbox-strip" data-lb="thumbs"></div></div>""" +
            "</div></div></div></div></div></div>";
    }
}

/// <summary>
/// Image grid that opens a full-screen lightbox: captions under each image, then in the preview a caption bar,
/// a thumbnail strip, zoom, rotate, drag, swipe, keyboard paging and download.
/// <c>&lt;app-lightbox items="Model.Photos" /&gt;</c> with <see cref="LightboxItem"/>s.
/// </summary>
[HtmlTargetElement("app-lightbox", TagStructure = TagStructure.WithoutEndTag)]
public class LightboxTagHelper : TagHelper
{
    public IEnumerable<LightboxItem> Items { get; set; } = [];

    /// <summary>Smallest tile width in pixels; the grid fits as many columns as the width allows.</summary>
    public int MinWidth { get; set; } = 180;

    /// <summary>Tile shape as a CSS aspect-ratio, e.g. "4 / 3" or "1".</summary>
    public string Aspect { get; set; } = "4 / 3";

    /// <summary>Shows the title and caption under each tile (they always show in the preview).</summary>
    public bool Captions { get; set; } = true;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var items = Items.ToList();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-lightbox");
        output.AddStyle($"--m-lightbox-min:{MinWidth}px;--m-lightbox-aspect:{Html.E(Aspect)}");
        Lightbox.Root(output, items);
        output.Content.SetHtmlContent(Render(items, Captions) + Lightbox.Overlay);
    }

    internal static string Render(IReadOnlyList<LightboxItem> items, bool captions)
    {
        var sb = new StringBuilder("""<div class="m-lightbox-grid">""");
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            sb.Append("""<figure class="m-lightbox-item">""")
              .Append($"""<button type="button" class="m-lightbox-tile" data-lightbox-index="{i}" aria-label="Open {Html.E(item.Title)}">""")
              .Append($"""<img src="{Html.E(item.Thumb ?? item.Src)}" alt="{Html.E(item.Title)}" loading="lazy" />""")
              .Append($"""<span class="m-lightbox-tile-mask" aria-hidden="true">{AntIcons.Span("zoom-in")}</span></button>""");
            if (captions && (item.Title.Length > 0 || item.Caption is not null))
            {
                sb.Append("<figcaption>");
                if (item.Title.Length > 0) sb.Append($"""<div class="m-lightbox-item-title">{Html.E(item.Title)}</div>""");
                if (item.Caption is not null) sb.Append($"""<div class="m-lightbox-item-caption">{Html.E(item.Caption)}</div>""");
                sb.Append("</figcaption>");
            }
            sb.Append("</figure>");
        }
        return sb.Append("</div>").ToString();
    }
}
