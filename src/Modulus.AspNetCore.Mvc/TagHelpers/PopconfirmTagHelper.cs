using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Asks before an action runs (Ant Design Popconfirm). The wrapped control opens the question; OK carries the action, so
/// every attribute besides the ones below (<c>hx-delete</c>, <c>hx-target</c>, <c>form</c>, <c>formaction</c>, <c>x-on:click</c>) goes on the OK button:
/// <code>
/// &lt;app-popconfirm title="Delete this invoice?" ok-text="Delete" ok-danger hx-delete="/invoices/5" hx-target="closest tr" hx-swap="outerHTML"&gt;
///   &lt;app-button danger&gt;Delete&lt;/app-button&gt;
/// &lt;/app-popconfirm&gt;
/// </code>
/// Without an action attribute, OK only raises <c>modulus:popconfirm-ok</c> on the root. Escape and a click outside cancel.
/// </summary>
[HtmlTargetElement("app-popconfirm")]
public class PopconfirmTagHelper : TagHelper
{
    public string Title { get; set; } = "Are you sure?";
    public string? Description { get; set; }
    public string OkText { get; set; } = "OK";
    public string CancelText { get; set; } = "Cancel";
    public bool OkDanger { get; set; }

    /// <summary>top | bottom | left | right</summary>
    public string Placement { get; set; } = "top";

    /// <summary>Hides the cancel button.</summary>
    public bool HideCancel { get; set; }

    private const string OkScript = "open = false; $root.dispatchEvent(new CustomEvent('modulus:popconfirm-ok', { bubbles: true }))";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var trigger = (await output.GetChildContentAsync()).GetContent();
        var action = output.TakeAttributes();
        var titleId = $"{context.UniqueId}-title";
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-tooltip-host", "m-popover-click", "m-popconfirm");
        output.Attributes.SetAttribute("x-data", "{ open: false }");
        output.Attributes.SetAttribute(":class", "open && 'm-open'");
        output.Attributes.SetAttribute("@click.outside", "open = false");
        output.Attributes.SetAttribute("@keydown.escape", "open = false");
        var cancel = HideCancel ? "" : $"""<button type="button" class="ant-btn ant-btn-default ant-btn-sm" @click="open = false"><span>{Html.E(CancelText)}</span></button>""";
        output.Content.SetHtmlContent(
            $"""<span class="m-popover-trigger" @click="open = !open" :aria-expanded="open" aria-haspopup="dialog">{trigger}</span>""" +
            $"""<span class="ant-popover ant-popover-placement-{Html.E(Placement)} m-tooltip m-popover m-pop-{Html.E(Placement)}" role="alertdialog" aria-labelledby="{titleId}"><span class="ant-popover-content"><span class="ant-popover-arrow"><span class="ant-popover-arrow-content"></span></span><span class="ant-popover-inner" role="presentation"><span class="ant-popover-inner-content"><span class="ant-popover-message"><span class="ant-popover-message-title" id="{titleId}">{Html.E(Title)}</span>""" +
            (Description is null ? "" : $"""<span class="m-popconfirm-description">{Html.E(Description)}</span>""") +
            $"""</span><span class="ant-popover-buttons">{cancel}<button type="button" class="{Html.Cx("ant-btn", "ant-btn-primary", "ant-btn-sm", OkDanger ? "ant-btn-dangerous" : null)}"{action} @click="{OkScript}"><span>{Html.E(OkText)}</span></button></span></span></span></span></span>""");
    }
}
