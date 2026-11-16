using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Carousel of <c>&lt;carousel-slide&gt;</c> children with dots, arrow keys and autoplay (paused on hover):
/// <code>&lt;app-carousel autoplay="5000"&gt;&lt;carousel-slide&gt;…&lt;/carousel-slide&gt;&lt;/app-carousel&gt;</code>
/// </summary>
[HtmlTargetElement("app-carousel")]
public class CarouselTagHelper : TagHelper
{
    /// <summary>Milliseconds between slides; 0 turns autoplay off.</summary>
    public int Autoplay { get; set; }

    public bool Dots { get; set; } = true;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slides = new List<string>();
        context.Items[typeof(CarouselTagHelper)] = slides;
        await output.GetChildContentAsync();
        var n = Math.Max(1, slides.Count);
        var sb = new StringBuilder($"""<div class="slick-slider slick-initialized" dir="ltr"><div class="slick-list"><div class="slick-track" style="width: {n * 100}%; transform: translate3d(0%, 0px, 0px);" :style="trackStyle()">""");
        for (var i = 0; i < slides.Count; i++)
            sb.Append($$"""<div data-index="{{i}}" class="{{Html.Cx("slick-slide", i == 0 ? "slick-active slick-current" : null)}}" :class="{ 'slick-active slick-current': index === {{i}} }" tabindex="-1" aria-hidden="{{(i == 0 ? "false" : "true")}}" :aria-hidden="index !== {{i}}" style="outline: none; width: {{100.0 / n:0.####}}%;"><div><div tabindex="-1" style="width: 100%; display: inline-block;">{{slides[i]}}</div></div></div>""");
        sb.Append("</div></div>");
        if (Dots && slides.Count > 1)
        {
            sb.Append("""<ul class="slick-dots slick-dots-bottom" style="display: block;">""");
            for (var i = 0; i < slides.Count; i++)
                sb.Append($$"""<li class="{{(i == 0 ? "slick-active" : "")}}" :class="{ 'slick-active': index === {{i}} }"><button type="button" aria-label="Slide {{i + 1}}" @click="go({{i}})">{{i + 1}}</button></li>""");
            sb.Append("</ul>");
        }
        sb.Append("</div>");
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-carousel");
        output.Attributes.SetAttribute("role", "region");
        output.Attributes.SetAttribute("aria-roledescription", "carousel");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusCarousel({slides.Count}, {Autoplay})"));
        output.Attributes.SetAttribute("@mouseenter", "pause = true");
        output.Attributes.SetAttribute("@mouseleave", "pause = false");
        output.Attributes.SetAttribute("@keydown.left", "go(index - 1)");
        output.Attributes.SetAttribute("@keydown.right", "go(index + 1)");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
