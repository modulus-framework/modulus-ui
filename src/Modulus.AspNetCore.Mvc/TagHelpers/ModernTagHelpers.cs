using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Qr;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// Ant Design 5 components that AntD Blazor 1.6.2 lacks: QRCode, ColorPicker, FloatButton and Tour. The Blazor
// versions live in Modulus.Blazor/Components/Extras; both emit the antd v5 DOM styled by modulus.css.
// ColorPicker and Tour run on modulusColorPicker / modulusTour in modulus-widgets.js; back-to-top and the tour
// geometry come from modulus-shell.js, which Blazor shares.

/// <summary>
/// QR code drawn as inline SVG on the server: <c>&lt;app-qr-code value="https://…" size="160" /&gt;</c>.
/// <c>status="expired"</c> shows a Refresh button over the code: it links to <c>refresh-href</c> when set (the server
/// issues a new code), otherwise it clears the overlay and raises <c>modulus-qr-refresh</c> with the value.
/// </summary>
[HtmlTargetElement("app-qr-code", TagStructure = TagStructure.WithoutEndTag)]
public class QRCodeTagHelper : TagHelper
{
    public string Value { get; set; } = "";
    public int Size { get; set; } = 160;
    public string Color { get; set; } = "#000";
    public string BgColor { get; set; } = "transparent";

    [HtmlAttributeName("error-level")]
    public QrErrorLevel ErrorLevel { get; set; } = QrErrorLevel.M;

    public string? Icon { get; set; }
    public int IconSize { get; set; } = 40;
    public bool Bordered { get; set; } = true;
    public QRCodeStatus Status { get; set; }
    public string? RefreshHref { get; set; }
    public string ExpiredText { get; set; } = "QR code expired";
    public string RefreshText { get; set; } = "Refresh";
    public string ScannedText { get; set; } = "Scanned";

    [HtmlAttributeName("aria-label")]
    public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (cls, style) = (output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null,
            output.Attributes.TryGetAttribute("style", out var s) ? s.Value?.ToString() : null);
        output.Attributes.RemoveAll("class");
        output.Attributes.RemoveAll("style");
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", QRCodeMarkup.CssClass(Bordered, cls));
        output.Attributes.SetAttribute("style", QRCodeMarkup.CssStyle(Size, BgColor, style));

        var html = new StringBuilder(QRCodeMarkup.Svg(Value, Size, Color, BgColor, ErrorLevel, Icon, IconSize, Bordered, AriaLabel));
        var clientRefresh = Status == QRCodeStatus.Expired && RefreshHref is null;
        if (clientRefresh) output.Attributes.SetAttribute("x-data", new HtmlString("{ expired: true }"));
        if (Status != QRCodeStatus.Active)
        {
            html.Append(clientRefresh ? "<div class=\"ant-qrcode-mask\" x-show=\"expired\">" : "<div class=\"ant-qrcode-mask\">");
            html.Append(Status switch
            {
                QRCodeStatus.Loading => """<div class="ant-spin ant-spin-spinning" role="status" aria-label="Loading"><span class="ant-spin-dot ant-spin-dot-spin"><i class="ant-spin-dot-item"></i><i class="ant-spin-dot-item"></i><i class="ant-spin-dot-item"></i><i class="ant-spin-dot-item"></i></span></div>""",
                QRCodeStatus.Expired => $"""<p class="ant-qrcode-expired">{Html.E(ExpiredText)}</p>""" +
                    (RefreshHref is null
                        ? $"""<button type="button" class="ant-btn ant-btn-link" @click="expired = false; $dispatch('modulus-qr-refresh', {Html.E(System.Text.Json.JsonSerializer.Serialize(Value))})">{AntIcons.Span("reload")}<span>{Html.E(RefreshText)}</span></button>"""
                        : $"""<a class="ant-btn ant-btn-link" href="{Html.E(RefreshHref)}">{AntIcons.Span("reload")}<span>{Html.E(RefreshText)}</span></a>"""),
                _ => $"""<p class="ant-qrcode-scanned">{Html.E(ScannedText)}</p>""",
            });
            html.Append("</div>");
        }
        output.Content.SetHtmlContent(html.ToString());
    }
}

/// <summary>
/// Colour picker bound to a string: <c>&lt;app-color-picker asp-for="Brand" show-text allow-clear /&gt;</c>. Posts
/// #rrggbb (or #rrggbbaa with alpha, or rgb()/rgba() with <c>format="rgb"</c>); an empty string when cleared.
/// </summary>
[HtmlTargetElement("app-color-picker", TagStructure = TagStructure.WithoutEndTag)]
public class ColorPickerTagHelper : BoundControlTagHelper
{
    /// <summary>hex | rgb</summary>
    public string Format { get; set; } = "hex";

    public bool DisabledAlpha { get; set; }
    public bool AllowClear { get; set; }
    public bool ShowText { get; set; }

    /// <summary>Swatch rows; null shows <see cref="ColorPreset.Recommended"/>, an empty list shows none.</summary>
    public IEnumerable<ColorPreset>? Presets { get; set; }

    [HtmlAttributeName("aria-label")]
    public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, value) = Bound();
        var extra = output.TakeAttributes();
        var cleared = !HsvColor.TryParse(value, out var color);
        if (cleared) color = HsvColor.Parse(null);
        if (DisabledAlpha) color = color with { A = 1 };
        var fmt = Format == "rgb" ? "rgb" : "hex";
        var label = AriaLabel ?? "Pick a colour";
        var inv = CultureInfo.InvariantCulture;

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-color-picker");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusColorPicker({Widgets.Config(new { value = cleared ? "" : color.Format(fmt), format = fmt, alpha = !DisabledAlpha })})"));
        output.Attributes.SetAttribute("@click.outside", "open = false");
        output.Attributes.SetAttribute("@keydown.escape", "open = false");

        var sb = new StringBuilder();
        sb.Append(inv, $"""<input type="hidden" x-ref="input" name="{Html.E(name)}" value="{Html.E(cleared ? "" : color.Format(fmt))}" :value="value"{extra} />""");
        var trigger = Html.Cx("ant-color-picker-trigger", SizeClass("ant-color-picker"), Disabled ? "ant-color-picker-trigger-disabled" : null);
        sb.Append(inv, $"""<div class="{trigger}"{Html.Attr("id", id.Length > 0 ? id : null)} tabindex="{(Disabled ? -1 : 0)}" role="button" aria-haspopup="dialog" :aria-expanded="open" aria-label="{Html.E(label)}"{(Disabled ? " aria-disabled=\"true\"" : "")}""");
        sb.Append(Disabled ? ">" : """ :class="open && 'ant-color-picker-trigger-active'" @click="open = !open" @keydown.enter.prevent="open = !open" @keydown.space.prevent="open = !open">""");
        sb.Append(inv, $"""<div class="ant-color-picker-color-block"><div class="ant-color-picker-clear" x-show="cleared"{(cleared ? "" : " style=\"display:none\"")}></div>""");
        sb.Append(inv, $"""<div class="ant-color-picker-color-block-inner" x-show="!cleared" style="background:{Html.E(color.ToRgbString())}{(cleared ? ";display:none" : "")}" :style="'background:' + css"></div></div>""");
        if (ShowText) sb.Append(inv, $"""<div class="ant-color-picker-trigger-text" x-text="cleared ? '' : text">{Html.E(cleared ? "" : color.Format(fmt))}</div>""");
        sb.Append("</div>");

        if (!Disabled)
        {
            sb.Append(inv, $"""<div class="ant-popover ant-color-picker ant-popover-placement-bottomLeft m-color-picker-popup" x-show="open" x-cloak x-transition.opacity><div class="ant-popover-content"><div class="ant-popover-inner" role="dialog" aria-label="{Html.E(AriaLabel ?? "Colour picker")}"><div class="ant-popover-inner-content"><div class="ant-color-picker-inner-content">""");
            if (AllowClear)
                sb.Append("""<div class="m-color-picker-header"><button type="button" class="ant-color-picker-clear m-color-picker-clear-btn" title="Clear" aria-label="Clear colour" @click="clear()"></button></div>""");
            sb.Append("""<div class="ant-color-picker-panel"><div class="ant-color-picker-palette" :style="'background-color:' + hueCss">""");
            sb.Append("""<div class="ant-color-picker-select" tabindex="0" role="slider" aria-label="Saturation and brightness" x-ref="palette" :aria-valuetext="'Saturation ' + Math.round(s * 100) + '%, brightness ' + Math.round(v * 100) + '%'" @pointerdown="down('palette', $event)" @pointermove="move($event)" @pointerup="up()" @keydown="key('palette', $event)">""");
            sb.Append("""<div class="ant-color-picker-saturation"></div><div class="ant-color-picker-handler" :style="'left:' + s * 100 + '%;top:' + (1 - v) * 100 + '%;background-color:' + text"></div></div></div>""");
            sb.Append("""<div class="ant-color-picker-slider-container"><div class="ant-color-picker-slider-group">""");
            sb.Append("""<div class="ant-color-picker-slider ant-color-picker-slider-hue" tabindex="0" role="slider" aria-label="Hue" aria-valuemin="0" aria-valuemax="360" :aria-valuenow="Math.round(h)" x-ref="hue" @pointerdown="down('hue', $event)" @pointermove="move($event)" @pointerup="up()" @keydown="key('hue', $event)">""");
            sb.Append("""<div class="ant-color-picker-handler ant-color-picker-handler-sm" :style="'left:' + h / 3.6 + '%;background-color:' + hueCss"></div></div>""");
            if (!DisabledAlpha)
            {
                sb.Append("""<div class="ant-color-picker-slider ant-color-picker-slider-alpha" tabindex="0" role="slider" aria-label="Alpha" aria-valuemin="0" aria-valuemax="100" :aria-valuenow="pct" x-ref="alpha" @pointerdown="down('alpha', $event)" @pointermove="move($event)" @pointerup="up()" @keydown="key('alpha', $event)">""");
                sb.Append("""<div class="ant-color-picker-slider-track" :style="'background:' + alphaTrack"></div><div class="ant-color-picker-handler ant-color-picker-handler-sm" :style="'left:' + a * 100 + '%;background-color:' + css"></div></div>""");
            }
            sb.Append("""</div><div class="ant-color-picker-color-block"><div class="ant-color-picker-color-block-inner" :style="'background:' + css"></div></div></div>""");
            sb.Append("""<div class="ant-color-picker-input-container"><select class="m-color-picker-format" aria-label="Colour format" x-model="fmt"><option value="hex">HEX</option><option value="rgb">RGB</option></select>""");
            sb.Append("""<div class="ant-color-picker-input"><input class="ant-input ant-input-sm" spellcheck="false" :aria-label="fmt === 'rgb' ? 'RGB value' : 'Hex value'" :value="text" @change="typed($event)" /></div>""");
            if (!DisabledAlpha)
                sb.Append("""<div class="ant-color-picker-alpha-input"><input class="ant-input ant-input-sm" type="number" min="0" max="100" aria-label="Alpha percent" :value="pct" @change="typedAlpha($event)" /><span class="m-color-picker-suffix">%</span></div>""");
            sb.Append("</div></div>");

            foreach (var preset in Presets ?? [ColorPreset.Recommended])
            {
                sb.Append(inv, $"""<div class="ant-color-picker-presets"><div class="ant-color-picker-presets-label">{Html.E(preset.Label)}</div><div class="ant-color-picker-presets-items">""");
                foreach (var swatch in preset.Colors)
                {
                    var c = HsvColor.Parse(swatch);
                    var bright = c.V > 0.85 && c.S < 0.15 ? " ant-color-picker-presets-color-bright" : "";
                    var hex = c.ToHex();
                    sb.Append(inv, $"""<button type="button" class="ant-color-picker-presets-color{bright}" style="--m-swatch:{Html.E(swatch)}" title="{Html.E(swatch)}" aria-label="{Html.E(swatch)}" :class="is('{hex}') && 'ant-color-picker-presets-color-checked'" :aria-pressed="is('{hex}')" @click="pick('{hex}')"></button>""");
                }
                sb.Append("</div></div>");
            }
            sb.Append("</div></div></div></div></div>");
        }
        output.Content.SetHtmlContent(sb.ToString());
    }
}

/// <summary>Shared markup of the float buttons.</summary>
internal static class FloatButtons
{
    public static string Classes(string type, string shape) =>
        $"ant-float-btn ant-float-btn-{(type == "primary" ? "primary" : "default")} ant-float-btn-{(shape == "square" ? "square" : "circle")}";

    public static string Body(string? icon, string? description, string? tooltip, int? badgeCount, bool badgeDot)
    {
        var sb = new StringBuilder("""<div class="ant-float-btn-body"><div class="ant-float-btn-content">""");
        if (icon is not null || description is null) sb.Append($"""<div class="ant-float-btn-icon">{AntIcons.Span(icon ?? "file-text")}</div>""");
        if (description is not null) sb.Append($"""<div class="ant-float-btn-description">{Html.E(description)}</div>""");
        sb.Append("</div></div>");
        if (badgeDot) sb.Append("""<sup class="ant-scroll-number ant-badge-dot m-float-btn-badge"></sup>""");
        else if (badgeCount > 0) sb.Append($"""<sup class="ant-scroll-number ant-badge-count m-float-btn-badge">{(badgeCount > 99 ? "99+" : badgeCount.Value.ToString(CultureInfo.InvariantCulture))}</sup>""");
        if (tooltip is not null) sb.Append($"""<span class="m-float-btn-tip" aria-hidden="true">{Html.E(tooltip)}</span>""");
        return sb.ToString();
    }
}

/// <summary>
/// Button fixed to the bottom right: <c>&lt;app-float-button icon="question-circle" tooltip="Help" /&gt;</c>. A link
/// with <c>href</c>. Other attributes (<c>@click</c>, <c>hx-*</c>…) stay on the button.
/// </summary>
[HtmlTargetElement("app-float-button", TagStructure = TagStructure.WithoutEndTag)]
public class FloatButtonTagHelper : TagHelper
{
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? Tooltip { get; set; }

    /// <summary>default | primary</summary>
    public string Type { get; set; } = "default";

    /// <summary>circle | square</summary>
    public string Shape { get; set; } = "circle";

    public string? Href { get; set; }
    public int? BadgeCount { get; set; }
    public bool BadgeDot { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagMode = TagMode.StartTagAndEndTag;
        if (Href is null)
        {
            output.TagName = "button";
            if (!output.Attributes.ContainsName("type")) output.Attributes.SetAttribute("type", "button");
        }
        else
        {
            output.TagName = "a";
            output.Attributes.SetAttribute("href", Href);
        }
        output.AddClasses(FloatButtons.Classes(Type, Shape));
        if (!output.Attributes.ContainsName("aria-label") && (Tooltip ?? Description) is { } label)
            output.Attributes.SetAttribute("aria-label", label);
        output.Content.SetHtmlContent(FloatButtons.Body(Icon, Description, Tooltip, BadgeCount, BadgeDot));
    }
}

/// <summary>
/// A stack of float buttons: <c>&lt;app-float-button-group trigger="click"&gt;&lt;app-float-button …/&gt;&lt;/app-float-button-group&gt;</c>.
/// With <c>trigger</c> (click | hover) the children fold behind a toggle button.
/// </summary>
[HtmlTargetElement("app-float-button-group")]
public class FloatButtonGroupTagHelper : TagHelper
{
    public string? Trigger { get; set; }
    public bool Open { get; set; }
    public string Shape { get; set; } = "circle";
    public string Type { get; set; } = "default";
    public string Icon { get; set; } = "plus";
    public string CloseIcon { get; set; } = "close";
    public string? Description { get; set; }
    public string? Tooltip { get; set; }

    [HtmlAttributeName("aria-label")]
    public string? AriaLabel { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var body = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        var square = Shape == "square";
        output.AddClasses("ant-float-btn-group", square ? "ant-float-btn-group-square" : "ant-float-btn-group-circle",
            square && Trigger is null ? "ant-float-btn-group-square-shadow" : null);
        if (Trigger is null)
        {
            output.Content.SetHtmlContent(body);
            return;
        }

        output.Attributes.SetAttribute("x-data", new HtmlString($"{{ open: {(Open ? "true" : "false")} }}"));
        output.Attributes.SetAttribute(":class", "open && 'ant-float-btn-group-open'");
        if (Trigger == "hover")
        {
            output.Attributes.SetAttribute("@mouseenter", "open = true");
            output.Attributes.SetAttribute("@mouseleave", "open = false");
        }
        var label = AriaLabel ?? Tooltip ?? "Actions";
        output.Content.SetHtmlContent(
            $"""<div class="ant-float-btn-group-wrap" x-show="open"{(Open ? "" : " x-cloak")}>{body}</div>""" +
            $"""<button type="button" class="{FloatButtons.Classes(Type, Shape)}" aria-label="{Html.E(label)}" :aria-expanded="open" @click="open = !open">""" +
            """<div class="ant-float-btn-body"><div class="ant-float-btn-content">""" +
            $"""<div class="ant-float-btn-icon"><span x-show="!open">{AntIcons.Span(Icon)}</span><span x-show="open" x-cloak>{AntIcons.Span(CloseIcon)}</span></div>""" +
            (Description is null ? "" : $"""<div class="ant-float-btn-description" x-show="!open">{Html.E(Description)}</div>""") +
            "</div></div>" +
            (Tooltip is null ? "" : $"""<span class="m-float-btn-tip" aria-hidden="true">{Html.E(Tooltip)}</span>""") + "</button>");
    }
}

/// <summary>
/// Back-to-top float button, shown once the page (or <c>target</c>) has scrolled <c>visibility-height</c> px:
/// <c>&lt;app-float-back-top /&gt;</c>. Behaviour comes from modulus-shell.js.
/// </summary>
[HtmlTargetElement("app-float-back-top", TagStructure = TagStructure.WithoutEndTag)]
public class FloatBackTopTagHelper : TagHelper
{
    public int VisibilityHeight { get; set; } = 400;

    /// <summary>CSS selector of the scrolling element; the page when unset.</summary>
    public string? Target { get; set; }

    public string Type { get; set; } = "default";
    public string Shape { get; set; } = "circle";
    public string? Description { get; set; }
    public string? Tooltip { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "button";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "button");
        output.AddClasses(FloatButtons.Classes(Type, Shape), "m-float-back-top");
        output.Attributes.SetAttribute("data-m-back-top", VisibilityHeight.ToString(CultureInfo.InvariantCulture));
        if (Target is not null) output.Attributes.SetAttribute("data-m-target", Target);
        if (!output.Attributes.ContainsName("aria-label")) output.Attributes.SetAttribute("aria-label", Tooltip ?? "Back to top");
        output.Content.SetHtmlContent(FloatButtons.Body("vertical-align-top", Description, Tooltip, null, false));
    }
}

internal sealed class TourContext
{
    public List<TourStep> Steps { get; } = [];
}

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

/// <summary>One step of an <c>app-tour</c>.</summary>
[HtmlTargetElement("tour-step", ParentTag = "app-tour", TagStructure = TagStructure.WithoutEndTag)]
public class TourStepTagHelper : TagHelper
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>CSS selector of the element to highlight; unset centres the step.</summary>
    public string? Target { get; set; }

    public string? Cover { get; set; }

    /// <summary>top | bottom | left | right</summary>
    public string Placement { get; set; } = "bottom";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(TourContext), out var t) && t is TourContext tour)
            tour.Steps.Add(new TourStep(Title, Description, Target, Cover, Placement));
        output.SuppressOutput();
    }
}
