namespace Modulus.Blazor.Core.Theme;

/// <summary>
/// Theme configuration options for Modulus UI.
/// </summary>
public class ModulusThemeOptions
{
    /// <summary>
    /// Primary color (hex, e.g. "#1890ff").
    /// </summary>
    public string PrimaryColor { get; set; } = "#1890ff";

    /// <summary>
    /// Compact mode (reduces spacing).
    /// </summary>
    public bool CompactMode { get; set; } = false;

    /// <summary>
    /// Border radius size: "small" | "medium" | "large".
    /// </summary>
    public string BorderRadius { get; set; } = "medium";

    /// <summary>
    /// Locale: "en-US" | "zh-CN" | etc. Affects number/date formatting and antd locale.
    /// </summary>
    public string Locale { get; set; } = "en-US";

    /// <summary>
    /// Enable RTL layout.
    /// </summary>
    public bool EnableRTL { get; set; } = false;
}

/// <summary>
/// Design tokens state (used by DesignTokens component).
/// </summary>
public class ModulusDesignTokens
{
    public bool IsDark { get; set; }
    public ModulusThemeOptions? Options { get; set; }
}
