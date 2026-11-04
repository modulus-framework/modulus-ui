using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Chat;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// A conversation and its composer: the turns as a log (assistant notes under each answer), a text area for the next
/// question, and the conversation and the filter in effect as hidden fields, so a plain form posts them back. Put it in a
/// form with <c>asp-antiforgery</c>; the handler reads <c>question</c>, <c>transcript</c> and <c>filter</c>:
/// <code>
/// &lt;form method="post" asp-antiforgery="true"&gt;
///   &lt;app-chat turns="Model.Turns" filter="Model.Filter" placeholder="Try: orders over 500" /&gt;
/// &lt;/form&gt;
/// </code>
/// The same component is <c>AppChat</c> in Blazor.
/// </summary>
[HtmlTargetElement("app-chat", TagStructure = TagStructure.WithoutEndTag)]
public class ChatTagHelper : TagHelper
{
    /// <summary>The conversation so far.</summary>
    public IEnumerable<ChatTurn>? Turns { get; set; }

    /// <summary>The filter in effect, as the text of a <c>FilterGroup</c> (kept in a hidden field).</summary>
    public string? Filter { get; set; }

    /// <summary>The label of the question field.</summary>
    public string Label { get; set; } = "Your question";

    /// <summary>The hint in the question field.</summary>
    public string Placeholder { get; set; } = "";

    /// <summary>The send button's text.</summary>
    public string Button { get; set; } = "Ask";

    /// <summary>What the log says before the first question.</summary>
    public string EmptyText { get; set; } = "Ask a question to start.";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var turns = Turns?.ToList() ?? [];
        output.TagName = "section";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "m-chat");
        output.Content.SetHtmlContent(ChatHtml.Log(turns, EmptyText) + ChatHtml.Composer(ChatTranscript.ToText(turns), Filter, "question", Label, Placeholder, Button));
    }
}
