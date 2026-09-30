using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;

namespace Modulus.AdminTemplate.Mvc.Controllers;

[Route("pages")]
public class PagesController(SampleStore store) : SampleController
{
    [HttpGet("profile")]
    public IActionResult Profile() => Page("Profile", "Pages/Profile");

    [HttpGet("pricing")]
    public IActionResult Pricing() => Page("Pricing", "Pages/Pricing");

    [HttpGet("faq")]
    public IActionResult Faq() => Page("FAQ", "Pages/Faq");

    [HttpGet("changelog")]
    public IActionResult Changelog() => Page("Changelog", "Pages/Changelog");

    [HttpGet("help-center")]
    public IActionResult HelpCenter() => Page("Help center", "Pages/HelpCenter");

    [HttpGet("help-center/{slug}")]
    public IActionResult HelpArticle(string slug) =>
        SampleContent.FindArticle(slug, out var category) is { } article
            ? Page(article.Title, "Pages/HelpArticle", (category!, article))
            : NotFound();

    // Account settings: one GET, one POST per section (Post/Redirect/Get with a toast).

    [HttpGet("account-settings")]
    public IActionResult AccountSettings(string? tab) => Settings(AccountSettingsPage.Load(store, tab));

    [HttpPost("account-settings/account")]
    public IActionResult SaveAccount([Bind(Prefix = "Account")] ProfileSettingsInput input)
    {
        if (!ModelState.IsValid) return Settings(AccountSettingsPage.Load(store, "account", account: input));
        store.SaveAccount(input);
        return Saved("Profile details saved", "account");
    }

    [HttpPost("account-settings/password")]
    public IActionResult ChangePassword([Bind(Prefix = "Password")] ChangePasswordInput input) =>
        ModelState.IsValid ? Saved("Password changed", "security") : Settings(AccountSettingsPage.Load(store, "security", password: input));

    [HttpPost("account-settings/twofactor")]
    public IActionResult ToggleTwoFactor()
    {
        store.TwoFactorEnabled = !store.TwoFactorEnabled;
        return Saved(store.TwoFactorEnabled ? "Two-factor authentication enabled" : "Two-factor authentication disabled", "security");
    }

    [HttpPost("account-settings/notifications")]
    public IActionResult SaveNotifications(string[] enabled)
    {
        store.SaveNotifications(enabled);
        return Saved("Notification preferences saved", "notifications");
    }

    [HttpPost("account-settings/connection")]
    public IActionResult SetConnection(string app, bool connected)
    {
        store.SetAppConnected(app, connected);
        return Saved(connected ? $"{app} connected" : $"{app} disconnected", "connections");
    }

    private ViewResult Settings(AccountSettingsPage model) => Page("Account settings", "Pages/AccountSettings", model);

    private RedirectResult Saved(string message, string tab)
    {
        Flash(message);
        return Redirect("~/pages/account-settings?tab=" + tab);
    }
}
