using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

internal static class Html
{
    public static string E(string? value) => HtmlEncoder.Default.Encode(value ?? "");

    /// <summary>Appends classes to whatever the author already put in class="...".</summary>
    public static void AddClasses(this TagHelperOutput output, params string?[] classes)
    {
        var existing = output.Attributes.TryGetAttribute("class", out var attr) ? attr.Value?.ToString() : null;
        var all = classes.Where(c => !string.IsNullOrWhiteSpace(c)).Append(existing).Where(c => !string.IsNullOrWhiteSpace(c));
        output.Attributes.SetAttribute("class", string.Join(' ', all));
    }

    /// <summary>Prepends declarations to the author's style="..." (the author's own values win).</summary>
    public static void AddStyle(this TagHelperOutput output, string? style)
    {
        if (string.IsNullOrEmpty(style)) return;
        var existing = output.Attributes.TryGetAttribute("style", out var attr) ? attr.Value?.ToString() : null;
        output.Attributes.SetAttribute("style", string.IsNullOrWhiteSpace(existing) ? style : $"{style.TrimEnd(';')};{existing}");
    }

    /// <summary>Joins the classes that are not null or empty.</summary>
    public static string Cx(params string?[] classes) => string.Join(' ', classes.Where(c => !string.IsNullOrWhiteSpace(c)));

    /// <summary>
    /// Removes the author's unbound attributes (placeholder, maxlength, hx-*, x-*, aria-*, …) from the
    /// output element and returns them serialized, so a helper that renders a wrapper can put them on
    /// the real control inside. <c>class</c> and <c>style</c> stay on the wrapper.
    /// </summary>
    public static string TakeAttributes(this TagHelperOutput output, params string[] keep)
    {
        var sb = new StringBuilder();
        foreach (var attr in output.Attributes.ToList())
        {
            if (attr.Name is "class" or "style" || keep.Contains(attr.Name, StringComparer.OrdinalIgnoreCase)) continue;
            output.Attributes.Remove(attr);
            using var writer = new StringWriter();
            attr.WriteTo(writer, HtmlEncoder.Default);
            sb.Append(' ').Append(writer);
        }
        return sb.ToString();
    }

    /// <summary>Name, id and current value for a control bound with <c>asp-for</c> (or an explicit name).</summary>
    public static (string Name, string Id, string? Value) Bind(ModelExpression? @for, string? name, string? value = null)
    {
        var n = @for?.Name ?? name ?? "";
        var v = value ?? Format(@for?.Model);
        return (n, n.Length == 0 ? "" : TagBuilder.CreateSanitizedId(n, "_"), v);
    }

    /// <summary>ModelState errors for a bound control (drives <c>ant-input-status-error</c>).</summary>
    public static bool HasError(ViewContext? view, ModelExpression? @for) =>
        view is not null && @for is not null &&
        view.ViewData.ModelState.TryGetValue(@for.Name, out var entry) && entry.Errors.Count > 0;

    public static string Format(object? value) => value switch
    {
        null => "",
        DateTime d when d.TimeOfDay == TimeSpan.Zero => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly t => t.ToString("HH:mm", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    public static string Attr(string name, string? value) => value is null ? "" : $" {name}=\"{E(value)}\"";

    public static string Attr(string name, bool on) => on ? $" {name}" : "";
}
