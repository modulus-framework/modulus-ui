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

    [HttpGet("icons")]
    public IActionResult Icons() => Page("Icons", "Ui/Icons");

    [HttpGet("typography")]
    public IActionResult Typography() => Page("Typography", "Ui/Typography");

    [HttpGet("colors")]
    public IActionResult Colors() => Page("Colours", "Ui/Colors");

    /// <summary>The customer picker's "Add customer" modal: the form again with errors, or a fresh one after a save.</summary>
    [HttpPost("components/customer")]
    public IActionResult Customer([FromServices] SampleStore store, CustomerInput input)
    {
        if (!ModelState.IsValid) return PartialView(CustomerPicker.FormView, input);
        CustomerPicker.Saved(Response, store.AddCustomer(input));
        ModelState.Clear();
        return PartialView(CustomerPicker.FormView, new CustomerInput());
    }

    [HttpPost("components/demo/{kind}")]
    [HttpPost("cards/demo/{kind}")]
    public IActionResult Demo(string kind)
    {
        GalleryDemo.Respond(Request, kind);
        return NoContent();
    }
}
