using System.Collections.Concurrent;
using System.Reflection;
using AntDesign;

namespace Modulus.AspNetCore.Mvc;

/// <summary>
/// Ant Design icon SVGs for server-rendered views — the exact markup Ant Design Blazor's
/// &lt;Icon&gt; renders, so MVC and Blazor pages show identical icons.
/// </summary>
/// <remarks>
/// AntDesign 1.6 exposes the SVG store only internally (<c>IconService.GetIconImg</c>).
/// It is reached by reflection once and cached; <c>AntIconsTests</c> fails the build if an
/// AntDesign upgrade moves it, so the break is caught in CI rather than in production.
/// </remarks>
public static class AntIcons
{
    private static readonly Func<string, IconThemeType, string?> Lookup = CreateLookup();
    private static readonly ConcurrentDictionary<(string, IconThemeType), string?> Cache = new();

    private const string SvgAttributes = """<svg focusable="false" width="1em" height="1em" fill="currentColor" aria-hidden="true" """;

    /// <summary>Returns the icon's <c>&lt;svg&gt;</c> markup, or null when the name is unknown.</summary>
    public static string? Svg(string name, IconThemeType theme = IconThemeType.Outline) =>
        Cache.GetOrAdd((name, theme), key =>
        {
            var svg = Lookup(key.Item1, key.Item2);
            return string.IsNullOrEmpty(svg) ? null : svg.Replace("<svg ", SvgAttributes, StringComparison.Ordinal);
        });

    /// <summary>Full <c>&lt;span class="anticon"&gt;</c> element, as Ant Design renders it.</summary>
    public static string Span(string name, IconThemeType theme = IconThemeType.Outline, string? cssClass = null)
    {
        var svg = Svg(name, theme) ?? "";
        var extra = string.IsNullOrEmpty(cssClass) ? "" : " " + cssClass;
        return $"""<span role="img" aria-hidden="true" class="anticon anticon-{System.Net.WebUtility.HtmlEncode(name)}{extra}">{svg}</span>""";
    }

    private static Func<string, IconThemeType, string?> CreateLookup()
    {
        var method = typeof(IconService).GetMethod(
            "GetIconImg",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            [typeof(string), typeof(IconThemeType)]);
        if (method is null)
            throw new InvalidOperationException(
                "AntDesign.IconService.GetIconImg(string, IconThemeType) was not found. " +
                "The AntDesign package changed its icon API — update Modulus.AspNetCore.Mvc.AntIcons.");
        return method.CreateDelegate<Func<string, IconThemeType, string?>>();
    }
}
