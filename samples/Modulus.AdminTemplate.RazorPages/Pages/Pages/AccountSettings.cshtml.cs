using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;

namespace Modulus.AdminTemplate.RazorPages.Pages.Pages;

/// <summary>Account settings: one GET, one POST handler per section (Post/Redirect/Get with a toast).</summary>
public class AccountSettingsModel(SampleStore store) : SamplePageModel
{
    public AccountSettingsPage View { get; private set; } = default!;

    public void OnGet(string? tab) => View = AccountSettingsPage.Load(store, tab);

    public IActionResult OnPostAccount([Bind(Prefix = "Account")] ProfileSettingsInput input)
    {
        if (!ModelState.IsValid) return Show(AccountSettingsPage.Load(store, "account", account: input));
        store.SaveAccount(input);
        return Saved("Profile details saved", "account");
    }

    public IActionResult OnPostPassword([Bind(Prefix = "Password")] ChangePasswordInput input) =>
        ModelState.IsValid ? Saved("Password changed", "security") : Show(AccountSettingsPage.Load(store, "security", password: input));

    public IActionResult OnPostTwoFactor()
    {
        store.TwoFactorEnabled = !store.TwoFactorEnabled;
        return Saved(store.TwoFactorEnabled ? "Two-factor authentication enabled" : "Two-factor authentication disabled", "security");
    }

    public IActionResult OnPostNotifications(string[] enabled)
    {
        store.SaveNotifications(enabled);
        return Saved("Notification preferences saved", "notifications");
    }

    public IActionResult OnPostConnection(string app, bool connected)
    {
        store.SetAppConnected(app, connected);
        return Saved(connected ? $"{app} connected" : $"{app} disconnected", "connections");
    }

    private PageResult Show(AccountSettingsPage view)
    {
        View = view;
        return Page();
    }

    private RedirectResult Saved(string message, string tab)
    {
        Flash(message);
        return Redirect("~/pages/account-settings?tab=" + tab);
    }
}
