using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Ant Design card. Optional <c>&lt;app-card-extra&gt;</c> child renders in the header's right side.
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
        var style = BodyStyle is null ? "" : $""" style="{Html.E(BodyStyle)}" """;
        output.Content.SetHtmlContent($"""{head}<div class="ant-card-body"{style}>{body}</div>""");
    }
}

internal sealed class CardSlots
{
    public string? Extra { get; set; }
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
