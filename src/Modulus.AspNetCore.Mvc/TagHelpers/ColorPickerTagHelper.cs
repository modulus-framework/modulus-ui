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
