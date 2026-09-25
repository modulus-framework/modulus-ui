using System.Text;

namespace Modulus.Ui.Abstractions.Theming;

/// <summary>
/// Builds the <c>:root</c> custom-property block consumed by
/// <c>ant-design-blazor.variable.css</c> (antd v4 dynamic theme variables) and
/// <c>modulus.css</c>. Output is identical for every flavor, which is what keeps
/// Blazor and MVC pixel-aligned.
/// </summary>
public static class ThemeCss
{
    /// <summary>Cookie holding the user's explicit light/dark choice so the server can render it without a flash.</summary>
    public const string ModeCookie = "modulus-theme";

    /// <summary>Cookie holding the sider collapsed state.</summary>
    public const string CollapsedCookie = "modulus-collapsed";

    // Theme settings panel choices. The client script applies them before first paint and the
    // server reads them through ResolveOptions, so both render the same theme.
    public const string PrimaryCookie = "modulus-primary";
    public const string RadiusCookie = "modulus-radius";
    public const string LayoutCookie = "modulus-layout";
    public const string WidthCookie = "modulus-width";
    public const string NavCookie = "modulus-nav";

    /// <summary>
    /// Applies the user's theme-settings cookies to a copy of <paramref name="options"/>.
    /// Only known preset names, radii from 0 to 16 and the enum values are accepted.
    /// </summary>
    public static ModulusThemeOptions ResolveOptions(ModulusThemeOptions options, Func<string, string?> cookie)
    {
        var o = options.Clone();
        if (cookie(PrimaryCookie) is { } preset && o.ColorPresets.TryGetValue(preset, out var hex)) o.PrimaryColor = hex;
        if (int.TryParse(cookie(RadiusCookie), out var radius) && radius is >= 0 and <= 16) o.BorderRadius = radius;
        if (Enum.TryParse<ShellLayout>(cookie(LayoutCookie), true, out var layout)) o.Layout = layout;
        if (Enum.TryParse<ContentWidth>(cookie(WidthCookie), true, out var width)) o.ContentWidth = width;
        if (Enum.TryParse<NavTheme>(cookie(NavCookie), true, out var nav)) o.NavTheme = nav;
        return o;
    }

    /// <summary>The name of the preset matching the current primary colour, or null for a custom colour.</summary>
    public static string? PresetName(ModulusThemeOptions options) =>
        options.ColorPresets.FirstOrDefault(p => string.Equals(p.Value, options.PrimaryColor, StringComparison.OrdinalIgnoreCase)).Key;

    /// <summary>Brand tokens for one preset colour, used by the theme settings panel to recolour without a reload.</summary>
    public static string BuildForPrimary(ModulusThemeOptions options, string primary)
    {
        var o = options.Clone();
        o.PrimaryColor = primary;
        return Build(o);
    }

    public static string Build(ModulusThemeOptions options)
    {
        var vars = new List<(string Name, string Value)>();

        var primary = ParseOr(options.PrimaryColor, "#1677ff");
        FillColor(vars, "primary", primary);
        var palette = AntPalette.Generate(primary);
        for (var i = 0; i < palette.Count; i++)
            vars.Add(($"--ant-primary-{i + 1}", palette[i].ToString()));
        vars.Add(("--ant-primary-color-deprecated-l-35", primary.Lighten(35).ToString()));
        vars.Add(("--ant-primary-color-deprecated-l-20", primary.Lighten(20).ToString()));
        vars.Add(("--ant-primary-color-deprecated-t-20", primary.Tint(20).ToString()));
        vars.Add(("--ant-primary-color-deprecated-t-50", primary.Tint(50).ToString()));
        vars.Add(("--ant-primary-color-deprecated-f-12", primary.WithAlpha(0.12).ToString()));
        vars.Add(("--ant-primary-color-active-deprecated-f-30", palette[0].WithAlpha(0.3).ToString()));
        vars.Add(("--ant-primary-color-active-deprecated-d-02", palette[0].Darken(2).ToString()));

        FillColor(vars, "success", ParseOr(options.SuccessColor, "#52c41a"));
        FillColor(vars, "warning", ParseOr(options.WarningColor, "#faad14"));
        FillColor(vars, "error", ParseOr(options.ErrorColor, "#ff4d4f"));
        FillColor(vars, "info", ParseOr(options.InfoColor ?? options.PrimaryColor, "#1677ff"));

        var radius = Math.Clamp(options.BorderRadius, 0, 16);
        vars.Add(("--m-radius-sm", $"{Math.Max(radius - 2, 0)}px"));
        vars.Add(("--m-radius", $"{radius}px"));
        vars.Add(("--m-radius-lg", $"{radius + 2}px"));
        vars.Add(("--m-sider-width", $"{options.SiderWidth}px"));
        vars.Add(("--m-header-height", $"{options.HeaderHeight}px"));

        var sb = new StringBuilder(":root{");
        foreach (var (name, value) in vars)
            sb.Append(name).Append(':').Append(value).Append(';');
        return sb.Append('}').ToString();
    }

    private static void FillColor(List<(string, string)> vars, string type, AntColor color)
    {
        var palette = AntPalette.Generate(color);
        vars.Add(($"--ant-{type}-color", color.ToString()));
        vars.Add(($"--ant-{type}-color-disabled", palette[1].ToString()));
        vars.Add(($"--ant-{type}-color-hover", palette[4].ToString()));
        vars.Add(($"--ant-{type}-color-active", palette[6].ToString()));
        vars.Add(($"--ant-{type}-color-outline", color.WithAlpha(0.2).ToString()));
        vars.Add(($"--ant-{type}-color-deprecated-bg", palette[0].ToString()));
        vars.Add(($"--ant-{type}-color-deprecated-border", palette[2].ToString()));
    }

    private static AntColor ParseOr(string? value, string fallback) =>
        AntColor.TryParse(value, out var c) ? c : AntColor.Parse(fallback);

    /// <summary>
    /// Resolves the mode to render on the server from the persisted cookie value.
    /// Returns null for <see cref="ThemeMode.System"/> with no cookie — the client script decides.
    /// </summary>
    public static string? ResolveMode(string? cookieValue, ThemeMode defaultMode) =>
        cookieValue is "dark" or "light"
            ? cookieValue
            : defaultMode switch
            {
                ThemeMode.Dark => "dark",
                ThemeMode.Light => "light",
                _ => null,
            };
}
