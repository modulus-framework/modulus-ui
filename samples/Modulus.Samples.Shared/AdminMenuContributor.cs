using Modulus.Ui.Abstractions.Navigation;

namespace Modulus.Samples.Shared;

/// <summary>
/// Main menu for every sample app (Blazor, MVC, Razor Pages) — one contributor, three flavors.
/// Top-level items with children become navbar dropdowns / sidebar groups; a child with children
/// is a section (header plus links). Icons are Tabler Icons names.
/// </summary>
public sealed class AdminMenuContributor : IMenuContributor
{
    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        context
            .Add(Group("dashboards", "Dashboards", "layout-dashboard", 0,
                Link("dashboard-analytics", "Analytics", "", "chart-line", exact: true),
                Link("dashboard-crm", "CRM", "dashboards/crm", "users-group"),
                Link("dashboard-ecommerce", "eCommerce", "dashboards/ecommerce", "shopping-cart")))
            .Add(Group("apps", "Apps", "apps", 10,
                Link("email", "Email", "apps/email", "mail"),
                Link("chat", "Chat", "apps/chat", "messages"),
                Link("calendar", "Calendar", "apps/calendar", "calendar"),
                Link("kanban", "Kanban", "apps/kanban", "layout-kanban"),
                Section("ecommerce", "eCommerce",
                    Link("products", "Products", "apps/ecommerce/products", "package"),
                    Link("orders", "Orders", "apps/ecommerce/orders", "truck-delivery"),
                    Link("customers", "Customers", "apps/customers", "users")),
                Section("invoice", "Invoice",
                    Link("invoice-list", "Invoices", "apps/invoice/list", "file-invoice", also: ["apps/invoice/preview", "apps/invoice/edit"]),
                    Link("invoice-add", "Create invoice", "apps/invoice/add", "file-dollar")),
                Section("access", "Users & access",
                    Link("users", "Users", "apps/users/list", "user", also: ["apps/users/view"]),
                    Link("roles", "Roles", "apps/roles", "shield-lock"),
                    Link("permissions", "Permissions", "apps/permissions", "key"))))
            .Add(Group("pages", "Pages", "file-text", 20,
                Link("profile", "User profile", "pages/profile", "user-circle"),
                Link("account-settings", "Account settings", "pages/account-settings", "settings"),
                Link("pricing", "Pricing", "pages/pricing", "currency-dollar"),
                Link("faq", "FAQ", "pages/faq", "help-circle"),
                Link("help-center", "Help center", "pages/help-center", "lifebuoy"),
                Section("auth", "Authentication",
                    Link("login", "Sign in", "auth/login", "login"),
                    Link("register", "Sign up", "auth/register", "user-plus"),
                    Link("forgot-password", "Forgot password", "auth/forgot-password", "lock"),
                    Link("reset-password", "Reset password", "auth/reset-password", "lock-password"),
                    Link("verify-email", "Verify email", "auth/verify-email", "mail-opened"),
                    Link("two-steps", "Two-step verification", "auth/two-steps", "auth-2fa")),
                Section("misc", "Miscellaneous",
                    Link("error-404", "Page not found", "misc/not-found", "alert-triangle"),
                    Link("error-403", "Not authorized", "misc/not-authorized", "ban"),
                    Link("error-500", "Server error", "misc/server-error", "bug"),
                    Link("maintenance", "Under maintenance", "misc/maintenance", "tool"),
                    Link("coming-soon", "Coming soon", "misc/coming-soon", "rocket"))))
            .Add(Group("ui", "Interface", "components", 30,
                Link("components", "Components", "ui/components", "box"),
                Link("cards", "Cards", "ui/cards", "cards"),
                Link("charts", "Charts", "ui/charts", "chart-pie")))
            .Add(Group("forms", "Forms & tables", "forms", 40,
                Link("form-layouts", "Form layouts", "forms/layouts", "layout"),
                Link("form-wizard", "Wizard", "forms/wizard", "list-check"),
                Link("form-validation", "Validation", "forms/validation", "square-rounded-check"),
                Link("tables", "Data table", "tables/advanced", "table"),
                Link("editable-grid", "Editable grid", "tables/editable", "table-options")));
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
