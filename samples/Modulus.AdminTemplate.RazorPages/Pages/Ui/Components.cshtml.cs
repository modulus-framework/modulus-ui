using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;

namespace Modulus.AdminTemplate.RazorPages.Pages.Ui;

public class ComponentsModel : SamplePageModel
{
    public GalleryForm Form { get; } = new();

    public void OnGet() { }

    public IActionResult OnPostDemo(string kind)
    {
        GalleryDemo.Respond(Request, kind);
        return NoContent();
    }
}
