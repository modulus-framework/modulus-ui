using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Web;

namespace Modulus.AdminTemplate.RazorPages.Pages.Ui;

public class CardsModel : SamplePageModel
{
    public void OnGet() { }

    public IActionResult OnPostDemo(string kind)
    {
        GalleryDemo.Respond(Request, kind);
        return NoContent();
    }
}
