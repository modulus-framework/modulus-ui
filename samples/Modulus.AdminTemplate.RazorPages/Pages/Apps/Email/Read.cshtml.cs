using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Email;

/// <summary>One open message (marks it read); actions post to the Email page's handlers.</summary>
public class EmailReadModel(SampleStore store) : SamplePageModel
{
    public EmailPage View { get; private set; } = default!;

    public IActionResult OnGet(int id, string? folder, string? label)
    {
        View = EmailPage.Load(store, folder, label, null, id);
        return View.Open is null ? NotFound() : Page();
    }
}
