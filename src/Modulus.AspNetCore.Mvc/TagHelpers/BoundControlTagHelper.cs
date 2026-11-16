using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// Data-entry controls. Every control renders a real form element (input, textarea, hidden input),
// so forms post normally, work with htmx (hx-post / hx-include) and bind back to the model.
// Unbound attributes (placeholder, maxlength, required, autocomplete, hx-*, x-*, aria-*) go on the
// real control; class and style stay on the outer element.


/// <summary>Base for controls bound with <c>asp-for</c> or a plain <c>name</c>.</summary>
public abstract class BoundControlTagHelper : TagHelper
{
    [HtmlAttributeName("asp-for")] public ModelExpression? For { get; set; }

    public string? Name { get; set; }
    public string? Value { get; set; }

    /// <summary>small | middle | large</summary>
    public string? Size { get; set; }

    /// <summary>error | warning. Defaults to error when ModelState has errors for <c>asp-for</c>.</summary>
    public string? Status { get; set; }

    public bool Disabled { get; set; }

    [ViewContext, HtmlAttributeNotBound] public ViewContext? ViewContext { get; set; }

    protected string? EffectiveStatus => Status ?? (Html.HasError(ViewContext, For) ? "error" : null);

    protected string? SizeClass(string prefix) => Size switch { "small" => $"{prefix}-sm", "large" => $"{prefix}-lg", _ => null };

    protected string? StatusClass(string prefix) => EffectiveStatus is { } s ? $"{prefix}-status-{s}" : null;

    protected (string Name, string Id, string? Value) Bound() => Html.Bind(For, Name, Value);

    /// <summary>Removes and returns the author's class and style, for controls rendered without a wrapper.</summary>
    protected static (string? Class, string? Style) TakeClassAndStyle(TagHelperOutput output)
    {
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        var style = output.Attributes.TryGetAttribute("style", out var st) ? st.Value?.ToString() : null;
        output.Attributes.RemoveAll("class");
        output.Attributes.RemoveAll("style");
        return (cls, style);
    }

    /// <summary>name, id, value, disabled and the author's pass-through attributes for the inner control.</summary>
    protected string ControlAttributes(TagHelperOutput output, bool includeValue = true)
    {
        var (name, id, value) = Bound();
        var passThrough = output.TakeAttributes();
        var hasId = passThrough.Contains(" id=\"", StringComparison.Ordinal);
        return Html.Attr("name", name.Length > 0 ? name : null) + (hasId || id.Length == 0 ? "" : Html.Attr("id", id)) +
               (includeValue ? Html.Attr("value", value) : "") + Html.Attr("disabled", Disabled) +
               (EffectiveStatus == "error" ? " aria-invalid=\"true\"" : "") + passThrough;
    }
}
