using System.Collections.Concurrent;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Shell;

/// <summary>
/// Ant Design icon SVGs, the exact markup Ant Design Blazor's &lt;Icon&gt; renders. Every flavour ships the
/// AntDesign package (through Modulus.Ui.Assets), so the shell, Blazor pages and MVC views draw the same icons
/// without a sprite.
/// </summary>
/// <remarks>
/// AntDesign keeps its SVG store internal (<c>IconService.GetIconImg</c>); its public API only lists icon names
/// (<c>GetAllIcons</c>, <c>IconExists</c>), checked for 1.6.2. The method is found by reflection once, by name, so
/// this assembly needs no AntDesign reference. <c>AntIconsTests</c> fails the build if an upgrade moves it.
/// </remarks>
public static class AntIconSvg
{
    /// <summary>Theme names, in AntDesign's <c>IconThemeType</c> order.</summary>
    public const int Fill = 0, Outline = 1, TwoTone = 2;

    private const string SvgAttributes = """<svg focusable="false" width="1em" height="1em" fill="currentColor" aria-hidden="true" """;

    private static readonly Lazy<Func<string, int, string?>> Lookup = new(CreateLookup);
    private static readonly ConcurrentDictionary<(string, int), string?> Cache = new();

    /// <summary>Returns the icon's <c>&lt;svg&gt;</c> markup, or null when the name is unknown.</summary>
    /// <param name="name">Icon name, as in <c>&lt;Icon Type="…"&gt;</c>.</param>
    /// <param name="theme"><see cref="Outline"/> (default), <see cref="Fill"/> or <see cref="TwoTone"/>.</param>
    public static string? Svg(string name, int theme = Outline) =>
        Cache.GetOrAdd((name, theme), key =>
        {
            var svg = Lookup.Value(key.Item1, key.Item2);
            return string.IsNullOrEmpty(svg) ? null : svg.Replace("<svg ", SvgAttributes, StringComparison.Ordinal);
        });

    private static readonly ConcurrentDictionary<int, IReadOnlyList<string>> NameCache = new();

    /// <summary>Every icon name of a theme, sorted (AntDesign's public <c>IconService.GetAllIcons</c>).</summary>
    /// <param name="theme"><see cref="Outline"/> (default), <see cref="Fill"/> or <see cref="TwoTone"/>.</param>
    public static IReadOnlyList<string> Names(int theme = Outline) =>
        NameCache.GetOrAdd(theme, t =>
        {
            var method = Type.GetType("AntDesign.IconService, AntDesign")?.GetMethod("GetAllIcons", BindingFlags.Static | BindingFlags.Public, Type.EmptyTypes)
                ?? throw new InvalidOperationException("AntDesign.IconService.GetAllIcons() was not found — update Modulus.Ui.Abstractions.Shell.AntIconSvg.");
            if (method.Invoke(null, null) is not System.Collections.IDictionary all) return [];
            var themeName = t switch { Fill => "fill", TwoTone => "twotone", _ => "outline" };
            foreach (System.Collections.DictionaryEntry entry in all)
            {
                var match = entry.Key is Enum e ? Convert.ToInt32(e) == t : string.Equals(entry.Key?.ToString(), themeName, StringComparison.OrdinalIgnoreCase);
                if (match && entry.Value is IEnumerable<string> names)
                    return names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            }
            return [];
        });

    private static Func<string, int, string?> CreateLookup()
    {
        var service = Type.GetType("AntDesign.IconService, AntDesign");
        var theme = Type.GetType("AntDesign.IconThemeType, AntDesign");
        var method = service is null || theme is null ? null : service.GetMethod(
            "GetIconImg",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            [typeof(string), theme]);
        if (method is null)
            throw new InvalidOperationException(
                "AntDesign.IconService.GetIconImg(string, IconThemeType) was not found. " +
                "Either the AntDesign package is missing or it changed its icon API — update Modulus.Ui.Abstractions.Shell.AntIconSvg.");
        return (name, t) =>
        {
            try
            {
                return method.Invoke(null, [name, Enum.ToObject(theme!, t)]) as string;
            }
            catch (TargetInvocationException)
            {
                return null; // an unknown name, in AntDesign versions that throw for one
            }
        };
    }
}
