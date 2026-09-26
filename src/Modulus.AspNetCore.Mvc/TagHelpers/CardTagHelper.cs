using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Ant Design card. Optional children: <c>&lt;app-card-extra&gt;</c> (header, right side), <c>&lt;app-card-cover&gt;</c>
/// (above the body), <c>&lt;app-card-action&gt;</c> (one per action in the footer bar) and <c>&lt;app-card-meta&gt;</c>.
/// <code>&lt;app-card title="Sales"&gt;&lt;app-card-extra&gt;…&lt;/app-card-extra&gt;…&lt;/app-card&gt;</code>
/// </summary>
[HtmlTargetElement("app-card")]
public class CardTagHelper : TagHelper
{
    public string? Title { get; set; }

    /// <summary>Borderless cards get the elevated Modulus shadow (Ant Design Pro default).</summary>
    public bool Bordered { get; set; }

    public bool Hoverable { get; set; }

    /// <summary>Inline style for the body (e.g. <c>padding: 0</c>).</summary>
    public string? BodyStyle { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new CardSlots();
        context.Items[typeof(CardSlots)] = slots;
        var body = (await output.GetChildContentAsync()).GetContent();

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-card", Bordered ? "ant-card-bordered" : null, Hoverable ? "ant-card-hoverable" : null);

        var head = "";
        if (Title is not null || slots.Extra is not null)
        {
            var extra = slots.Extra is null ? "" : $"""<div class="ant-card-extra">{slots.Extra}</div>""";
            head = $"""<div class="ant-card-head"><div class="ant-card-head-wrapper"><div class="ant-card-head-title">{Html.E(Title)}</div>{extra}</div></div>""";
        }
        var cover = slots.Cover is null ? "" : $"""<div class="ant-card-cover">{slots.Cover}</div>""";
        var width = slots.Actions.Count == 0 ? "" : $"{100 / slots.Actions.Count}%";
        var actions = slots.Actions.Count == 0 ? "" :
            $"""<ul class="ant-card-actions">{string.Concat(slots.Actions.Select(a => $"<li style=\"width:{width}\"><span>{a}</span></li>"))}</ul>""";
        var style = BodyStyle is null ? "" : $""" style="{Html.E(BodyStyle)}" """;
        output.Content.SetHtmlContent($"""{head}{cover}<div class="ant-card-body"{style}>{body}</div>{actions}""");
    }
}

internal sealed class CardSlots
{
    public string? Extra { get; set; }
    public string? Cover { get; set; }
    public List<string> Actions { get; } = [];
}

[HtmlTargetElement("app-card-extra", ParentTag = "app-card")]
public class CardExtraTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(CardSlots), out var slots))
            ((CardSlots)slots).Extra = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}

/// <summary>Content above the card body, usually an image: <c>&lt;app-card-cover&gt;&lt;img …/&gt;&lt;/app-card-cover&gt;</c>.</summary>
[HtmlTargetElement("app-card-cover", ParentTag = "app-card")]
public class CardCoverTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(CardSlots), out var slots))
            ((CardSlots)slots).Cover = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}

/// <summary>One action in the card's footer bar: <c>&lt;app-card-action&gt;&lt;app-icon type="like" /&gt;&lt;/app-card-action&gt;</c>.</summary>
[HtmlTargetElement("app-card-action", ParentTag = "app-card")]
public class CardActionTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var body = (await output.GetChildContentAsync()).GetContent();
        if (context.Items.TryGetValue(typeof(CardSlots), out var slots)) ((CardSlots)slots).Actions.Add(body);
        output.SuppressOutput();
    }
}

/// <summary>
/// Avatar, title and description inside a card body:
/// <c>&lt;app-card-meta title="Alpine lake" description="Shared by …" avatar="SP" avatar-color="#1677ff" /&gt;</c>.
/// </summary>
[HtmlTargetElement("app-card-meta", TagStructure = TagStructure.WithoutEndTag)]
public class CardMetaTagHelper : TagHelper
{
    public string? Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Initials (or text) for the avatar.</summary>
    public string? Avatar { get; set; }
    public string? AvatarColor { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-card-meta");
        var avatar = Avatar is null ? "" :
            $"""<div class="ant-card-meta-avatar"><span class="ant-avatar"{(AvatarColor is null ? "" : $" style=\"background: {Html.E(AvatarColor)};\"")}><span class="ant-avatar-string" style="transform: scale(1) translateX(-50%);">{Html.E(Avatar)}</span></span></div>""";
        output.Content.SetHtmlContent(avatar +
            $"""<div class="ant-card-meta-detail">{(Title is null ? "" : $"<div class=\"ant-card-meta-title\">{Html.E(Title)}</div>")}{(Description is null ? "" : $"<div class=\"ant-card-meta-description\">{Html.E(Description)}</div>")}</div>""");
    }
}
