using Modulus.Ui.Abstractions.Navigation;

namespace Modulus.Ui.Abstractions.Admin;

/// <summary>The admin pages the scaffolds create. The admin module shows a menu entry only for the pages you select.</summary>
[Flags]
public enum AdminPages
{
    None = 0,
    Dashboard = 1 << 0,
    Users = 1 << 1,
    Roles = 1 << 2,
    Tenants = 1 << 3,
    Settings = 1 << 4,
    Audit = 1 << 5,
    Analytics = 1 << 6,
    Reports = 1 << 7,
    Health = 1 << 8,
    Profile = 1 << 9,
    Notifications = 1 << 10,
    NotificationPreferences = 1 << 11,
    All = (1 << 12) - 1,
}

/// <summary>Options of the premade admin module (<c>AddModulusAdminUi</c>).</summary>
public sealed class AdminModuleOptions
{
    /// <summary>The pages that get a menu entry. Default: all. Set it to the pages you scaffolded.</summary>
    public AdminPages Pages { get; set; } = AdminPages.All;

    /// <summary>
    /// Policy or permission required to see the Settings and System apps (settings, users, roles, tenants, audit, health). Null: visible to
    /// everyone who can reach the page, so protect the pages themselves too (the scaffolds carry [Authorize]).
    /// </summary>
    public string? AdministrationPolicy { get; set; }

    /// <summary>Order of the first app. The apps use this plus 0, 10, 20, 30 so your own menu contributors can sit in between.</summary>
    public int FirstOrder { get; set; }
}

/// <summary>
/// The navigation of the premade admin module, in the same shape as the sample menus of this framework: each top-level item is
/// an app (Overview, Settings, System, My account) whose children are its menu bar, with bold sections inside. Registered by
/// <c>AddModulusAdminUi()</c>.
/// Routes match the scaffolded pages; entries for pages you did not scaffold are left out through <see cref="AdminModuleOptions.Pages"/>.
/// </summary>
public sealed class AdminMenuContributor(AdminModuleOptions options) : IMenuContributor
{
    /// <summary>The route of each page, relative to the app root (no leading slash).</summary>
    public static class Routes
    {
        public const string Dashboard = "dashboard";
        public const string Users = "admin/users";
        public const string Roles = "admin/roles";
        public const string Tenants = "admin/tenants";
        public const string Settings = "admin/settings";
        public const string Audit = "audit";
        public const string Analytics = "analytics";
        public const string Reports = "reports";
        public const string Health = "system/health";
        public const string Profile = "account/profile";
        public const string Notifications = "notifications";
        public const string NotificationPreferences = "account/notification-preferences";
    }

    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        var first = options.FirstOrder;

        Group(context, "admin-overview", "Overview", "dashboard", first, null,
            Has(AdminPages.Dashboard) ? new MenuItemDefinition("admin-dashboard", "Dashboard", Routes.Dashboard, "dashboard") { ExactMatch = true } : null,
            Has(AdminPages.Analytics) ? Link("admin-analytics", "Analytics", Routes.Analytics, "line-chart") : null,
            Has(AdminPages.Reports) ? Link("admin-reports", "Reports", Routes.Reports, "file-text") : null);

        Group(context, "admin-settings-app", "Settings", "setting", first + 10, options.AdministrationPolicy,
            Has(AdminPages.Settings) ? Link("admin-settings", "General settings", Routes.Settings, "setting") : null,
            Section("admin-access", "Users & access",
                Has(AdminPages.Users) ? Link("admin-users", "Users", Routes.Users, "user") : null,
                Has(AdminPages.Roles) ? Link("admin-roles", "Roles", Routes.Roles, "tb:shield-lock") : null),
            Section("admin-organisation", "Organisation",
                Has(AdminPages.Tenants) ? Link("admin-tenants", "Tenants", Routes.Tenants, "cluster") : null),
            Has(AdminPages.Audit) ? Link("admin-audit", "Audit log", Routes.Audit, "audit") : null);

        Group(context, "admin-system", "System", "monitor", first + 20, options.AdministrationPolicy,
            Has(AdminPages.Health) ? Link("admin-health", "System health", Routes.Health, "heart") : null);

        Group(context, "admin-account", "My account", "user", first + 30, null,
            Has(AdminPages.Profile) ? Link("admin-profile", "Profile", Routes.Profile, "tb:user-circle") : null,
            Has(AdminPages.Notifications) ? Link("admin-notifications", "Notifications", Routes.Notifications, "bell") : null,
            Has(AdminPages.NotificationPreferences) ? Link("admin-notification-preferences", "Notification preferences", Routes.NotificationPreferences, "tb:bell-cog") : null);

        return Task.CompletedTask;
    }

    private bool Has(AdminPages page) => (options.Pages & page) == page;

    private static MenuItemDefinition Link(string key, string label, string href, string icon, string[]? also = null) =>
        new(key, label, href, icon) { ActivePaths = also ?? [] };

    /// <summary>A heading with entries under it; null when none of its entries is selected.</summary>
    private static MenuItemDefinition? Section(string key, string label, params MenuItemDefinition?[] items)
    {
        var children = items.OfType<MenuItemDefinition>().ToList();
        if (children.Count == 0) return null;
        var section = new MenuItemDefinition(key, label);
        section.Children.AddRange(children);
        return section;
    }

    /// <summary>A top-level group; skipped entirely when none of its entries is selected.</summary>
    private static void Group(MenuConfigurationContext context, string key, string label, string icon, int order, string? policy, params MenuItemDefinition?[] items)
    {
        var children = items.OfType<MenuItemDefinition>().ToList();
        if (children.Count == 0) return;
        var group = new MenuItemDefinition(key, label, null, icon) { Order = order, RequiredPolicy = policy };
        group.Children.AddRange(children);
        context.Add(group);
    }
}
