using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Chat;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// An approval waiting for a person: what it is, its details, and the Approve and Reject buttons. The buttons post
/// <c>approval</c> as "id:approve" or "id:reject" to the form named <see cref="Form"/>. The same card is <c>AppApprovalCard</c> in Blazor.
/// </summary>
[HtmlTargetElement("app-approval-card", TagStructure = TagStructure.WithoutEndTag)]
public class ApprovalCardTagHelper : TagHelper
{
    /// <summary>The approval to show (an <c>ApprovalRequest</c>).</summary>
    public Modulus.Ui.Abstractions.Assistant.ApprovalRequest? Request { get; set; }

    /// <summary>The id of the form the buttons submit.</summary>
    public string? Form { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent(Request is null ? "" : ApprovalHtml.Card(Request, Form));
    }
}
