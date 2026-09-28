using Modulus.Ui.Abstractions.Navigation;

namespace Modulus.Samples.Shared;

/// <summary>
/// Main menu for every sample app (Blazor, MVC, Razor Pages) — one contributor, three flavors.
/// Laid out like an ERP: each top-level item is a module (an app in the launcher), and its sections become the
/// module's own menus (Orders ▾, Products ▾) in the app menu bar, as in Odoo. A child with children is a section
/// (header plus links). Icons are Tabler Icons names.
/// </summary>
public sealed class AdminMenuContributor : IMenuContributor
{
    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        context
            .Add(Group("dashboards", "Overview", "layout-dashboard", 0,
                Link("dashboard-analytics", "Analytics", "", "chart-line", exact: true),
                Link("dashboard-crm", "CRM", "dashboards/crm", "users-group"),
                Link("dashboard-ecommerce", "eCommerce", "dashboards/ecommerce", "shopping-cart"),
                Link("dashboard-erp", "ERP analytics", "dashboards/erp", "building")))
            .Add(Group("sales", "Sales", "shopping-cart", 10,
                Section("sales-orders", "Orders",
                    Link("orders", "Orders", "apps/ecommerce/orders", "truck-delivery"),
                    Link("customers", "Customers", "apps/customers", "users")),
                Section("sales-catalog", "Products",
                    Link("products", "Products", "apps/ecommerce/products", "package"))))
            .Add(Group("invoicing", "Invoicing", "file-invoice", 20,
                Section("invoicing-customers", "Customers",
                    Link("invoice-list", "Invoices", "apps/invoice/list", "file-invoice", also: ["apps/invoice/preview", "apps/invoice/edit"]),
                    Link("invoice-add", "Create invoice", "apps/invoice/add", "file-dollar"))))
            .Add(Group("workspace", "Productivity", "briefcase", 30,
                Link("email", "Email", "apps/email", "mail"),
                Link("chat", "Discuss", "apps/chat", "messages"),
                Link("calendar", "Calendar", "apps/calendar", "calendar"),
                Link("kanban", "Project", "apps/kanban", "layout-kanban")))
            .Add(Group("settings", "Settings", "settings", 40,
                Section("access", "Users & companies",
                    Link("users", "Users", "apps/users/list", "user", also: ["apps/users/view"]),
                    Link("roles", "Roles", "apps/roles", "shield-lock"),
                    Link("permissions", "Permissions", "apps/permissions", "key")),
                Section("general", "General",
                    Link("account-settings", "Account settings", "pages/account-settings", "settings"),
                    Link("profile", "User profile", "pages/profile", "user-circle"))))
            .Add(Group("pages", "Website", "world", 50,
                Section("site", "Pages",
                    Link("pricing", "Pricing", "pages/pricing", "currency-dollar"),
                    Link("faq", "FAQ", "pages/faq", "help-circle"),
                    Link("help-center", "Help center", "pages/help-center", "lifebuoy")),
                Section("auth", "Authentication",
                    Link("login", "Sign in", "auth/login", "login"),
                    Link("register", "Sign up", "auth/register", "user-plus"),
                    Link("forgot-password", "Forgot password", "auth/forgot-password", "lock"),
                    Link("reset-password", "Reset password", "auth/reset-password", "lock-password"),
                    Link("verify-email", "Verify email", "auth/verify-email", "mail-opened"),
                    Link("two-steps", "Two-step verification", "auth/two-steps", "auth-2fa")),
                Section("misc", "Errors",
                    Link("error-404", "Page not found", "misc/not-found", "alert-triangle"),
                    Link("error-403", "Not authorized", "misc/not-authorized", "ban"),
                    Link("error-500", "Server error", "misc/server-error", "bug"),
                    Link("maintenance", "Under maintenance", "misc/maintenance", "tool"),
                    Link("coming-soon", "Coming soon", "misc/coming-soon", "rocket"))))
            .Add(Group("extra", "Extra", "sparkles", 55,
                Section("extra-account", "Account & billing",
                    Link("extra-billing", "Billing", "extra/billing", "credit-card"),
                    Link("extra-settings-plan", "Settings plan", "extra/settings-plan", "crown"),
                    Link("extra-pay", "Pay", "extra/pay", "wallet"),
                    Link("extra-pricing-table", "Pricing table", "extra/pricing-table", "table"),
                    Link("extra-trial-ended", "Trial ended", "extra/trial-ended", "hourglass"),
                    Link("extra-license", "License", "extra/license", "license")),
                Section("extra-apps", "Apps",
                    Link("extra-activity", "Activity", "extra/activity", "activity"),
                    Link("extra-tasks", "Tasks", "extra/tasks", "checkbox"),
                    Link("extra-logs", "Logs", "extra/logs", "terminal-2"),
                    Link("extra-uptime", "Uptime monitor", "extra/uptime", "heartbeat"),
                    Link("extra-music", "Music", "extra/music", "music"),
                    Link("extra-jobs", "Job listing", "extra/jobs", "briefcase"),
                    Link("extra-search", "Search results", "extra/search", "search")),
                Section("extra-media", "Media",
                    Link("extra-gallery", "Gallery", "extra/gallery", "photo"),
                    Link("extra-photogrid", "Photogrid", "extra/photogrid", "layout-grid"),
                    Link("extra-signatures", "Signatures", "extra/signatures", "signature")),
                Section("extra-blocks", "Page blocks",
                    Link("extra-widgets", "Widgets", "extra/widgets", "components"),
                    Link("extra-text-features", "Text features", "extra/text-features", "article"),
                    Link("extra-marketing", "Marketing", "extra/marketing", "presentation"),
                    Link("extra-cookie-banner", "Cookie banner", "extra/cookie-banner", "cookie"),
                    Link("extra-empty", "Empty page", "extra/empty", "file"),
                    Link("extra-page-loader", "Page loader", "extra/page-loader", "loader-2"))))
            .Add(Group("developer", "Developer", "code", 60,
                Section("ui", "Interface",
                    Link("components", "Components", "ui/components", "box"),
                    Link("cards", "Cards", "ui/cards", "cards"),
                    Link("charts", "Charts", "ui/charts", "chart-pie")),
                Section("forms", "Forms",
                    Link("form-layouts", "Form layouts", "forms/layouts", "layout"),
                    Link("form-wizard", "Wizard", "forms/wizard", "list-check"),
                    Link("form-validation", "Validation", "forms/validation", "square-rounded-check"),
                    Link("form-business", "Business fields", "forms/business", "forms")),
                Section("tables", "Tables",
                    Link("tables", "Data table", "tables/advanced", "table"),
                    Link("editable-grid", "Editable grid", "tables/editable", "table-options"),
                    Link("nested-grid", "Nested grid", "tables/nested", "stack-2"))));
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
