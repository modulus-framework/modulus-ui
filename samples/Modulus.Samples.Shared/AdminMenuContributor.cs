using Modulus.Ui.Abstractions.Navigation;

namespace Modulus.Samples.Shared;

/// <summary>
/// Main menu for every sample app (Blazor, MVC, Razor Pages) — one contributor, three flavors.
/// Organised like Odoo: each top-level item is an app of the launcher (SampleShellContentProvider), so every page
/// belongs to one, and the app's children are its menu bar (Orders ▾, Products ▾, …). In a menu, plain entries come
/// first, then sections: a section is a bold heading with its entries indented under it, and a group inside a
/// section folds open in place. Icons are Ant Design icon names.
/// </summary>
public sealed class AdminMenuContributor : IMenuContributor
{
    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        context
            .Add(Group("dashboards", "Overview", "dashboard", 0,
                Link("dashboard-analytics", "Analytics", "", "line-chart", exact: true),
                Link("dashboard-crm", "CRM", "dashboards/crm", "ri:group"),
                Link("dashboard-ecommerce", "eCommerce", "dashboards/ecommerce", "shopping-cart"),
                Link("dashboard-erp", "ERP analytics", "dashboards/erp", "ri:building")))
            .Add(Group("sales", "Sales", "shopping-cart", 10,
                Section("sales-orders", "Orders",
                    Link("orders", "Orders", "apps/ecommerce/orders", "truck"),
                    Link("customers", "Customers", "apps/customers", "team")),
                Section("sales-catalog", "Products",
                    Link("products", "Products", "apps/ecommerce/products", "product"))))
            .Add(Group("invoicing", "Invoicing", "file-text", 20,
                Section("invoicing-customers", "Customers",
                    Link("invoice-list", "Invoices", "apps/invoice/list", "file-text", also: ["apps/invoice/preview", "apps/invoice/edit"]),
                    Link("invoice-add", "Create invoice", "apps/invoice/add", "account-book"))))
            .Add(Group("workspace", "Productivity", "ri:briefcase", 30,
                Link("email", "Email", "apps/email", "mail"),
                Link("chat", "Discuss", "apps/chat", "comment"),
                Link("calendar", "Calendar", "apps/calendar", "calendar"),
                Link("kanban", "Project", "apps/kanban", "project")))
            .Add(Group("settings", "Settings", "setting", 40,
                Link("account-settings", "General settings", "pages/account-settings", "setting"),
                Section("access", "Users & companies",
                    Link("users", "Users", "apps/users/list", "user", also: ["apps/users/view", "apps/users/grid"]),
                    Link("profile", "User profile", "pages/profile", "ri:account-circle"),
                    Section("access-rights", "Access rights",
                        Link("roles", "Roles", "apps/roles", "ri:shield-keyhole"),
                        Link("permissions", "Permissions", "apps/permissions", "key")))))
            .Add(Group("pages", "Website", "global", 50,
                Section("site", "Site",
                    Link("pricing", "Pricing", "pages/pricing", "dollar"),
                    Link("faq", "FAQ", "pages/faq", "question-circle"),
                    Link("help-center", "Help center", "pages/help-center", "ri:lifebuoy"),
                    Link("changelog", "Changelog", "pages/changelog", "history")),
                Section("auth", "Authentication",
                    Link("login", "Sign in", "auth/login", "login"),
                    Link("register", "Sign up", "auth/register", "user-add"),
                    Section("auth-password", "Password",
                        Link("forgot-password", "Forgot password", "auth/forgot-password", "lock"),
                        Link("reset-password", "Reset password", "auth/reset-password", "ri:lock-password")),
                    Section("auth-verify", "Verification",
                        Link("verify-email", "Verify email", "auth/verify-email", "ri:mail-open"),
                        Link("two-steps", "Two-step verification", "auth/two-steps", "ri:lock-2"))),
                Section("misc", "Errors",
                    Link("error-404", "Page not found", "misc/not-found", "warning"),
                    Link("error-403", "Not authorized", "misc/not-authorized", "stop"),
                    Link("error-500", "Server error", "misc/server-error", "bug"),
                    Section("misc-status", "Status pages",
                        Link("maintenance", "Under maintenance", "misc/maintenance", "tool"),
                        Link("coming-soon", "Coming soon", "misc/coming-soon", "rocket")))))
            .Add(Group("extra", "Extra", "ri:sparkling", 55,
                Section("extra-account", "Account & billing",
                    Link("extra-billing", "Billing", "extra/billing", "credit-card"),
                    Link("extra-pay", "Pay", "extra/pay", "wallet"),
                    Section("extra-subscription", "Subscription",
                        Link("extra-settings-plan", "Settings plan", "extra/settings-plan", "crown"),
                        Link("extra-pricing-table", "Pricing table", "extra/pricing-table", "table"),
                        Link("extra-trial-ended", "Trial ended", "extra/trial-ended", "hourglass"),
                        Link("extra-license", "License", "extra/license", "file-protect"))),
                Section("extra-apps", "Apps",
                    Link("extra-jobs", "Job listing", "extra/jobs", "ri:briefcase"),
                    Link("extra-music", "Music", "extra/music", "ri:music-2"),
                    Link("extra-search", "Search results", "extra/search", "search"),
                    Section("extra-work", "Work",
                        Link("extra-activity", "Activity", "extra/activity", "ri:pulse"),
                        Link("extra-tasks", "Tasks", "extra/tasks", "check-square")),
                    Section("extra-monitoring", "Monitoring",
                        Link("extra-logs", "Logs", "extra/logs", "ri:terminal-box"),
                        Link("extra-uptime", "Uptime monitor", "extra/uptime", "ri:heart-pulse"))),
                Section("extra-media", "Media",
                    Link("extra-gallery", "Gallery", "extra/gallery", "picture"),
                    Link("extra-photogrid", "Photogrid", "extra/photogrid", "appstore"),
                    Link("extra-signatures", "Signatures", "extra/signatures", "signature")),
                Section("extra-blocks", "Page blocks",
                    Link("extra-empty", "Empty page", "extra/empty", "file"),
                    Link("extra-page-loader", "Page loader", "extra/page-loader", "loading"),
                    Link("extra-cookie-banner", "Cookie banner", "extra/cookie-banner", "ri:cookie"),
                    Section("extra-content", "Content",
                        Link("extra-widgets", "Widgets", "extra/widgets", "block"),
                        Link("extra-text-features", "Text features", "extra/text-features", "ri:article"),
                        Link("extra-marketing", "Marketing", "extra/marketing", "fund-projection-screen")))))
            .Add(Group("plugins", "Plugins", "api", 58,
                Section("plugins-data", "Data",
                    Link("plugins-charts", "Charts", "ui/charts", "pie-chart", exact: true),
                    Link("plugins-charts-advanced", "Charts advanced", "plugins/charts-advanced", "fund"),
                    Link("plugins-datatables", "Datatables", "tables/advanced", "table", exact: true),
                    Link("plugins-calendar", "Fullcalendar", "apps/calendar", "calendar", exact: true)),
                Section("plugins-inputs", "Inputs",
                    Link("plugins-dropzone", "Dropzone", "plugins/dropzone", "cloud-upload"),
                    Link("plugins-editor", "WYSIWYG editor", "plugins/editor", "edit"),
                    Link("plugins-sortable", "Sortable", "plugins/sortable", "drag"),
                    Section("plugins-pickers", "Pickers",
                        Link("plugins-color-picker", "Color picker", "plugins/color-picker", "bg-colors"),
                        Link("plugins-date-picker", "Date picker", "plugins/date-picker", "schedule"))),
                Section("plugins-ui", "Interface",
                    Link("plugins-lightbox", "Lightbox", "plugins/lightbox", "picture"),
                    Link("plugins-player", "Inline player", "plugins/player", "play-square"),
                    Link("plugins-tour", "Tour", "plugins/tour", "flag")),
                Section("plugins-maps", "Maps",
                    Link("plugins-map", "Map", "plugins/map", "environment"),
                    Link("plugins-map-fullsize", "Map fullsize", "plugins/map-fullsize", "expand"),
                    Link("plugins-map-vector", "Map vector", "plugins/map-vector", "global"))))
            .Add(Group("developer", "Developer", "code", 60,
                Section("ui", "Interface",
                    Link("components", "Components", "ui/components", "ri:box-3"),
                    Link("cards", "Cards", "ui/cards", "switcher"),
                    Link("charts", "Charts", "ui/charts", "pie-chart"),
                    Section("ui-foundations", "Foundations",
                        Link("icons", "Icons", "ui/icons", "appstore"),
                        Link("typography", "Typography", "ui/typography", "font-size"),
                        Link("colors", "Colours", "ui/colors", "bg-colors"))),
                Section("forms", "Forms",
                    Link("form-layouts", "Form layouts", "forms/layouts", "layout"),
                    Link("form-wizard", "Wizard", "forms/wizard", "ri:list-check-2"),
                    Link("form-validation", "Validation", "forms/validation", "check-square"),
                    Link("form-business", "Business fields", "forms/business", "form")),
                Section("tables", "Tables",
                    Link("tables", "Data table", "tables/advanced", "table"),
                    Link("editable-grid", "Editable grid", "tables/editable", "ri:table"),
                    Link("nested-grid", "Nested grid", "tables/nested", "ri:stack"),
                    Link("large-data", "Large data", "tables/large", "database"),
                    Link("pivot", "Pivot", "tables/pivot", "border-inner"))));
        return Task.CompletedTask;
    }

    private static MenuItemDefinition Link(string key, string label, string href, string icon, bool exact = false, string[]? also = null) =>
        new(key, label, href, icon) { ExactMatch = exact, ActivePaths = also ?? [] };

    private static MenuItemDefinition Section(string key, string label, params MenuItemDefinition[] items)
    {
        var section = new MenuItemDefinition(key, label);
        section.Children.AddRange(items);
        return section;
    }

    private static MenuItemDefinition Group(string key, string label, string icon, int order, params MenuItemDefinition[] items)
    {
        var group = new MenuItemDefinition(key, label, null, icon) { Order = order };
        group.Children.AddRange(items);
        return group;
    }
}
