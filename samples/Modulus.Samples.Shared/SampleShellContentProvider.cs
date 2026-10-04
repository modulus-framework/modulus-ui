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
            // The signed-in user (SampleAuth); the demo user while nobody is signed in.
            User = user.Identity?.IsAuthenticated == true
                ? new ShellUser(user.Identity.Name ?? CurrentUser.Name, user.FindFirst(ClaimTypes.Role)?.Value)
                : new ShellUser(CurrentUser.Name, CurrentUser.Role),
            SearchPlaceholder = "Search pages…",
            Notices =
            [
                new("Invoice #5012 was paid", "Fabrikam · $12,400.00", "18 min ago", true, "tb:file-invoice", "success", "apps/invoice/list"),
                new("3 products are low on stock", "Dock Pro, Smart Lock, Field Tablet", "3 h ago", true, "warning", "warning", "apps/ecommerce/products"),
                new("Olivia Bennett sent you a message", "“If we can sign by Friday we keep the Q3 pricing.”", "5 h ago", true, "comment", "primary", "apps/chat"),
                new("New sign-in from Chrome on Windows", "San Francisco, United States", "Yesterday", false, "safety-certificate", "azure", "pages/account-settings?tab=security"),
                new("Weekly report is ready", null, "2 days ago", false, "bar-chart", "purple", "dashboards/crm"),
            ],
            UserLinks =
            [
                new("My profile", "pages/profile", "user"),
                new("Account settings", "pages/account-settings", "setting"),
                new("Billing", "pages/account-settings?tab=billing", "credit-card"),
                new("Pricing", "pages/pricing", "dollar") { DividerBefore = true },
                new("Help center", "pages/help-center", "tb:lifebuoy"),
            ],
            SignOutHref = "auth/logout",
            // Each app owns a menu item (AdminMenuContributor keys); with the "Current app" menu the navbar shows
            // only that app's entries, as in Odoo.
            AppLauncher = AppLauncherStyle.Overlay,
            Apps =
            [
                new("Overview", "", "dashboard", "blue", "Analytics, CRM and eCommerce dashboards") { MenuKey = "dashboards", Group = "Insights" },
                new("Sales", "apps/ecommerce/orders", "shopping-cart", "orange", "Orders, products and customers") { MenuKey = "sales", Group = "Sales & finance" },
                new("Invoicing", "apps/invoice/list", "file-text", "teal", "Customer invoices and payments") { MenuKey = "invoicing", Group = "Sales & finance" },
                new("Email", "apps/email", "mail", "azure", "Inbox, drafts and sent mail") { MenuKey = "email", Group = "Productivity" },
                new("Discuss", "apps/chat", "comment", "green", "Team and customer chat") { MenuKey = "chat", Group = "Productivity" },
                new("Calendar", "apps/calendar", "calendar", "red", "Meetings and deadlines") { MenuKey = "calendar", Group = "Productivity" },
                new("Project", "apps/kanban", "project", "purple", "Tasks on a kanban board") { MenuKey = "kanban", Group = "Productivity" },
                new("Settings", "apps/users/list", "setting", "gray", "Users, roles, companies and preferences") { MenuKey = "settings", Group = "Administration" },
                new("Website", "pages/pricing", "global", "pink", "Pages, sign-in and error screens") { MenuKey = "pages", Group = "Administration" },
                new("Developer", "ui/components", "code", "indigo", "Components, forms, tables and charts") { MenuKey = "developer", Group = "Administration" },
                // Listed after Developer and Calendar: Plugins also links to some of their pages, which stay in those apps.
                new("Plugins", "plugins/charts-advanced", "tb:puzzle", "cyan", "Charts, inputs, media and maps") { MenuKey = "plugins", Group = "Templates" },
                new("Extra", "extra/billing", "tb:sparkles", "yellow", "Billing, apps, media and page blocks") { MenuKey = "extra", Group = "Templates" },
            ],
            Activities =
            [
                new("Sales orders", "shopping-cart", Late: 2, Today: 3, Planned: 5, "apps/ecommerce/orders", "orange"),
                new("Invoices", "file-text", Late: 1, Today: 0, Planned: 4, "apps/invoice/list", "teal"),
                new("Customers", "team", Late: 0, Today: 2, Planned: 3, "apps/customers", "indigo"),
                new("Tasks", "project", Late: 0, Today: 1, Planned: 6, "apps/kanban", "purple"),
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
                new("Manage companies", "pages/account-settings", "setting"),
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
