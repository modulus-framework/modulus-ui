using System.Security.Claims;

namespace Modulus.Ui.Abstractions.Shell;

/// <summary>The signed-in user shown in the header.</summary>
public sealed record ShellUser(string Name, string? Subtitle = null, string? AvatarUrl = null);

/// <summary>A header notification. <paramref name="Tone"/> is primary, success, warning or danger.</summary>
public sealed record ShellNotice(string Title, string? Description, string When, bool Unread, string Icon = "bell", string Tone = "primary", string? Href = null);

/// <summary>A link in the user menu or footer. Set <see cref="DividerBefore"/> to separate groups.</summary>
public sealed record ShellLink(string Label, string Href, string? Icon = null)
{
    public bool DividerBefore { get; init; }
}

/// <summary>A tile in the header app switcher. <paramref name="Tone"/> tints the icon (primary, green, orange, …).</summary>
public sealed record ShellApp(string Name, string Href, string Icon, string Tone = "primary", string? Description = null)
{
    /// <summary>
    /// Key of the menu item that holds this app's pages (any depth). While a page under it is open, the app is the
    /// current app: with <see cref="Theming.MenuScope.App"/> the navbar and sidebar show only its menu, as in Odoo.
    /// </summary>
    public string? MenuKey { get; init; }

    /// <summary>
    /// Group of the app ("Sales &amp; finance", "Productivity"). The app switchers file apps under it; the full-screen
    /// launcher keeps a group's apps together in its one grid and finds them by it. Apps keep their order within a
    /// group; groups appear in order of first use.
    /// </summary>
    public string? Group { get; init; }
}

/// <summary>How the header app switcher opens.</summary>
public enum AppLauncherStyle
{
    /// <summary>A dropdown grid of small tiles under the header button (Tabler).</summary>
    Dropdown,
    /// <summary>A full-screen, searchable home menu of large app icons that also finds menu entries (Odoo).</summary>
    Overlay,
}

/// <summary>
/// Scheduled activities of one kind of record (sales orders, invoices, …) in the header activity menu, as in Odoo's
/// systray. <paramref name="Planned"/> counts the activities due after today.
/// </summary>
public sealed record ShellActivity(string Name, string Icon, int Late, int Today, int Planned, string? Href = null, string Tone = "primary");

/// <summary>
/// A tenant (workspace, organisation) in the header tenant switcher. Without <paramref name="Href"/>, choosing it
/// writes the <see cref="ShellRenderer.TenantCookie"/> cookie and reloads the page.
/// </summary>
public sealed record ShellTenant(string Id, string Name, string? Subtitle = null, string? LogoUrl = null, string? Href = null)
{
    /// <summary>Id of the parent tenant: the switcher indents a branch under the tenant listed before it.</summary>
    public string? ParentId { get; init; }
}

/// <summary>Per-request header and footer content: user, notifications, menus.</summary>
public sealed class ShellContent
{
    public ShellUser User { get; set; } = new("Admin");

    /// <summary>Null hides the notification bell.</summary>
    public IReadOnlyList<ShellNotice>? Notices { get; set; }

    /// <summary>"View all" link under the notifications. Null hides it.</summary>
    public string? NoticesHref { get; set; }

    public IReadOnlyList<ShellLink> UserLinks { get; set; } =
    [
        new("Profile", "profile", "user"),
        new("Settings", "settings", "setting"),
    ];

    /// <summary>Sign-out link at the bottom of the user menu. Null hides it.</summary>
    public string? SignOutHref { get; set; } = "logout";

    public IReadOnlyList<ShellLink> FooterLinks { get; set; } = [];

    /// <summary>Tiles of the app switcher (grid icon in the header). Null hides it.</summary>
    public IReadOnlyList<ShellApp>? Apps { get; set; }

    /// <summary>"View all apps" link under the app grid. Null hides it.</summary>
    public string? AppsHref { get; set; }

    /// <summary>Dropdown grid (Tabler) or full-screen home menu (Odoo). The overlay's button sits before the brand.</summary>
    public AppLauncherStyle AppLauncher { get; set; } = AppLauncherStyle.Dropdown;

    /// <summary>Scheduled activities (clock icon with the late and due-today count). Null hides the menu.</summary>
    public IReadOnlyList<ShellActivity>? Activities { get; set; }

    /// <summary>"View all activities" link under the activity menu. Null hides it.</summary>
    public string? ActivitiesHref { get; set; }

    /// <summary>Tenants of the tenant switcher (next to the brand). Null or empty hides it.</summary>
    public IReadOnlyList<ShellTenant>? Tenants { get; set; }

    /// <summary>
    /// Id of the current tenant. Null uses the <see cref="ShellRenderer.TenantCookie"/> cookie
    /// (<see cref="ShellView.TenantId"/>), then the first tenant.
    /// </summary>
    public string? CurrentTenantId { get; set; }

    /// <summary>
    /// Multi-company mode (Odoo): besides switching the current tenant, each row has a checkbox that adds the tenant
    /// to the active set, kept in the <see cref="ShellRenderer.TenantsCookie"/> cookie (comma-separated ids). The
    /// page reloads when the switcher closes after a change; cancel the <c>modulus:tenants-change</c> event to
    /// handle it yourself.
    /// </summary>
    public bool MultiTenant { get; set; }

    /// <summary>
    /// Ids of the active tenants in multi-company mode. Null uses the <see cref="ShellRenderer.TenantsCookie"/>
    /// cookie (<see cref="ShellView.TenantIds"/>). The current tenant is always active.
    /// </summary>
    public IReadOnlyList<string>? ActiveTenantIds { get; set; }

    /// <summary>Links under the tenant list, such as "Manage workspaces".</summary>
    public IReadOnlyList<ShellLink> TenantLinks { get; set; } = [];

    /// <summary>Placeholder of the header search box, which searches the menu. Null hides the search box.</summary>
    public string? SearchPlaceholder { get; set; } = "Search…";
}

/// <summary>
/// Supplies <see cref="ShellContent"/> for the current user. Register your own to show real
/// notifications and account links; the default shows the user's name and a sign-out link.
/// </summary>
public interface IShellContentProvider
{
    Task<ShellContent> GetContentAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

public sealed class DefaultShellContentProvider : IShellContentProvider
{
    public Task<ShellContent> GetContentAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ShellContent { User = new ShellUser(user.Identity?.Name ?? "Admin") });
}
