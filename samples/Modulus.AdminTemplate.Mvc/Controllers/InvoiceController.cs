using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Invoices: list (datagrid), preview with send / payment, add / edit, print.</summary>
[Route("apps/invoice")]
public class InvoiceController(SampleStore store) : SampleController
{
    public const string RefreshEvent = "invoices:refresh";
    private const string PaymentForm = "~/Views/Shared/Ui/Apps/Invoice/_PaymentForm.cshtml";

    [HttpGet("list")]
    public IActionResult List([FromQuery] InvoiceQuery query, [FromQuery] ProTableRequest request) =>
        Page("Invoices", "Apps/Invoice/InvoiceList", InvoiceListPage.Load(store, query, request));

    [HttpPost("list/delete")]
    public IActionResult Delete([FromForm] int[] ids)
    {
        var count = store.DeleteInvoices(ids);
        Response.HxMessage(MessageType.Success, count == 1 ? "Invoice deleted" : $"{count} invoices deleted").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/invoice/list");
    }

    [HttpGet("preview/{id:int}")]
    public IActionResult Preview(int id) =>
        store.FindInvoice(id) is { } invoice ? Page($"Invoice {invoice.Number}", "Apps/Invoice/InvoicePreview", invoice) : NotFound();

    [HttpPost("preview/{id:int}/send")]
    public IActionResult Send(int id)
    {
        if (store.FindInvoice(id) is not { } invoice || !store.MarkInvoiceSent(id)) return NotFound();
        Response.HxMessage(MessageType.Success, $"Invoice {invoice.Number} sent to {invoice.ClientEmail}").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect($"~/apps/invoice/preview/{id}");
    }

    [HttpGet("preview/{id:int}/payment")]
    public IActionResult PaymentFormView(int id) =>
        store.FindInvoice(id) is { } invoice ? Payment(invoice, new PaymentInput { Amount = invoice.Balance }) : NotFound();

    [HttpPost("preview/{id:int}/payment")]
    public IActionResult RecordPayment(int id, PaymentInput input)
    {
        if (store.FindInvoice(id) is not { } invoice) return NotFound();
        if (!ModelState.IsValid) return Payment(invoice, input);
        store.RecordPayment(id, input.Amount!.Value);
        Response.HxMessage(MessageType.Success, $"Payment of {Fmt.Money(input.Amount.Value)} recorded").HxCloseDrawer().HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect($"~/apps/invoice/preview/{id}");
    }

    [HttpGet("add")]
    public IActionResult Add() => Editor(null, new InvoiceInput());

    [HttpPost("add")]
    public IActionResult Add([Bind(Prefix = "Input")] InvoiceInput input)
    {
        if (!ModelState.IsValid) return Editor(null, input);
        var invoice = store.SaveInvoice(null, input);
        Flash($"Invoice {invoice.Number} created");
        return Redirect($"~/apps/invoice/preview/{invoice.Id}");
    }

    [HttpGet("edit/{id:int}")]
    public IActionResult Edit(int id) =>
        store.FindInvoice(id) is { } invoice ? Editor(id, InvoiceInput.From(invoice)) : NotFound();

    [HttpPost("edit/{id:int}")]
    public IActionResult Edit(int id, [Bind(Prefix = "Input")] InvoiceInput input)
    {
        if (store.FindInvoice(id) is null) return NotFound();
        if (!ModelState.IsValid) return Editor(id, input);
        var invoice = store.SaveInvoice(id, input);
        Flash($"Invoice {invoice.Number} saved");
        return Redirect($"~/apps/invoice/preview/{id}");
    }

    [HttpGet("print/{id:int}")]
    public IActionResult Print(int id)
    {
        if (store.FindInvoice(id) is not { } invoice) return NotFound();
        ViewData["Title"] = $"Invoice {invoice.Number}";
        ViewData["Layout"] = "_BlankLayout";
        return View("~/Views/Shared/Ui/Apps/Invoice/InvoicePrint.cshtml", invoice);
    }

    private ViewResult Editor(int? id, InvoiceInput input) =>
        Page(id is null ? "Create invoice" : "Edit invoice", "Apps/Invoice/InvoiceEdit", new InvoiceEditPage(id, id ?? store.NextInvoiceNumber, input));

    private PartialViewResult Payment(Invoice invoice, PaymentInput input)
    {
        ViewData["Invoice"] = invoice;
        return PartialView(PaymentForm, input);
    }
}
