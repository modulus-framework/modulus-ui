using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Invoice;

/// <summary>Invoice list (datagrid over htmx); /delete returns 204 with a refresh event.</summary>
public class InvoiceListModel(SampleStore store) : SamplePageModel
{
    public const string RefreshEvent = "invoices:refresh";

    public InvoiceListPage View { get; private set; } = default!;

    public void OnGet([FromQuery] InvoiceQuery query, [FromQuery] ProTableRequest request) =>
        View = InvoiceListPage.Load(store, query, request);

    public IActionResult OnPostDelete([FromForm] int[] ids)
    {
        var count = store.DeleteInvoices(ids);
        Response.HxMessage(MessageType.Success, count == 1 ? "Invoice deleted" : $"{count} invoices deleted").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/invoice/list");
    }
}
