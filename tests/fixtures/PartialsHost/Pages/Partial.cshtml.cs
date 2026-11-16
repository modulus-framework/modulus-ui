using Microsoft.AspNetCore.Mvc.RazorPages;

public class PartialModel : PageModel
{
    public string Name => (string?)RouteData.Values["name"] ?? "";

    /// <summary>Fills the ViewBag of the partial with its sample values when the request asks for them (?sample=true).</summary>
    public void OnGet()
    {
        if (Request.Query["sample"] != "true") return;
        foreach (var (key, value) in PartialSamples.For(Name)) ViewData[key] = value;
    }
}
