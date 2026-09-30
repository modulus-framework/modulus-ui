using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Modulus.Ui.Abstractions.Theming;

/// <summary>
/// Builds antd-dark-brand.css from Ant Design Blazor's dark stylesheet: every declaration that hard-codes one of its blue
/// shades (<see cref="AntDarkBrand.Sources"/>), with the shade as a <c>--m-adp-*</c> variable. Selectors naming the blue
/// preset colour (<c>-blue</c>) keep antd's blue.
/// </summary>
static partial class DarkBrandCss
{
    public const string Header = "/* Generated from Ant Design Blazor's dark stylesheet: the declarations that hard-code its primary blue, with that blue as --m-adp-* variables (ThemeCss.Build sets them from the brand colour). Regenerate: dotnet run --project tools/antd-drift -- --write-dark-brand */\n";

    [GeneratedRegex(@"#([0-9a-fA-F]{6})\b")] private static partial Regex Hex();
    [GeneratedRegex(@"rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*([\d.]+)\s*)?\)")] private static partial Regex Rgb();

    /// <summary>The stylesheet of the pinned AntDesign package in the NuGet cache, or null when it is not restored.</summary>
    public static string? Source(string version)
    {
        var packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        var path = Path.Combine(packages, "antdesign", version, "staticwebassets", "css", "ant-design-blazor.dark.min.css");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public static string Build(string css)
    {
        var sources = AntDarkBrand.Sources.ToHashSet(StringComparer.Ordinal);
        var output = new List<string>();
        int depth = 0, start = 0;
        char? quote = null;
        for (var i = 0; i < css.Length; i++)
        {
            var c = css[i];
            if (quote is not null)
            {
                if (c == quote) quote = null;
                continue;
            }
            if (c is '"' or '\'') quote = c;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0)
            {
                var rule = css[start..(i + 1)].Trim();
                start = i + 1;
                if (rule.StartsWith('@')) continue;
                var open = rule.IndexOf('{');
                var selector = rule[..open];
                var body = rule[(open + 1)..^1];
                var keep = Split(body, ';').Select(d => Recolor(d.Trim(), sources)).Where(d => d.Contains("var(--m-adp-", StringComparison.Ordinal)).ToList();
                var selectors = Split(selector.Trim(), ',').Where(s => !s.Contains("-blue", StringComparison.Ordinal)).Select(s => s.Trim()).ToList();
                if (keep.Count > 0 && selectors.Count > 0) output.Add(string.Join(',', selectors) + "{" + string.Join(';', keep) + "}");
            }
        }
        return Header + string.Join('\n', output) + "\n";
    }

    private static string Recolor(string declaration, HashSet<string> sources)
    {
        declaration = Hex().Replace(declaration, m =>
        {
            var hex = m.Groups[1].Value.ToLowerInvariant();
            return sources.Contains(hex) ? $"var(--m-adp-{hex})" : m.Value;
        });
        return Rgb().Replace(declaration, m =>
        {
            var hex = string.Concat(Enumerable.Range(1, 3).Select(g => int.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture).ToString("x2", CultureInfo.InvariantCulture)));
            if (!sources.Contains(hex)) return m.Value;
            if (!m.Groups[4].Success) return $"var(--m-adp-{hex})";
            var percent = (double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) * 100).ToString("0.##", CultureInfo.InvariantCulture);
            return $"color-mix(in srgb,var(--m-adp-{hex}) {percent}%,transparent)";
        });
    }

    private static List<string> Split(string text, char separator)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;
        char? quote = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quote is not null)
            {
                if (c == quote) quote = null;
            }
            else if (c is '"' or '\'') quote = c;
            else if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == separator && depth == 0)
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }
        parts.Add(text[start..]);
        return parts;
    }
}
