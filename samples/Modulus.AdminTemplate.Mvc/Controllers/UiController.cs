using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;

namespace Modulus.AdminTemplate.Mvc.Controllers;

[Route("ui")]
public class UiController : SampleController
{
    [HttpGet("components")]
    public IActionResult Components() => Page("Components", "Ui/Components", new GalleryForm());

    [HttpGet("cards")]
    public IActionResult Cards() => Page("Cards", "Ui/Cards");

    [HttpGet("charts")]
    public IActionResult Charts() => Page("Charts", "Ui/Charts");

    [HttpPost("components/demo/{kind}")]
    [HttpPost("cards/demo/{kind}")]
    public IActionResult Demo(string kind)
    {
        GalleryDemo.Respond(Request, kind);
        return NoContent();
    }
}
