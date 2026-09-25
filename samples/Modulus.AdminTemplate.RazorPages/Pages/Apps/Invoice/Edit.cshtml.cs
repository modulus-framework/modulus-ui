using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Invoice;

/// <summary>Create (/apps/invoice/add) or edit (/apps/invoice/edit/{id}) an invoice; Post/Redirect/Get to the preview.</summary>
public class InvoiceEditModel(SampleStore store) : SamplePageModel
{
    public InvoiceEditPage View { get; private set; } = default!;

    public IActionResult OnGet(string mode, int? id)
    {
        if (mode == "add") return Show(null, new InvoiceInput());
        return id is { } existing && store.FindInvoice(existing) is { } invoice ? Show(existing, InvoiceInput.From(invoice)) : NotFound();
    }

    public IActionResult OnPost(string mode, int? id, [Bind(Prefix = "Input")] InvoiceInput input)
    {
        if (mode == "edit" && (id is null || store.FindInvoice(id.Value) is null)) return NotFound();
        var editId = mode == "edit" ? id : null;
        if (!ModelState.IsValid) return Show(editId, input);
        var invoice = store.SaveInvoice(editId, input);
        Flash(editId is null ? $"Invoice {invoice.Number} created" : $"Invoice {invoice.Number} saved");
        return Redirect($"~/apps/invoice/preview/{invoice.Id}");
    }

    private IActionResult Show(int? id, InvoiceInput input)
    {
        View = new InvoiceEditPage(id, id ?? store.NextInvoiceNumber, input);
        return Page();
    }
}
