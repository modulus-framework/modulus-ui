using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Forms;

public class ValidationModel : SamplePageModel
{
    public ValidationDemoInput Input { get; private set; } = new();

    public void OnGet() { }

    // Bound as a handler parameter (field names without a prefix, same as the MVC action).
    public IActionResult OnPost(ValidationDemoInput input)
    {
        Input = input;
        if (!ModelState.IsValid) return Page();
        Flash($"Profile for {input.Name} submitted");
        return Redirect("~/forms/validation");
    }
}
