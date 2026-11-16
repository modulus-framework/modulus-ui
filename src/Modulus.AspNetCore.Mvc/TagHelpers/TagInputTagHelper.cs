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
/// Tag input: type a value and press Enter (or a separator) to add it as a tag; Backspace in the empty field removes the last
/// one. Each tag posts as its own hidden input, so a <c>List&lt;string&gt;</c> or <c>string[]</c> property binds back.
/// <code>&lt;app-tag-input asp-for="Keywords" max="5" placeholder="Add a keyword" /&gt;</code>
/// The field raises <c>m-tags-change</c> (detail: the tags). Driven by modulus-shell.js; the Blazor <c>AppTagInput</c> renders the same DOM.
/// </summary>
[HtmlTargetElement("app-tag-input", TagStructure = TagStructure.WithoutEndTag)]
public class TagInputTagHelper : BoundControlTagHelper
{
    /// <summary>Most tags allowed; 0 for no limit.</summary>
    public int Max { get; set; }

    /// <summary>Longest tag in characters; 0 for no limit.</summary>
    public int MaxLength { get; set; }

    /// <summary>Characters that end a tag, besides Enter.</summary>
    public string Separators { get; set; } = TagInput.DefaultSeparators;

    public bool AllowDuplicates { get; set; }

    public string? Placeholder { get; set; }

    [HtmlAttributeName("aria-label")] public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, _) = Bound();
        output.TakeAttributes();
        var tags = TagInput.Clean(For?.Model is IEnumerable list and not string ? list.Cast<object?>().Select(Html.Format) : [], Max, MaxLength, AllowDuplicates);
        var label = AriaLabel ?? "Tags";

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-tags", SizeClass("m-tags"), StatusClass("m-tags"), Disabled ? "m-tags-disabled" : null);
        output.Attributes.SetAttribute("data-m-tags", name);
        output.Attributes.SetAttribute("data-max", Max.ToString(CultureInfo.InvariantCulture));
        output.Attributes.SetAttribute("data-max-length", MaxLength.ToString(CultureInfo.InvariantCulture));
        output.Attributes.SetAttribute("data-separators", Separators);
        output.Attributes.SetAttribute("data-duplicates", AllowDuplicates ? "true" : "false");

        var sb = new StringBuilder("""<div class="m-tags-list" role="list">""");
        foreach (var t in tags) sb.Append(Chip(t, name, label, Disabled));
        sb.Append($"""<input type="text" class="m-tags-field"{(id.Length > 0 ? $" id=\"{Html.E(id)}\"" : "")} autocomplete="off" aria-label="{Html.E(label)}"{Html.Attr("placeholder", Placeholder)}{Html.Attr("disabled", Disabled)} /></div>""");
        output.Content.SetHtmlContent(sb.ToString());
    }

    internal static string Chip(string tag, string name, string label, bool disabled) =>
        $"""<span class="m-tags-item" role="listitem"><span class="m-tags-text">{Html.E(tag)}</span>""" +
        (disabled ? "" : $"""<button type="button" class="m-tags-remove" aria-label="Remove {Html.E(tag)}" tabindex="-1">{AntIcons.Span("close")}</button>""") +
        $"""<input type="hidden"{Html.Attr("name", name.Length > 0 ? name : null)} value="{Html.E(tag)}" /></span>""";
}
