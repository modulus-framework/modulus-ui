using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Misc;

// Also answers POST: the status-code and exception middleware re-execute the original method.
[IgnoreAntiforgeryToken]
public class ServerErrorModel : SamplePageModel
{
    public void OnGet() { }

    public void OnPost() { }
}
