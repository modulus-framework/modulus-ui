using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Invoice;

/// <summary>The invoice alone on a blank page (print / save as PDF).</summary>
public class InvoicePrintModel(SampleStore store) : SamplePageModel
{
    public Samples.Shared.Invoice Invoice { get; private set; } = default!;

    public IActionResult OnGet(int id)
    {
        if (store.FindInvoice(id) is not { } invoice) return NotFound();
        Invoice = invoice;
        return Page();
    }
}
