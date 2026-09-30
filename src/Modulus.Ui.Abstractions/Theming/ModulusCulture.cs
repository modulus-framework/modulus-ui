using System.Globalization;

namespace Modulus.Ui.Abstractions.Theming;

/// <summary>
/// The language picker of the theme settings panel (<see cref="ModulusThemeOptions.Cultures"/>). The panel writes ASP.NET Core's
/// culture cookie and reloads; the request localization middleware then sets the request culture, which Ant Design's
/// <c>LocaleProvider</c> (Blazor) and the tag helpers (dates, numbers) read. Blazor Server keeps the culture of the request that
/// opened the circuit.
/// </summary>
public static class ModulusCulture
{
    /// <summary>ASP.NET Core's culture cookie (<c>CookieRequestCultureProvider.DefaultCookieName</c>).</summary>
    public const string CookieName = ".AspNetCore.Culture";

    /// <summary>The cookie value for a culture, as <c>CookieRequestCultureProvider.MakeCookieValue</c> writes it.</summary>
    public static string CookieValue(string culture) => $"c={culture}|uic={culture}";

    /// <summary>The configured cultures that exist on this machine, without duplicates, in their configured order.</summary>
    public static IReadOnlyList<CultureInfo> Supported(IEnumerable<string>? cultures)
    {
        var list = new List<CultureInfo>();
        foreach (var name in cultures ?? [])
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            try
            {
                var culture = CultureInfo.GetCultureInfo(name.Trim());
                if (culture.Name.Length > 0 && list.All(c => c.Name != culture.Name)) list.Add(culture);
            }
            catch (CultureNotFoundException)
            {
                // Skip names this runtime does not know (invariant globalization mode knows none).
            }
        }
        return list;
    }

    /// <summary>
    /// The supported culture that matches <paramref name="current"/>: the same name, else the same language, else the first
    /// (the default). Null when nothing is supported.
    /// </summary>
    public static CultureInfo? Match(IReadOnlyList<CultureInfo> supported, CultureInfo current)
    {
        if (supported.Count == 0) return null;
        return supported.FirstOrDefault(c => c.Name.Equals(current.Name, StringComparison.OrdinalIgnoreCase))
            ?? supported.FirstOrDefault(c => c.TwoLetterISOLanguageName == current.TwoLetterISOLanguageName)
            ?? supported[0];
    }

    /// <summary>A culture's name in its own language, capitalised ("Deutsch (Deutschland)").</summary>
    public static string NativeName(CultureInfo culture)
    {
        var name = culture.NativeName;
        return name.Length == 0 ? culture.Name : char.ToUpper(name[0], culture) + name[1..];
    }
}
