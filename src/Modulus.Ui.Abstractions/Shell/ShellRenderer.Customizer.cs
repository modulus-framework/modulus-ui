using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.Ui.Abstractions.Shell;
public static partial class ShellRenderer
{
    // Theme settings panel ----------------------------------------------------------------------------------

    /// <summary>
    /// Offcanvas "Theme settings" panel. Each colour swatch carries its generated brand CSS in
    /// <c>data-css</c>, so the page recolours instantly; the choice is saved in cookies.
    /// </summary>
    public static string Customizer(ShellView view)
    {
        var o = view.Options;
        if (!o.ShowCustomizer) return "";
        var preset = ThemeCss.PresetName(o);
        var sb = new StringBuilder();
        sb.Append("""<div class="m-offcanvas" id="m-customizer" data-m-customizer hidden>""")
          .Append("""<div class="m-offcanvas-backdrop" data-m-customizer-close></div>""")
          .Append("""<aside class="m-offcanvas-panel m-customizer-panel" role="dialog" aria-modal="true" aria-labelledby="m-customizer-title">""")
          .Append("""<div class="m-offcanvas-header"><span class="m-customizer-mark">""").Append(Icon("tb:palette")).Append("</span>")
          .Append("""<div class="m-customizer-heading"><h2 class="m-offcanvas-title" id="m-customizer-title">Theme settings</h2>""")
          .Append("""<p class="m-offcanvas-subtitle">Saved in this browser and applied instantly.</p></div>""")
          .Append("""<button type="button" class="m-icon-btn" data-m-customizer-close aria-label="Close">""").Append(Icon("close")).Append("</button></div>")
          // Two panes, so neither is a long scroll: how the app looks, and how its shell is laid out.
          .Append("""<div class="m-setting-tabs" role="tablist" aria-label="Theme settings">""")
          .Append("""<button type="button" class="m-setting-tab" role="tab" id="m-setting-tab-theme" aria-controls="m-setting-pane-theme" aria-selected="true" data-m-customizer-tab="theme">""").Append(Icon("tb:brush")).Append("Appearance</button>")
          .Append("""<button type="button" class="m-setting-tab" role="tab" id="m-setting-tab-layout" aria-controls="m-setting-pane-layout" aria-selected="false" tabindex="-1" data-m-customizer-tab="layout">""").Append(Icon("dashboard")).Append("Layout</button>")
          .Append("</div>")
          .Append("""<form class="m-offcanvas-body" data-m-customizer-form>""")
          .Append("""<div class="m-setting-pane" id="m-setting-pane-theme" role="tabpanel" aria-labelledby="m-setting-tab-theme">""");

        Choice(sb, "Color mode", "mode", view.Mode ?? "system", [("light", "Light", Icon("sun")), ("dark", "Dark", Icon("moon")), ("system", "System", Icon("desktop"))]);

        sb.Append("""<fieldset class="m-setting"><legend class="m-setting-label">Color scheme</legend><div class="m-swatches">""");
        foreach (var (name, hex) in o.ColorPresets)
        {
            var css = ThemeCss.BuildForPrimary(o, hex);
            sb.Append($"""<label class="m-swatch" title="{E(Capitalize(name))}"><input type="radio" name="primary" value="{E(name)}" data-css="{E(css)}"{(name == preset ? " checked" : "")} />""")
              .Append($"""<span class="m-swatch-color" style="background:{E(hex)}">{Icon("check", "m-swatch-check")}</span><span class="m-sr-only">{E(Capitalize(name))}</span></label>""");
        }
        sb.Append("</div></fieldset>");

        // Each base previews its own surfaces, light on the left and dark on the right.
        sb.Append("""<fieldset class="m-setting"><legend class="m-setting-label">Theme base</legend><div class="m-choices m-choices-base">""");
        var currentBase = Lower(o.Base);
        foreach (var (value, text, hint, palette) in BasePalettes)
        {
            sb.Append($"""<label class="m-choice m-base-choice" title="{E(text)}: {E(hint)}"><input type="radio" name="base" value="{value}"{(value == currentBase ? " checked" : "")} />""")
              .Append($"""<span class="m-choice-box"><span class="m-base-preview" style="{palette}" aria-hidden="true"><i><b></b><b></b></i><i><b></b><b></b></i></span>""")
              .Append($"""<span class="m-base-text"><span class="m-base-name">{E(text)}</span><span class="m-base-hint">{E(hint)}</span></span></span></label>""");
        }
        sb.Append("</div></fieldset>");

        Choice(sb, "Density", "density", Lower(o.Density), [("compact", "Compact", Icon("unordered-list")), ("comfortable", "Comfortable", Icon("tb:list-details"))]);
        Choice(sb, "Font family", "font", Lower(o.Font),
        [
            ("sans", "Inter", """<span class="m-font-preview m-font-sans">Aa</span>"""),
            ("system", "System", """<span class="m-font-preview m-font-system">Aa</span>"""),
            ("serif", "Serif", """<span class="m-font-preview m-font-serif">Aa</span>"""),
            ("mono", "Mono", """<span class="m-font-preview m-font-mono">Aa</span>"""),
        ], "m-choices-compact");
        Choice(sb, "Corner radius", "radius", o.BorderRadius.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new[] { 0, 4, 6, 8, 12 }.Select(r => (r.ToString(System.Globalization.CultureInfo.InvariantCulture), r.ToString(System.Globalization.CultureInfo.InvariantCulture),
                (string?)$"""<span class="m-radius-preview" style="border-start-start-radius:{r * 1.5}px"></span>""")).ToArray(), "m-choices-radius");
        Languages(sb, o);
        sb.Append("""</div><div class="m-setting-pane" id="m-setting-pane-layout" role="tabpanel" aria-labelledby="m-setting-tab-layout" hidden>""");

        var pick = LayoutPick.From(o);
        sb.Append("""<fieldset class="m-setting"><legend class="m-setting-label">Presets</legend><div class="m-choices m-choices-presets">""");
        foreach (var (value, text, layout) in LayoutPresets)
        {
            sb.Append($"""<label class="m-choice" title="{E(text)}"><input type="radio" name="preset" value="{value}" data-preset="{E(layout.Json())}"{(layout == pick ? " checked" : "")} />""")
              .Append("""<span class="m-choice-box">""").Append(Mini(layout)).Append($"<span>{E(text)}</span></span></label>");
        }
        sb.Append("</div></fieldset>");

        var basePick = new LayoutPick();
        var vertical = basePick with { Layout = "vertical" };
        Choice(sb, "Navigation", "layout", Lower(o.Layout),
            [("horizontal", "Horizontal", Mini(basePick)), ("condensed", "Condensed", Mini(basePick with { Layout = "condensed" })), ("vertical", "Vertical", Mini(vertical))]);
        Choice(sb, "Content width", "width", Lower(o.ContentWidth),
            [("boxed", "Boxed", Mini(basePick)), ("fluid", "Fluid", Mini(basePick with { Width = "fluid" })), ("framed", "Framed", Mini(basePick with { Width = "framed" }))]);
        Choice(sb, "Header", "header", Lower(o.Header),
            Enum.GetValues<HeaderMode>().Select(m => (Lower(m), m.ToString(), (string?)Mini(basePick with { Header = Lower(m) }))).ToArray(), "m-choices-4");
        Choice(sb, "Header color", "topbar", Lower(o.HeaderTheme),
            [("light", "Light", Mini(basePick)), ("dark", "Dark", Mini(basePick with { Topbar = "dark" }))]);
        Choice(sb, "Menu color", "nav", Lower(o.NavTheme), [("light", "Light", Icon("sun")), ("dark", "Dark", Icon("moon"))]);
        Choice(sb, "Sidebar position", "side", Lower(o.SidebarPosition),
            [("left", "Start", Mini(vertical)), ("right", "End", Mini(vertical with { Side = "right" }))], "m-setting-vertical");
        Choice(sb, "Sidebar style", "sidebar", Lower(o.SidebarStyle),
            Enum.GetValues<SidebarStyle>().Select(m => (Lower(m), m.ToString(), (string?)Mini(vertical with { Sidebar = Lower(m) }))).ToArray(), "m-setting-vertical");
        Choice(sb, "Sidebar size", "fold", Lower(o.SidebarFold),
            [("full", "Full", Mini(vertical)), ("folded", "Folded", Mini(vertical with { Fold = "folded" })), ("hover", "Folded hover", Mini(vertical with { Fold = "hover" }))], "m-setting-vertical");
        Choice(sb, "Menu", "menu", Lower(o.MenuScope), [("all", "All menus", Thumb("menu-all")), ("app", "Current app", Thumb("menu-app"))]);
        Choice(sb, "Direction", "dir", Lower(o.Direction),
            [("ltr", "Left to right", Mini(basePick)), ("rtl", "Right to left", Mini(basePick with { Dir = "rtl" }))]);

        sb.Append("""</div></form><div class="m-offcanvas-footer m-customizer-footer"><button type="button" class="ant-btn ant-btn-default" data-m-customizer-reset>""").Append(Icon("reload")).Append("""<span>Reset</span></button>""")
          .Append("""<button type="button" class="ant-btn ant-btn-primary" data-m-customizer-close><span>Done</span></button></div>""")
          .Append("</aside></div>");
        return sb.ToString();
    }

    // The language picker: a native select, since the list can be long. Changing it writes the culture cookie and reloads
    // (modulus-shell.js); the page comes back in the request culture.
    private static void Languages(StringBuilder sb, ModulusThemeOptions o)
    {
        var cultures = ModulusCulture.Supported(o.Cultures);
        if (cultures.Count < 2) return;
        var current = ModulusCulture.Match(cultures, System.Globalization.CultureInfo.CurrentUICulture)!;
        sb.Append("""<div class="m-setting"><label class="m-setting-label" for="m-setting-culture">Language</label>""")
          .Append($"""<select class="ant-input m-setting-select" id="m-setting-culture" name="culture" data-m-culture data-cookie="{E(ModulusCulture.CookieName)}">""");
        foreach (var c in cultures)
            sb.Append($"""<option value="{E(c.Name)}" lang="{E(c.Name)}"{(c.Name == current.Name ? " selected" : "")}>{E(ModulusCulture.NativeName(c))}</option>""");
        sb.Append("""</select><p class="m-setting-hint">Dates, numbers and component texts. The page reloads.</p></div>""");
    }

    // visual: markup above the label (an icon, a preview); trusted, built by this class.
    private static void Choice(StringBuilder sb, string label, string name, string current, (string Value, string Text, string? Visual)[] options, string? cssClass = null)
    {
        var fieldset = cssClass is not null && cssClass.StartsWith("m-setting-", StringComparison.Ordinal);
        sb.Append($"""<fieldset class="m-setting{(fieldset ? " " + cssClass : "")}"><legend class="m-setting-label">{E(label)}</legend>""")
          .Append(fieldset ? """<p class="m-setting-note">Vertical layout only.</p>""" : "")
          .Append($"""<div class="m-choices{(cssClass is null || fieldset ? "" : " " + cssClass)}">""");
        foreach (var (value, text, visual) in options)
        {
            sb.Append($"""<label class="m-choice"><input type="radio" name="{E(name)}" value="{E(value)}"{(value == current ? " checked" : "")} />""")
              .Append("""<span class="m-choice-box">""").Append(visual ?? "").Append($"<span>{E(text)}</span></span></label>");
        }
        sb.Append("</div></fieldset>");
    }

    // Theme bases with their light and dark surfaces (modulus.css), for the previews in the theme settings.
    private static readonly (string Value, string Text, string Hint, string Palette)[] BasePalettes =
    [
        ("gray", "Gray", "Balanced", Palette("#f6f8fb", "#e5e7eb", "#9ca3af", "#111827", "#1a2332", "#2e3c51")),
        ("slate", "Slate", "Cool blue", Palette("#f4f6fa", "#e2e8f0", "#94a3b8", "#0b1120", "#111a2e", "#26344b")),
        ("zinc", "Zinc", "Crisp", Palette("#f6f6f7", "#e4e4e7", "#a1a1aa", "#111113", "#18181b", "#2e2e33")),
        ("neutral", "Neutral", "True gray", Palette("#f6f6f6", "#e5e5e5", "#a3a3a3", "#101010", "#171717", "#2e2e2e")),
        ("stone", "Stone", "Warm", Palette("#f7f6f4", "#e7e5e4", "#a8a29e", "#12100f", "#1c1917", "#34302c")),
    ];

    private static string Palette(string lightBg, string lightLine, string lightInk, string darkBg, string darkCard, string darkLine) =>
        $"--bp-l-bg:{lightBg};--bp-l-line:{lightLine};--bp-l-ink:{lightInk};--bp-d-bg:{darkBg};--bp-d-card:{darkCard};--bp-d-line:{darkLine}";

    /// <summary>The layout settings a preset sets; every other setting keeps its value.</summary>
    private sealed record LayoutPick(string Layout = "horizontal", string Width = "boxed", string Side = "left", string Sidebar = "default",
        string Fold = "full", string Header = "fixed", string Topbar = "light", string Dir = "ltr")
    {
        public static LayoutPick From(ModulusThemeOptions o) => new(Lower(o.Layout), Lower(o.ContentWidth), Lower(o.SidebarPosition),
            Lower(o.SidebarStyle), Lower(o.SidebarFold), Lower(o.Header), Lower(o.HeaderTheme), Lower(o.Direction));

        public string Json() =>
            $$"""{"layout":"{{Layout}}","width":"{{Width}}","side":"{{Side}}","sidebar":"{{Sidebar}}","fold":"{{Fold}}","header":"{{Header}}","topbar":"{{Topbar}}","dir":"{{Dir}}"}""";
    }

    /// <summary>Named layouts offered as one-click presets (the Tabler / Vuexy demo layouts).</summary>
    private static readonly (string Value, string Text, LayoutPick Pick)[] LayoutPresets =
    [
        ("horizontal", "Horizontal", new()),
        ("condensed", "Condensed", new() { Layout = "condensed" }),
        ("fluid", "Fluid", new() { Width = "fluid" }),
        ("boxed", "Boxed", new() { Width = "framed" }),
        ("navbar-dark", "Navbar dark", new() { Topbar = "dark" }),
        ("navbar-sticky", "Navbar sticky", new() { Header = "sticky" }),
        ("navbar-floating", "Navbar floating", new() { Header = "floating" }),
        ("vertical", "Vertical", new() { Layout = "vertical" }),
        ("fluid-vertical", "Fluid vertical", new() { Layout = "vertical", Width = "fluid" }),
        ("right-vertical", "Right vertical", new() { Layout = "vertical", Side = "right" }),
        ("vertical-transparent", "Vertical transparent", new() { Layout = "vertical", Sidebar = "transparent" }),
        ("vertical-floating", "Vertical floating", new() { Layout = "vertical", Sidebar = "floating" }),
        ("folded", "Folded sidebar", new() { Layout = "vertical", Fold = "folded" }),
        ("folded-hover", "Folded hover", new() { Layout = "vertical", Fold = "hover" }),
        ("rtl", "RTL mode", new() { Dir = "rtl" }),
    ];

    // Miniature of a layout (72 x 46), coloured by modulus-shell.css (.m-mini-*) so it follows the theme.
    private static string Mini(LayoutPick p)
    {
        var sb = new StringBuilder("""<svg class="m-mini" viewBox="0 0 72 46" aria-hidden="true" focusable="false">""");
        if (p.Dir == "rtl") sb.Append("""<g transform="matrix(-1 0 0 1 72 0)">""");
        double x0 = 0, x1 = 72;
        if (p.Width == "framed")
        {
            Rect(sb, 0, 0, 72, 46, "m-mini-backdrop");
            (x0, x1) = (8, 64);
        }
        Rect(sb, x0, 0, x1 - x0, 46, "m-mini-bg");

        double cx0 = x0, cx1 = x1;
        if (p.Layout == "vertical")
        {
            var floating = p.Sidebar == "floating";
            var width = p.Fold == "full" ? 16 : 7;
            var inset = floating ? 2 : 0;
            var right = p.Side == "right";
            var sx = right ? x1 - inset - width : x0 + inset;
            var surface = p.Sidebar == "transparent" ? "m-mini-bg" : "m-mini-nav";
            Rect(sb, sx, inset, width, 46 - 2 * inset, surface, floating ? 2 : 0);
            if (p.Sidebar == "default") Rect(sb, right ? sx : sx + width - 0.5, 0, 0.5, 46, "m-mini-edge");
            if (p.Fold == "hover") Rect(sb, right ? sx + width - 16 : sx, inset + 0.5, 16, 45 - 2 * inset, "m-mini-ghost", 1.5);
            Rect(sb, sx + (width - 4) / 2.0, inset + 3, 4, 4, "m-mini-accent", 1);
            for (var i = 0; i < 4; i++)
            {
                var y = inset + 11 + i * 5;
                if (p.Fold == "full") Rect(sb, sx + 3, y, i == 0 ? 10 : 8, 2, i == 0 ? "m-mini-accent" : "m-mini-navline", 1);
                else Rect(sb, sx + 2.5, y, 2, 2, i == 0 ? "m-mini-accent" : "m-mini-navline", 0.5);
            }
            if (right) cx1 = sx; else cx0 = sx + width;
        }

        var floatingHeader = p.Header == "floating";
        var horizontal = p.Layout == "horizontal";
        double hx0 = cx0 + (floatingHeader ? 2 : 0), hx1 = cx1 - (floatingHeader ? 2 : 0), hy = floatingHeader ? 2 : 0;
        var headerHeight = horizontal ? 11 : 7;
        var topbar = p.Topbar == "dark" ? "m-mini-dark" : "m-mini-surface";
        Rect(sb, hx0, hy, hx1 - hx0, headerHeight, "m-mini-surface", floatingHeader ? 1.5 : 0);
        Rect(sb, hx0, hy, hx1 - hx0, 7, topbar, floatingHeader ? 1.5 : 0);
        if (horizontal) Rect(sb, hx0, hy + 7, hx1 - hx0, 4, "m-mini-nav", floatingHeader ? 1.5 : 0);
        if (!floatingHeader && p.Header != "static") Rect(sb, hx0, hy + (p.Header == "sticky" || !horizontal ? headerHeight : 7), hx1 - hx0, 0.75, "m-mini-shadow");
        if (floatingHeader) Rect(sb, hx0, hy, hx1 - hx0, headerHeight, "m-mini-outline", 1.5);
        var line = p.Topbar == "dark" ? "m-mini-darkline" : "m-mini-line";
        if (p.Layout != "vertical") Rect(sb, hx0 + 3, hy + 2, 3, 3, "m-mini-accent", 0.75);
        Circle(sb, hx1 - 4, hy + 3.5, 1.5, line);
        if (p.Layout == "condensed")
            for (var i = 0; i < 3; i++) Rect(sb, hx0 + 9 + i * 7, hy + 3, 5, 1.2, i == 0 ? "m-mini-accent" : line, 0.6);
        if (horizontal)
            for (var i = 0; i < 4; i++) Rect(sb, hx0 + 3 + i * 7, hy + 8.4, 5, 1.2, i == 0 ? "m-mini-accent" : "m-mini-navline", 0.6);

        var gutter = p.Width == "boxed" && p.Layout != "vertical" ? 8 : 3;
        double top = hy + headerHeight + 4, left = cx0 + gutter, width2 = cx1 - cx0 - 2 * gutter;
        Rect(sb, left, top, width2, 46 - top - 3, "m-mini-card", 1.5);
        Rect(sb, left + 3, top + 3, width2 * 0.45, 1.6, "m-mini-line", 0.8);
        Rect(sb, left + 3, top + 7, width2 * 0.7, 1.2, "m-mini-line", 0.6);
        Rect(sb, left + 3, top + 10.5, width2 * 0.3, 1.2, "m-mini-line", 0.6);
        if (p.Dir == "rtl") sb.Append("</g>");
        return sb.Append("</svg>").ToString();

        static void Rect(StringBuilder sb, double x, double y, double w, double h, string cls, double r = 0) =>
            sb.Append(System.FormattableString.Invariant($"""<rect class="{cls}" x="{x}" y="{y}" width="{w}" height="{h}"{(r > 0 ? $" rx=\"{r}\"" : "")}/>"""));

        static void Circle(StringBuilder sb, double x, double y, double r, string cls) =>
            sb.Append(System.FormattableString.Invariant($"""<circle class="{cls}" cx="{x}" cy="{y}" r="{r}"/>"""));
    }

    // Miniature of a layout, drawn by modulus-shell.css.
    private static string Thumb(string kind) => $"""<span class="m-thumb m-thumb-{kind}" aria-hidden="true"><i></i><i></i><i></i></span>""";

    private static string Lower<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();

}
