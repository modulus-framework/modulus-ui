namespace Modulus.Ui.Abstractions.Theming;

public enum ThemeMode
{
    Light,
    Dark,
    /// <summary>Follow the operating system's <c>prefers-color-scheme</c>.</summary>
    System,
}

public enum NavTheme
{
    /// <summary>Navigation on the page surface (white in light mode).</summary>
    Light,
    /// <summary>Dark navigation bar / sidebar in both modes.</summary>
    Dark,
}

/// <summary>Where the main navigation lives (Tabler layouts).</summary>
public enum ShellLayout
{
    /// <summary>Header plus a horizontal navigation bar with dropdown menus.</summary>
    Horizontal,
    /// <summary>Fixed sidebar with collapsible groups.</summary>
    Vertical,
}

public enum ContentWidth
{
    /// <summary>Content centred in a 1320 px container.</summary>
    Boxed,
    /// <summary>Content uses the full window width.</summary>
    Fluid,
}

/// <summary>What the navbar and sidebar list.</summary>
public enum MenuScope
{
    /// <summary>The whole menu (Tabler).</summary>
    All,
    /// <summary>
    /// Only the current app's menu, behind its name, while a page of an app is open (Odoo). Apps are the header's
    /// <c>ShellApp</c> tiles with a <c>MenuKey</c>; other apps are a click away in the app launcher.
    /// </summary>
    App,
}

/// <summary>Font family of the whole interface (Tabler's theme settings).</summary>
public enum ThemeFont
{
    /// <summary>Inter, bundled with Modulus.</summary>
    Sans,
    /// <summary>The operating system's UI font (Segoe UI, San Francisco, Roboto).</summary>
    System,
    Serif,
    Mono,
}

/// <summary>Neutral palette of surfaces, borders and text (Tabler's "theme base").</summary>
public enum ThemeBase
{
    /// <summary>Cool gray with a hint of blue; Tabler's default.</summary>
    Gray,
    /// <summary>Blue-gray.</summary>
    Slate,
    /// <summary>Cool, nearly neutral gray.</summary>
    Zinc,
    /// <summary>Pure gray.</summary>
    Neutral,
    /// <summary>Warm gray.</summary>
    Stone,
}

/// <summary>
/// Brand and layout settings shared by every Modulus flavor (Blazor, MVC, Razor Pages).
/// Bind from configuration: <c>builder.Services.Configure&lt;ModulusThemeOptions&gt;(config.GetSection("Modulus:Theme"))</c>.
/// </summary>
public class ModulusThemeOptions
{
    /// <summary>Application name shown in the sider header and document title suffix.</summary>
    public string Title { get; set; } = "Modulus Admin";

    /// <summary>Logo URL. Leave null to render the built-in mark.</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Brand colour (#rrggbb). Defaults to Tabler blue.</summary>
    public string PrimaryColor { get; set; } = "#066fd1";

    public string SuccessColor { get; set; } = "#2fb344";
    public string WarningColor { get; set; } = "#f59f00";
    public string ErrorColor { get; set; } = "#d63939";

    /// <summary>Info colour. Null follows <see cref="PrimaryColor"/>, as in Ant Design.</summary>
    public string? InfoColor { get; set; }

    /// <summary>Base control radius in px (buttons, inputs). Cards and overlays use radius + 2.</summary>
    public int BorderRadius { get; set; } = 6;

    /// <summary>Theme used on first visit; the user's toggle choice is remembered after that.</summary>
    public ThemeMode DefaultMode { get; set; } = ThemeMode.Light;

    public NavTheme NavTheme { get; set; } = NavTheme.Light;

    /// <summary>Horizontal navbar (Tabler's default) or a vertical sidebar.</summary>
    public ShellLayout Layout { get; set; } = ShellLayout.Horizontal;

    public ContentWidth ContentWidth { get; set; } = ContentWidth.Boxed;

    /// <summary>The whole menu (Tabler) or only the current app's menu (Odoo).</summary>
    public MenuScope MenuScope { get; set; } = MenuScope.All;

    public ThemeFont Font { get; set; } = ThemeFont.Sans;

    public ThemeBase Base { get; set; } = ThemeBase.Gray;

    /// <summary>Brand colours offered in the theme settings panel (name → #rrggbb).</summary>
    public IDictionary<string, string> ColorPresets { get; set; } = new Dictionary<string, string>
    {
        ["blue"] = "#066fd1",
        ["indigo"] = "#4263eb",
        ["purple"] = "#ae3ec9",
        ["pink"] = "#d6336c",
        ["red"] = "#d63939",
        ["orange"] = "#f76707",
        ["green"] = "#2fb344",
        ["teal"] = "#0ca678",
    };

    /// <summary>Show the theme settings panel (colour mode, scheme, font, base, layout, menu, width, radius).</summary>
    public bool ShowCustomizer { get; set; } = true;

    /// <summary>Sidebar / legacy ProLayout sider width in px.</summary>
    public int SiderWidth { get; set; } = 240;

    /// <summary>Header height in px (Ant Design Pro uses 56).</summary>
    public int HeaderHeight { get; set; } = 56;

    /// <summary>Tighter control heights and table padding for data-dense screens.</summary>
    public bool Compact { get; set; }

    /// <summary>Footer copyright line. Null hides the copyright.</summary>
    public string? Copyright { get; set; } = $"{DateTime.UtcNow.Year} Modulus";

    /// <summary>Version label shown in the footer, e.g. "v1.0.0". Null hides it.</summary>
    public string? Version { get; set; }

    /// <summary>Returns a copy, so per-request overrides (theme settings cookies) never touch the singleton.</summary>
    public ModulusThemeOptions Clone()
    {
        var copy = (ModulusThemeOptions)MemberwiseClone();
        copy.ColorPresets = new Dictionary<string, string>(ColorPresets);
        return copy;
    }
}
