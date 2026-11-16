using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Forms;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Workflow status bar (Odoo statusbar): the stages as chevrons, done ones tinted, the current one filled. Each stage
/// is a radio, so it posts with no script; set <c>clickable="false"</c> when stages change only through actions.
/// <code>&lt;app-status-bar asp-for="Stage" stages="Model.Stages" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-status-bar", TagStructure = TagStructure.WithoutEndTag)]
public class StatusBarTagHelper : BoundControlTagHelper
{
    public IEnumerable<WorkflowStage> Stages { get; set; } = [];

    public bool Clickable { get; set; } = true;

    [HtmlAttributeName("aria-label")] public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, raw) = Bound();
        output.TakeAttributes();
        var readOnly = !Clickable || Disabled;
        var group = name.Length > 0 ? name : $"m-statusbar-{context.UniqueId}";

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-statusbar", !Clickable ? "m-statusbar-readonly" : null);
        output.Attributes.SetAttribute("role", "radiogroup");
        output.Attributes.SetAttribute("aria-label", AriaLabel ?? "Status");

        var sb = new StringBuilder();
        foreach (var stage in WorkflowStage.Visible(Stages, raw))
        {
            sb.Append($"""<label class="m-statusbar-item"><input type="radio" class="m-statusbar-radio" name="{Html.E(group)}" value="{Html.E(stage.Key)}"{Html.Attr("checked", stage.Key == raw)}{Html.Attr("disabled", readOnly)} /><span class="m-statusbar-label">{Html.E(stage.Label)}</span></label>""");
        }
        // Disabled radios do not post: keep the stage in a hidden input.
        if (readOnly && name.Length > 0) sb.Append($"""<input type="hidden" name="{Html.E(name)}" value="{Html.E(raw)}" />""");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
