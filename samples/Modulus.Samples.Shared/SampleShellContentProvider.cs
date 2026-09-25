using System.Security.Claims;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.Samples.Shared;

/// <summary>Header content for the sample apps: the demo user, notifications and account links.</summary>
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
            FooterLinks =
            [
                new("Help center", "pages/help-center"),
                new("FAQ", "pages/faq"),
                new("Pricing", "pages/pricing"),
            ],
        });
}
