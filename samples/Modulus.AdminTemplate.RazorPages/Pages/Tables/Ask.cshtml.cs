using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Ask your data: the question and the conversation are posted back in the form (the same page as MVC's /tables/ask).</summary>
public class AskModel : SamplePageModel
{
    public AskView View { get; private set; } = default!;

    public async Task OnGetAsync() => View = await AskPage.AskAsync(null, null, null, AskPage.Model());

    public async Task<IActionResult> OnPostAsync([FromForm] string? transcript, [FromForm] string? filter, [FromForm] string? question)
    {
        View = await AskPage.AskAsync(transcript, filter, question, AskPage.Model());
        return Page();
    }
}
