using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Modulus.AdminTemplate.RazorPages;

/// <summary>Base for the sample pages: a toast for the next request (Post/Redirect/Get).</summary>
public abstract class SamplePageModel : PageModel
{
    protected void Flash(string message, string type = "success")
    {
        TempData["Flash"] = message;
        TempData["FlashType"] = type;
    }

    /// <summary>204 for htmx actions whose result travels in HX-Trigger headers (toast, close drawer, refresh).</summary>
    protected static NoContentResult NoContent() => new();

    /// <summary>
    /// A partial with extra view data (PageModel.Partial drops the page's ViewData; model state is kept
    /// so validation errors render in place).
    /// </summary>
    protected PartialViewResult Partial(string viewName, object model, params (string Key, object? Value)[] data)
    {
        var viewData = new ViewDataDictionary<object>(MetadataProvider, ModelState) { Model = model };
        foreach (var (key, value) in data) viewData[key] = value;
        return new PartialViewResult { ViewName = viewName, ViewData = viewData };
    }
}
