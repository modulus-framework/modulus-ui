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
    /// <summary>One header row: brand, the menu and the header actions (no separate navbar).</summary>
    Condensed,
}

/// <summary>Side of the window the sidebar sits on (vertical layout). Follows the text direction.</summary>
public enum SidebarPosition
{
    /// <summary>Start side: left, or right in right-to-left mode.</summary>
    Left,
    /// <summary>End side: right, or left in right-to-left mode.</summary>
    Right,
}

/// <summary>Surface of the sidebar (vertical layout).</summary>
public enum SidebarStyle
{
    /// <summary>Full-height panel along the window edge.</summary>
    Default,
    /// <summary>No panel: the menu sits on the page background.</summary>
    Transparent,
    /// <summary>Rounded panel inset from the window edges, with a shadow.</summary>
    Floating,
}

/// <summary>Width of the sidebar (vertical layout).</summary>
public enum SidebarFold
{
    /// <summary>Icons and labels.</summary>
    Full,
    /// <summary>Icons only; groups open as flyouts and labels show as tooltips.</summary>
    Folded,
    /// <summary>Icons only until the pointer rests on it, then it expands over the page.</summary>
    Hover,
}

/// <summary>How the header behaves when the page scrolls.</summary>
public enum HeaderMode
{
    /// <summary>The header row stays at the top; the navbar scrolls away.</summary>
    Fixed,
    /// <summary>The header row and the navbar both stay at the top.</summary>
    Sticky,
    /// <summary>Everything scrolls with the page.</summary>
    Static,
    /// <summary>A rounded bar inset from the window edges that stays at the top.</summary>
    Floating,
}

/// <summary>Colour of the header row.</summary>
public enum HeaderTheme
{
    Light,
    Dark,
}

public enum TextDirection
{
    /// <summary>Left to right.</summary>
    Ltr,
    /// <summary>Right to left (Arabic, Hebrew, Persian, Urdu): <c>dir="rtl"</c> on &lt;html&gt;.</summary>
    Rtl,
}

public enum ContentWidth
{
    /// <summary>Content centred in a 1320 px container.</summary>
    Boxed,
    /// <summary>Content uses the full window width.</summary>
    Fluid,
    /// <summary>The whole page (header, navigation, content) in a centred frame on a tinted backdrop.</summary>
    Framed,
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

/// <summary>Spacing and type size of the whole interface (SAP Fiori's "compact" and "cozy").</summary>
public enum ThemeDensity
{
    /// <summary>
    /// Data-dense, for ERP screens: 13 px text, 30 px controls, 32 px table rows and a tighter menu. Body text stays at 13 px,
    /// small print at 12 px (11 px for uppercase labels), contrast is unchanged and every control stays above the 24 px WCAG 2.2 target size.
    /// </summary>
    Compact,
    /// <summary>Tabler's spacing: 14 px text, 36 px controls, roomier cards and menus.</summary>
    Comfortable,
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

    public SidebarPosition SidebarPosition { get; set; } = SidebarPosition.Left;

    public SidebarStyle SidebarStyle { get; set; } = SidebarStyle.Default;

    public SidebarFold SidebarFold { get; set; } = SidebarFold.Full;

    public HeaderMode Header { get; set; } = HeaderMode.Fixed;

    public HeaderTheme HeaderTheme { get; set; } = HeaderTheme.Light;

    public TextDirection Direction { get; set; } = TextDirection.Ltr;

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

    /// <summary>Show the theme settings panel (colour mode, scheme, base, density, font, radius, layout presets, layout, sidebar, header, menu, width, direction).</summary>
    public bool ShowCustomizer { get; set; } = true;

    /// <summary>
    /// Languages offered in the theme settings panel, as culture names ("en-US", "de-DE" …); the first is the default. With two or
    /// more the panel shows a language picker that sets the request culture (see <see cref="ModulusCulture"/>); the app must call
    /// <c>UseRequestLocalization</c> with the same cultures (MVC: <c>app.UseModulusLocalization()</c>).
    /// </summary>
    public List<string> Cultures { get; set; } = [];

    /// <summary>Sidebar / legacy ProLayout sider width in px (comfortable density; compact uses 224).</summary>
    public int SiderWidth { get; set; } = 240;

    /// <summary>Header height in px (Ant Design Pro uses 56) for comfortable density; compact uses 48.</summary>
    public int HeaderHeight { get; set; } = 56;

    /// <summary>Spacing and type size: compact (data-dense, the default) or comfortable.</summary>
    public ThemeDensity Density { get; set; } = ThemeDensity.Compact;

    /// <summary>Footer copyright line. Null hides the copyright.</summary>
    public string? Copyright { get; set; } = $"{DateTime.UtcNow.Year} Modulus";

    /// <summary>Version label shown in the footer, e.g. "v1.0.0". Null hides it.</summary>
    public string? Version { get; set; }

    /// <summary>
    /// MVC / Razor Pages: SPA-style navigation. Links and forms inside the shell load through htmx (<c>hx-boost</c>), the
    /// server answers with only the page body and the navigation, and the shell, scripts and Alpine state stay loaded.
    /// Pages that aren't in the shell (sign-in, status pages, downloads) still load normally. Blazor has its own enhanced
    /// navigation and ignores this.
    /// </summary>
    public bool SpaNavigation { get; set; }

    /// <summary>With <see cref="SpaNavigation"/>: fetch a page when the pointer rests on its link, so the click shows it at once.</summary>
    public bool SpaPrefetch { get; set; } = true;

    /// <summary>Returns a copy, so per-request overrides (theme settings cookies) never touch the singleton.</summary>
    public ModulusThemeOptions Clone()
    {
        var copy = (ModulusThemeOptions)MemberwiseClone();
        copy.ColorPresets = new Dictionary<string, string>(ColorPresets);
        copy.Cultures = [.. Cultures];
        return copy;
    }
}
