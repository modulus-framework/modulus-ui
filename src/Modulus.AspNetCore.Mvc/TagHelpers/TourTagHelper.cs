using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Qr;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Guided tour: <c>&lt;app-tour name="intro"&gt;&lt;tour-step title="…" description="…" target="#save" /&gt;&lt;/app-tour&gt;</c>,
/// started by <c>$dispatch('modulus-tour-open', 'intro')</c> (or <c>open</c> to start on load). Raises
/// <c>modulus-tour-finish</c> / <c>modulus-tour-close</c> with the name.
/// </summary>
[HtmlTargetElement("app-tour")]
public class TourTagHelper : TagHelper
{
    public string Name { get; set; } = "tour";
    public bool Open { get; set; }
    public bool Mask { get; set; } = true;

    /// <summary>default | primary</summary>
    public string Type { get; set; } = "default";

    public string NextText { get; set; } = "Next";
    public string PrevText { get; set; } = "Previous";
    public string FinishText { get; set; } = "Finish";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var tour = new TourContext();
        context.Items[typeof(TourContext)] = tour;
        await output.GetChildContentAsync();
        var steps = tour.Steps.Select(s => new { title = s.Title, description = s.Description, target = s.Target, cover = s.Cover, placement = s.Placement }).ToArray();
        var id = "m-tour-" + context.UniqueId;

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusTour({Widgets.Config(new { name = Name, steps, open = Open })})"));
        output.Attributes.SetAttribute("@modulus-tour-open.window", "start($event.detail)");
        output.Attributes.SetAttribute("style", "display:contents");

        var sb = new StringBuilder();
        sb.Append("""<div class="m-tour" x-ref="root" x-show="open" x-cloak @keydown.escape="close()" @keydown.arrow-right="next()" @keydown.arrow-left="prev()">""");
        if (Mask)
            sb.Append($"""<svg class="ant-tour-mask" aria-hidden="true"><defs><mask id="{id}-mask"><rect width="100%" height="100%" fill="white" /><rect class="m-tour-hole" x="0" y="0" width="0" height="0" rx="6" fill="black" /></mask></defs><rect width="100%" height="100%" class="m-tour-shade" mask="url(#{id}-mask)" /></svg>""");
        sb.Append($"""<div class="ant-tour{(Type == "primary" ? " ant-tour-primary" : "")}" role="dialog" aria-modal="true" aria-labelledby="{id}-title" tabindex="-1" :data-target="step.target" :data-placement="step.placement">""");
        sb.Append("""<div class="ant-tour-arrow"></div><div class="ant-tour-content"><div class="ant-tour-inner">""");
        sb.Append($"""<button type="button" class="ant-tour-close" aria-label="Close" @click="close()">{AntIcons.Span("close")}</button>""");
        sb.Append("""<template x-if="step.cover"><div class="ant-tour-cover"><img :src="step.cover" alt="" /></div></template>""");
        sb.Append($"""<div class="ant-tour-header"><div class="ant-tour-title" id="{id}-title" x-text="step.title"></div></div>""");
        sb.Append("""<div class="ant-tour-description" x-show="step.description" x-text="step.description"></div>""");
        sb.Append("""<div class="ant-tour-footer"><div class="ant-tour-indicators" :aria-label="'Step ' + (current + 1) + ' of ' + steps.length">""");
        sb.Append("""<template x-for="(s, i) in steps" :key="i"><span class="ant-tour-indicator" :class="i === current && 'ant-tour-indicator-active'"></span></template></div>""");
        sb.Append($"""<div class="ant-tour-buttons"><button type="button" class="ant-btn ant-btn-default ant-btn-sm ant-tour-prev-btn" x-show="current > 0" @click="prev()"><span>{Html.E(PrevText)}</span></button>""");
        sb.Append($"""<button type="button" class="ant-btn ant-btn-primary ant-btn-sm ant-tour-next-btn" @click="next()"><span x-text="last ? '{Html.E(FinishText.Replace("'", "\\'", StringComparison.Ordinal))}' : '{Html.E(NextText.Replace("'", "\\'", StringComparison.Ordinal))}'">{Html.E(NextText)}</span></button></div>""");
        sb.Append("</div></div></div></div></div>");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
