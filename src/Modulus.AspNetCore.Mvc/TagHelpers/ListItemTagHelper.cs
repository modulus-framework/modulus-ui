using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

[HtmlTargetElement("app-list-item")]
public class ListItemTagHelper : TagHelper
{
    public string? Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Makes the title a link.</summary>
    public string? Href { get; set; }

    public string? AvatarSrc { get; set; }
    public string? AvatarName { get; set; }
    public string? AvatarIcon { get; set; }
    public string? AvatarColor { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new Dictionary<string, string>();
        context.Items[typeof(ListItemTagHelper)] = slots;
        var content = (await output.GetChildContentAsync()).GetContent();

        string? avatar = null;
        if (AvatarSrc is not null || AvatarName is not null || AvatarIcon is not null)
        {
            var style = AvatarColor is null ? "" : $" style=\"background:{Html.E(AvatarColor)}\"";
            avatar = AvatarSrc is not null
                ? $"""<span class="ant-avatar ant-avatar-circle ant-avatar-image"><img src="{Html.E(AvatarSrc)}" alt="" /></span>"""
                : AvatarIcon is not null
                    ? $"""<span class="ant-avatar ant-avatar-circle ant-avatar-icon"{style}>{AntIcons.Span(AvatarIcon)}</span>"""
                    : $"""<span class="ant-avatar ant-avatar-circle"{style}><span class="ant-avatar-string">{Html.E(Initials(AvatarName!))}</span></span>""";
        }
        var title = Title is null ? null : Href is null ? Html.E(Title) : $"""<a href="{Html.E(Href)}">{Html.E(Title)}</a>""";
        var meta = title is null && Description is null && avatar is null ? "" :
            """<div class="ant-list-item-meta">""" +
            (avatar is null ? "" : $"""<div class="ant-list-item-meta-avatar">{avatar}</div>""") +
            """<div class="ant-list-item-meta-content">""" +
            (title is null ? "" : $"""<h4 class="ant-list-item-meta-title">{title}</h4>""") +
            (Description is null ? "" : $"""<div class="ant-list-item-meta-description">{Html.E(Description)}</div>""") + "</div></div>";
        var actions = slots.TryGetValue("list-item-actions", out var a) ? $"""<ul class="ant-list-item-action"><li>{a}</li></ul>""" : "";

        output.TagName = "li";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-list-item");
        output.Content.SetHtmlContent(meta + content + actions);
    }

    internal static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
}
