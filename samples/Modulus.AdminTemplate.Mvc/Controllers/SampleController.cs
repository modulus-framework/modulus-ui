using Microsoft.AspNetCore.Mvc;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>
/// Base for the sample controllers. Page bodies live in Modulus.Samples.Web (Views/Shared/Ui/…),
/// shared with the Razor Pages sample; the app's _ViewStart applies the layout chosen here.
/// </summary>
public abstract class SampleController : Controller
{
    /// <summary>Renders <c>Views/Shared/Ui/{view}.cshtml</c> inside the app shell.</summary>
    protected ViewResult Page(string title, string view, object? model = null) => Render(title, view, model, "_SampleLayout");

    /// <summary>Renders <c>Views/Shared/Ui/{view}.cshtml</c> in the centred sign-in frame (no shell).</summary>
    protected ViewResult AuthPage(string title, string view, object? model = null) => Render(title, view, model, "_AuthLayout");

    /// <summary>Queues a toast for the next page (Post/Redirect/Get).</summary>
    protected void Flash(string message, string type = "success")
    {
        TempData["Flash"] = message;
        TempData["FlashType"] = type;
    }

    private ViewResult Render(string title, string view, object? model, string layout)
    {
        ViewData["Title"] = title;
        ViewData["Layout"] = layout;
        return View($"~/Views/Shared/Ui/{view}.cshtml", model);
    }
}
