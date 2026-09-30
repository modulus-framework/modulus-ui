using AntDesign;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.AspNetCore.Mvc;

/// <summary>
/// Ant Design icon SVGs for server-rendered views — the exact markup Ant Design Blazor's
/// &lt;Icon&gt; renders, so MVC and Blazor pages show identical icons.
/// </summary>
/// <remarks>The SVGs come from <see cref="AntIconSvg"/>, which the shell uses for <c>ant:</c> menu icons too.</remarks>
public static class AntIcons
{
    /// <summary>Returns the icon's <c>&lt;svg&gt;</c> markup, or null when the name is unknown.</summary>
    public static string? Svg(string name, IconThemeType theme = IconThemeType.Outline) => AntIconSvg.Svg(name, (int)theme);

    /// <summary>Full <c>&lt;span class="anticon"&gt;</c> element, as Ant Design renders it.</summary>
    public static string Span(string name, IconThemeType theme = IconThemeType.Outline, string? cssClass = null)
    {
        var svg = Svg(name, theme) ?? "";
        var extra = string.IsNullOrEmpty(cssClass) ? "" : " " + cssClass;
        return $"""<span role="img" aria-hidden="true" class="anticon anticon-{System.Net.WebUtility.HtmlEncode(name)}{extra}">{svg}</span>""";
    }
}
