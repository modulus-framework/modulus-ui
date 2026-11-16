using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Text input with Ant Design's affixes and addons.
/// <code>
/// &lt;app-input asp-for="Name" placeholder="Name" prefix-icon="user" allow-clear /&gt;
/// &lt;app-input asp-for="Website" addon-before="https://" addon-after=".com" /&gt;
/// &lt;app-input asp-for="Password" type="password" /&gt;              (with show/hide toggle)
/// &lt;app-input name="city" items="Model.Cities" /&gt;                  (AutoComplete via &lt;datalist&gt;)
/// </code>
/// </summary>
[HtmlTargetElement("app-input", TagStructure = TagStructure.WithoutEndTag)]
public class InputTagHelper : BoundControlTagHelper
{
    /// <summary>text | password | email | url | tel | number | search</summary>
    public string Type { get; set; } = "text";

    public string? PrefixIcon { get; set; }
    public string? SuffixIcon { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public string? AddonBefore { get; set; }
    public string? AddonAfter { get; set; }

    /// <summary>
    /// Markup in the add-on before the field (a link or a shortcut hint, say). It is written as it is, so only trusted HTML goes
    /// here, never user input; <see cref="AddonBefore"/> is the escaped text form.
    /// </summary>
    [HtmlAttributeName("addon-before-html")] public string? AddonBeforeHtml { get; set; }

    /// <summary>Markup in the add-on after the field, written as it is (trusted HTML only); see <see cref="AddonBeforeHtml"/>.</summary>
    [HtmlAttributeName("addon-after-html")] public string? AddonAfterHtml { get; set; }

    /// <summary>Shows a clear button while the field has a value.</summary>
    public bool AllowClear { get; set; }

    /// <summary>Borderless variant.</summary>
    public bool Bordered { get; set; } = true;

    /// <summary>Suggestions shown as the user types (Ant Design AutoComplete, via a native datalist).</summary>
    public IEnumerable<string>? Items { get; set; }

    private const string ClearScript =
        "value = ''; $refs.input.value = ''; $refs.input.dispatchEvent(new Event('input', { bubbles: true })); $refs.input.focus()";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var addon = AddonBefore is not null || AddonAfter is not null || AddonBeforeHtml is not null || AddonAfterHtml is not null;
        // Without addons the control itself is the outermost element and receives the author's class/style.
        var (outerClass, outerStyle) = addon ? (null, null) : TakeClassAndStyle(output);
        var password = Type == "password";
        var datalist = Items is null ? null : $"{context.UniqueId}-list";
        var attrs = ControlAttributes(output) + (datalist is null ? "" : Html.Attr("list", datalist));
        var affix = password || AllowClear || PrefixIcon is not null || SuffixIcon is not null || Prefix is not null || Suffix is not null;

        string control;
        if (!affix)
        {
            control = $"""<input type="{Html.E(Type)}" class="{Html.Cx("ant-input", SizeClass("ant-input"), StatusClass("ant-input"), Bordered ? null : "ant-input-borderless", Disabled ? "ant-input-disabled" : null, outerClass)}"{Html.Attr("style", outerStyle)}{attrs} />""";
        }
        else
        {
            var prefix = PrefixIcon is not null ? AntIcons.Span(PrefixIcon) : Prefix is not null ? Html.E(Prefix) : null;
            var suffix = new StringBuilder();
            if (AllowClear)
                suffix.Append($"""<button type="button" class="ant-input-clear-icon m-input-clear" aria-label="Clear" x-show="value" @click="{ClearScript}" tabindex="-1">{AntIcons.Svg("close-circle", IconThemeType.Fill)}</button>""");
            if (password)
                suffix.Append($"""<button type="button" class="ant-input-password-icon m-input-reveal" :aria-label="reveal ? 'Hide password' : 'Show password'" aria-label="Show password" @click="reveal = !reveal"><span x-show="!reveal">{AntIcons.Span("eye-invisible")}</span><span x-show="reveal" x-cloak>{AntIcons.Span("eye")}</span></button>""");
            if (SuffixIcon is not null) suffix.Append(AntIcons.Span(SuffixIcon));
            else if (Suffix is not null) suffix.Append(Html.E(Suffix));

            var typeBinding = password ? " :type=\"reveal ? 'text' : 'password'\"" : "";
            var model = AllowClear ? " @input=\"value = $el.value\"" : "";
            var data = AllowClear || password ? " x-data=\"{ reveal: false, value: " + (string.IsNullOrEmpty(Bound().Value) ? "''" : "true") + " }\" " : " ";
            control =
                $"""<span class="{Html.Cx("ant-input-affix-wrapper", SizeClass("ant-input-affix-wrapper"), StatusClass("ant-input-affix-wrapper"), password ? "ant-input-password" : null, Bordered ? null : "ant-input-affix-wrapper-borderless", Disabled ? "ant-input-affix-wrapper-disabled" : null, outerClass)}"{Html.Attr("style", outerStyle)}{data.TrimEnd()}>""" +
                (prefix is null ? "" : $"""<span class="ant-input-prefix">{prefix}</span>""") +
                $"""<input type="{Html.E(Type)}" class="ant-input" x-ref="input"{typeBinding}{model}{attrs} />""" +
                (suffix.Length == 0 ? "" : $"""<span class="ant-input-suffix">{suffix}</span>""") + "</span>";
        }

        if (addon)
        {
            control =
                $"""<span class="ant-input-wrapper ant-input-group">""" +
                (AddonBeforeHtml is not null ? $"""<span class="ant-input-group-addon">{AddonBeforeHtml}</span>""" : AddonBefore is null ? "" : $"""<span class="ant-input-group-addon">{Html.E(AddonBefore)}</span>""") + control +
                (AddonAfterHtml is not null ? $"""<span class="ant-input-group-addon">{AddonAfterHtml}</span>""" : AddonAfter is null ? "" : $"""<span class="ant-input-group-addon">{Html.E(AddonAfter)}</span>""") + "</span>";
            output.TagName = "span";
            output.AddClasses("ant-input-group-wrapper", SizeClass("ant-input-group-wrapper"), StatusClass("ant-input-group-wrapper"));
        }
        else
        {
            output.TagName = null;
        }

        if (datalist is not null)
            control += $"""<datalist id="{datalist}">{string.Concat(Items!.Select(i => $"<option value=\"{Html.E(i)}\"></option>"))}</datalist>""";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent(control);
    }
}
