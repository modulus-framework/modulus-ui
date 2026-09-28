using Microsoft.AspNetCore.Mvc;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Extra pages: activity, billing, logs, music, uptime … Query-driven ones read the query in the shared view.</summary>
[Route("extra")]
public class ExtraController : SampleController
{
    [HttpGet("activity")]
    public IActionResult Activity() => Page("Activity", "Extra/Activity");

    [HttpGet("billing")]
    public IActionResult Billing() => Page("Billing", "Extra/Billing");

    [HttpGet("cookie-banner")]
    public IActionResult CookieBanner() => Page("Cookie banner", "Extra/CookieBanner");

    [HttpGet("empty")]
    public IActionResult EmptyPage() => Page("Empty page", "Extra/Empty");

    [HttpGet("gallery")]
    public IActionResult Gallery() => Page("Gallery", "Extra/Gallery");

    [HttpGet("pay")]
    public IActionResult Pay() => Page("Checkout", "Extra/Pay");

    [HttpGet("jobs")]
    public IActionResult Jobs() => Page("Job listing", "Extra/Jobs");

    [HttpGet("license")]
    public IActionResult License() => Page("License", "Extra/License");

    [HttpGet("logs")]
    public IActionResult Logs() => Page("Logs", "Extra/Logs");

    [HttpGet("marketing")]
    public IActionResult Marketing() => BlankPage("Marketing", "Extra/Marketing");

    [HttpGet("music")]
    public IActionResult Music() => Page("Music", "Extra/Music");

    [HttpGet("page-loader")]
    public IActionResult PageLoader() => BlankPage("Loading", "Extra/PageLoader");

    [HttpGet("photogrid")]
    public IActionResult Photogrid() => Page("Photogrid", "Extra/Photogrid");

    [HttpGet("pricing-table")]
    public IActionResult PricingTable() => Page("Pricing table", "Extra/PricingTable");

    [HttpGet("search")]
    public IActionResult SearchResults() => Page("Search results", "Extra/SearchResults");

    [HttpGet("settings-plan")]
    public IActionResult SettingsPlan() => Page("Plan", "Extra/SettingsPlan");

    [HttpGet("signatures")]
    public IActionResult Signatures() => Page("Signatures", "Extra/Signatures");

    [HttpGet("tasks")]
    public IActionResult Tasks() => Page("Tasks", "Extra/Tasks");

    [HttpGet("text-features")]
    public IActionResult TextFeatures() => Page("Text features", "Extra/TextFeatures");

    [HttpGet("trial-ended")]
    public IActionResult TrialEnded() => Page("Trial ended", "Extra/TrialEnded");

    [HttpGet("uptime")]
    public IActionResult Uptime() => Page("Uptime monitor", "Extra/Uptime");

    [HttpGet("widgets")]
    public IActionResult Widgets() => Page("Widgets", "Extra/Widgets");
}
