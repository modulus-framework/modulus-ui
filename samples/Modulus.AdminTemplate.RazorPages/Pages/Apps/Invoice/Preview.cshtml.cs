using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Invoice;

/// <summary>Invoice preview: send (htmx), record a payment in the drawer.</summary>
public class InvoicePreviewModel(SampleStore store) : SamplePageModel
{
    private const string PaymentForm = "~/Views/Shared/Ui/Apps/Invoice/_PaymentForm.cshtml";

    public Samples.Shared.Invoice Invoice { get; private set; } = default!;

    public IActionResult OnGet(int id)
    {
        if (store.FindInvoice(id) is not { } invoice) return NotFound();
        Invoice = invoice;
        return Page();
    }

    public IActionResult OnPostSend(int id)
    {
        if (store.FindInvoice(id) is not { } invoice || !store.MarkInvoiceSent(id)) return NotFound();
        Response.HxMessage(MessageType.Success, $"Invoice {invoice.Number} sent to {invoice.ClientEmail}").HxTrigger(InvoiceListModel.RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect($"~/apps/invoice/preview/{id}");
    }

    public IActionResult OnGetPayment(int id) =>
        store.FindInvoice(id) is { } invoice ? Partial(PaymentForm, new PaymentInput { Amount = invoice.Balance }, ("Invoice", invoice)) : NotFound();

    public IActionResult OnPostPayment(int id, PaymentInput input)
    {
        if (store.FindInvoice(id) is not { } invoice) return NotFound();
        if (!ModelState.IsValid) return Partial(PaymentForm, input, ("Invoice", invoice));
        store.RecordPayment(id, input.Amount!.Value);
        Response.HxMessage(MessageType.Success, $"Payment of {Fmt.Money(input.Amount.Value)} recorded").HxCloseDrawer().HxTrigger(InvoiceListModel.RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect($"~/apps/invoice/preview/{id}");
    }
}
