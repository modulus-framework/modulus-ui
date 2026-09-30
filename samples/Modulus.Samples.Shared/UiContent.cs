using Modulus.Ui.Abstractions.Shell;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.Samples.Shared;

// Reference pages (icons, colours, changelog, users grid) -------------------------------

/// <summary>An icon theme tab of the Icons page.</summary>
public sealed record IconTheme(string Key, string Label, int Value, string Enum);

/// <summary>A page of the Icons gallery: the search, the theme and the names on this page.</summary>
public sealed record IconGallery(string? Query, IconTheme Theme, int Page, int PageSize, int Total, IReadOnlyList<string> Names)
{
    public int Pages => Math.Max(1, (Total + PageSize - 1) / PageSize);

    /// <summary>The query string that keeps the search and theme (without the page), for links and pagination.</summary>
    public string QueryString(string? theme = null)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Query)) parts.Add("q=" + Uri.EscapeDataString(Query));
        var t = theme ?? Theme.Key;
        if (t != "outline") parts.Add("theme=" + t);
        return string.Join("&", parts);
    }

    public string BlazorSnippet(string name) =>
        Theme.Enum == "Outline" ? $"<Icon Type=\"{name}\" />" : $"<Icon Type=\"{name}\" Theme=\"IconThemeType.{Theme.Enum}\" />";

    public string MvcSnippet(string name) =>
        Theme.Enum == "Outline" ? $"<app-icon type=\"{name}\" />" : $"<app-icon type=\"{name}\" theme=\"{Theme.Enum}\" />";
}

/// <summary>A preset colour of the Ant Design palette, with its 10 shades (index 6 is the seed).</summary>
public sealed record PaletteColor(string Key, string Label, string Seed, IReadOnlyList<PaletteShade> Shades);

/// <summary>A shade of a palette colour; <paramref name="DarkText"/> when it is light enough for dark text.</summary>
public sealed record PaletteShade(int Index, string Hex, bool DarkText);

/// <summary>A theme colour token (<c>--m-*</c>) and what it is for.</summary>
public sealed record ColorToken(string Label, string Variable, string Hex, string Use);

public enum ChangeKind { Added, Changed, Fixed, Removed }

public sealed record ChangeEntry(ChangeKind Kind, string Text);

/// <summary>A release on the Changelog page.</summary>
public sealed record Release(string Version, DateOnly Date, string Title, string Summary, IReadOnlyList<ChangeEntry> Changes, bool Major = false);

/// <summary>A page of the users grid: search, role filter (with counts) and the cards on this page.</summary>
public sealed record UserGridPage(string? Query, string? Role, int Page, int PageSize, int Total, IReadOnlyList<AppUser> Users,
    IReadOnlyList<(string Role, int Count)> Roles)
{
    public const int Size = 12;

    public int Pages => Math.Max(1, (Total + PageSize - 1) / PageSize);

    /// <summary>The query string for a role chip (null = all roles) that keeps the search; the page restarts.</summary>
    public string QueryString(string? role)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Query)) parts.Add("q=" + Uri.EscapeDataString(Query));
        if (!string.IsNullOrWhiteSpace(role)) parts.Add("role=" + Uri.EscapeDataString(role));
        return string.Join("&", parts);
    }

    /// <summary>The query string that keeps the search and role (without the page), for pagination.</summary>
    public string Filters => QueryString(Role);

    public static UserGridPage Load(SampleStore store, string? q, string? role, int page)
    {
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        role = Lookups.Roles.FirstOrDefault(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
        var matches = store.Users().Where(u => q is null
            || u.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || u.Email.Contains(q, StringComparison.OrdinalIgnoreCase)
            || u.Company.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        var roles = Lookups.Roles.Select(r => (r, matches.Count(u => u.Role == r))).ToList();
        var filtered = matches.Where(u => role is null || u.Role == role).OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var pages = Math.Max(1, (filtered.Count + Size - 1) / Size);
        page = Math.Clamp(page, 1, pages);
        return new(q, role, page, Size, filtered.Count, filtered.Skip((page - 1) * Size).Take(Size).ToList(), roles);
    }
}

/// <summary>Content of the reference pages (Icons, Typography, Colours, Changelog), shared by the three samples.</summary>
public static class UiContent
{
    public const int IconPageSize = 120;

    public static readonly IReadOnlyList<IconTheme> IconThemes =
    [
        new("outline", "Outlined", AntIconSvg.Outline, "Outline"),
        new("fill", "Filled", AntIconSvg.Fill, "Fill"),
        new("twotone", "Two tone", AntIconSvg.TwoTone, "TwoTone"),
    ];

    /// <summary>A page of Ant Design icons of a theme, filtered by name.</summary>
    public static IconGallery Icons(string? q, string? theme, int page)
    {
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var t = IconThemes.FirstOrDefault(x => string.Equals(x.Key, theme, StringComparison.OrdinalIgnoreCase)) ?? IconThemes[0];
        var names = AntIconSvg.Names(t.Value).Where(n => q is null || n.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        var pages = Math.Max(1, (names.Count + IconPageSize - 1) / IconPageSize);
        page = Math.Clamp(page, 1, pages);
        return new(q, t, page, IconPageSize, names.Count, names.Skip((page - 1) * IconPageSize).Take(IconPageSize).ToList());
    }

    private static PaletteColor Preset(string key, string label, string seed)
    {
        var shades = AntPalette.Generate(AntColor.Parse(seed)).Select((c, i) =>
        {
            var r = c.Rounded();
            return new PaletteShade(i + 1, r.ToHex(), 0.299 * r.R + 0.587 * r.G + 0.114 * r.B > 150);
        }).ToList();
        return new(key, label, seed, shades);
    }

    /// <summary>The 12 Ant Design preset colours.</summary>
    public static readonly IReadOnlyList<PaletteColor> Palette =
    [
        Preset("blue", "Daybreak Blue", "#1890ff"),
        Preset("geekblue", "Geek Blue", "#2f54eb"),
        Preset("purple", "Golden Purple", "#722ed1"),
        Preset("magenta", "Magenta", "#eb2f96"),
        Preset("red", "Dust Red", "#f5222d"),
        Preset("volcano", "Volcano", "#fa541c"),
        Preset("orange", "Sunset Orange", "#fa8c16"),
        Preset("gold", "Calendula Gold", "#faad14"),
        Preset("yellow", "Sunrise Yellow", "#fadb14"),
        Preset("lime", "Lime", "#a0d911"),
        Preset("green", "Polar Green", "#52c41a"),
        Preset("cyan", "Cyan", "#13c2c2"),
    ];

    /// <summary>Semantic colours of the theme (light values; the page reads the live value from the variable).</summary>
    public static readonly IReadOnlyList<ColorToken> Semantic =
    [
        new("Primary", "--m-primary", "#1677ff", "Main actions, links, selection"),
        new("Success", "--m-success", "#52c41a", "Completed, healthy, positive change"),
        new("Warning", "--m-warning", "#faad14", "Needs attention, pending"),
        new("Error", "--m-error", "#ff4d4f", "Failures, destructive actions"),
        new("Info", "--m-info", "#1677ff", "Neutral notices and hints"),
    ];

    /// <summary>Neutral colours of the theme: text, borders and backgrounds.</summary>
    public static readonly IReadOnlyList<ColorToken> Neutrals =
    [
        new("Heading", "--m-heading", "#1f1f1f", "Titles and headings"),
        new("Text", "--m-text", "#262626", "Body text"),
        new("Secondary text", "--m-text-secondary", "#8c8c8c", "Hints, captions, metadata"),
        new("Border", "--m-border", "#d9d9d9", "Inputs and outlined controls"),
        new("Split", "--m-split", "#f0f0f0", "Dividers and card borders"),
        new("Subtle background", "--m-bg-subtle", "#fafafa", "Table headers, wells"),
    ];

    public static string KindTone(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => "green",
        ChangeKind.Changed => "blue",
        ChangeKind.Fixed => "orange",
        _ => "red",
    };

    public static string KindIcon(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => "plus-circle",
        ChangeKind.Changed => "sync",
        ChangeKind.Fixed => "tool",
        _ => "minus-circle",
    };

    /// <summary>Release notes of the template, newest first.</summary>
    public static readonly IReadOnlyList<Release> Changelog =
    [
        new("1.4.0", new(2026, 9, 21), "Plugins and reference pages",
            "Twelve plugin pages, Ant Design icon gallery, typography and colour references, and a card view of the users.",
            [
                new(ChangeKind.Added, "Plugins menu: media player, WYSIWYG editor, maps, vector maps and sortable lists"),
                new(ChangeKind.Added, "Icons page listing every Ant Design icon, with search, themes and copy-to-clipboard"),
                new(ChangeKind.Added, "Typography, Colours and Changelog pages"),
                new(ChangeKind.Added, "Users grid view with role filters"),
                new(ChangeKind.Changed, "The brand colour is kept in dark mode, with its dark palette"),
                new(ChangeKind.Fixed, "Dropdown button, Affix and BackTop menus in MVC and Razor Pages now match Blazor"),
            ], Major: true),
        new("1.3.2", new(2026, 8, 30), "Form polish",
            "Fixes to text areas, popovers and date ranges in the server-rendered flavours.",
            [
                new(ChangeKind.Fixed, "Text areas drew a double border inside form items"),
                new(ChangeKind.Fixed, "Popovers had no background in MVC and Razor Pages"),
                new(ChangeKind.Fixed, "Range picker panels now match the Blazor layout"),
                new(ChangeKind.Changed, "Kanban card actions use the same menu in all flavours"),
            ]),
        new("1.3.0", new(2026, 8, 4), "Business components",
            "Heatmap, treemap, pareto, waterfall, bullet, gauge and funnel charts, business form fields and an ERP dashboard.",
            [
                new(ChangeKind.Added, "Seven business chart types, with zoom, legend toggles and drill-down"),
                new(ChangeKind.Added, "Money, percentage, phone and address fields"),
                new(ChangeKind.Added, "Lightbox, column search and multi-column sort"),
                new(ChangeKind.Added, "ERP dashboard sample"),
                new(ChangeKind.Fixed, "Editable grid prices depended on the machine culture"),
            ], Major: true),
        new("1.2.0", new(2026, 6, 16), "Tables",
            "Editable and nested grids share the data table design.",
            [
                new(ChangeKind.Added, "Expandable rows and resizable columns in every grid"),
                new(ChangeKind.Changed, "Editable and nested grids use the data table toolbar and density menu"),
                new(ChangeKind.Removed, "The old simple grid; use the data table instead"),
            ]),
        new("1.1.0", new(2026, 5, 2), "Extra pages",
            "Activity, billing, logs, music, uptime and more, plus layout settings.",
            [
                new(ChangeKind.Added, "22 extra pages in every flavour"),
                new(ChangeKind.Added, "Layout settings drawer: menu position, width, density and theme"),
                new(ChangeKind.Fixed, "Gallery category pill lost its shadow under the header"),
            ]),
        new("1.0.0", new(2026, 3, 18), "First release",
            "One Ant Design admin template for Blazor, MVC and Razor Pages.",
            [
                new(ChangeKind.Added, "Shared shell: sidebar, header, search, notifications and theme switcher"),
                new(ChangeKind.Added, "Dashboards, apps (mail, chat, calendar, kanban, invoices, users) and auth pages"),
                new(ChangeKind.Added, "Tag helpers that render the Ant Design markup for MVC and Razor Pages"),
            ], Major: true),
    ];
}
