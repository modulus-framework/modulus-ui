using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>A link of an <c>app-anchor</c>; child links nest under it.</summary>
[HtmlTargetElement("app-anchor-link")]
public class AnchorLinkTagHelper : TagHelper
{
    public string Href { get; set; } = "";
    public string Title { get; set; } = "";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var children = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-anchor-link");
        output.Attributes.SetAttribute(":class", new HtmlString("{ 'ant-anchor-link-active': active === $el.dataset.href }"));
        output.Attributes.SetAttribute("data-href", Href);
        output.Content.SetHtmlContent($$"""<a class="ant-anchor-link-title" :class="{ 'ant-anchor-link-title-active': active === $el.parentElement.dataset.href }" href="{{Html.E(Href)}}" title="{{Html.E(Title)}}">{{Html.E(Title)}}</a>{{children}}""");
    }
}
