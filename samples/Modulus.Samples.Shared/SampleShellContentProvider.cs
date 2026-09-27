using System.Security.Claims;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.Samples.Shared;

/// <summary>
/// Header content for the sample apps, set up like an Odoo-style ERP: an app launcher whose apps own parts of the
/// menu, scheduled activities, notifications, account links and a multi-company switcher with branches.
/// </summary>
public sealed class SampleShellContentProvider : IShellContentProvider
{
    public Task<ShellContent> GetContentAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ShellContent
        {
            User = new ShellUser(CurrentUser.Name, CurrentUser.Role),
            SearchPlaceholder = "Search pages…",
            Notices =
            [
                new("Invoice #5012 was paid", "Fabrikam · $12,400.00", "18 min ago", true, "receipt", "success", "apps/invoice/list"),
                new("3 products are low on stock", "Dock Pro, Smart Lock, Field Tablet", "3 h ago", true, "alert-triangle", "warning", "apps/ecommerce/products"),
                new("Olivia Bennett sent you a message", "“If we can sign by Friday we keep the Q3 pricing.”", "5 h ago", true, "messages", "primary", "apps/chat"),
                new("New sign-in from Chrome on Windows", "San Francisco, United States", "Yesterday", false, "shield-check", "azure", "pages/account-settings?tab=security"),
                new("Weekly report is ready", null, "2 days ago", false, "chart-bar", "purple", "dashboards/crm"),
            ],
            UserLinks =
            [
                new("My profile", "pages/profile", "user"),
                new("Account settings", "pages/account-settings", "settings"),
                new("Billing", "pages/account-settings?tab=billing", "credit-card"),
                new("Pricing", "pages/pricing", "currency-dollar") { DividerBefore = true },
                new("Help center", "pages/help-center", "lifebuoy"),
            ],
            SignOutHref = "auth/login",
            // Each app owns a menu item (AdminMenuContributor keys); with the "Current app" menu the navbar shows
            // only that app's entries, as in Odoo.
            AppLauncher = AppLauncherStyle.Overlay,
            Apps =
            [
                new("Dashboards", "", "layout-dashboard", "blue", "Analytics, CRM and eCommerce") { MenuKey = "dashboards" },
                new("Sales", "apps/ecommerce/orders", "shopping-cart", "orange", "Orders, products and customers") { MenuKey = "ecommerce" },
                new("Invoicing", "apps/invoice/list", "file-invoice", "teal", "Invoices and payments") { MenuKey = "invoice" },
                new("Email", "apps/email", "mail", "azure") { MenuKey = "email" },
                new("Discuss", "apps/chat", "messages", "green", "Chat") { MenuKey = "chat" },
                new("Calendar", "apps/calendar", "calendar", "red") { MenuKey = "calendar" },
                new("Project", "apps/kanban", "layout-kanban", "purple", "Kanban board") { MenuKey = "kanban" },
                new("Users", "apps/users/list", "users", "indigo", "Users, roles and permissions") { MenuKey = "access" },
                new("Settings", "pages/account-settings", "settings", "gray", "Account settings") { MenuKey = "account-settings" },
                new("Website", "pages/profile", "world", "pink", "Pages, authentication and errors") { MenuKey = "pages" },
                new("UI kit", "ui/components", "components", "cyan", "Components, cards and charts") { MenuKey = "ui" },
                new("Forms & tables", "forms/layouts", "forms", "lime", "Forms, wizard, grids") { MenuKey = "forms" },
            ],
            Activities =
            [
                new("Sales orders", "shopping-cart", Late: 2, Today: 3, Planned: 5, "apps/ecommerce/orders", "orange"),
                new("Invoices", "file-invoice", Late: 1, Today: 0, Planned: 4, "apps/invoice/list", "teal"),
                new("Customers", "users", Late: 0, Today: 2, Planned: 3, "apps/customers", "indigo"),
                new("Tasks", "layout-kanban", Late: 0, Today: 1, Planned: 6, "apps/kanban", "purple"),
            ],
            ActivitiesHref = "apps/calendar",
            // No CurrentTenantId / ActiveTenantIds: the shell reads the cookies the switcher writes.
            MultiTenant = true,
            Tenants =
            [
                new("acme", "Acme Corporation", "Headquarters · USD"),
                new("acme-eu", "Acme Europe", "Branch · EUR") { ParentId = "acme" },
                new("acme-apac", "Acme Asia Pacific", "Branch · SGD") { ParentId = "acme" },
                new("globex", "Globex", "Company · USD"),
                new("initech", "Initech", "Company · CAD"),
                new("umbrella", "Umbrella Labs", "Company · GBP"),
                new("stark", "Stark Industries", "Company · USD"),
            ],
            TenantLinks =
            [
                new("Manage companies", "pages/account-settings", "settings"),
                new("New company", "pages/pricing", "plus"),
            ],
            FooterLinks =
            [
                new("Help center", "pages/help-center"),
                new("FAQ", "pages/faq"),
                new("Pricing", "pages/pricing"),
            ],
        });
}
