using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Ecommerce;

/// <summary>One order: items, totals, shipping activity; advance or cancel over htmx.</summary>
public class OrderDetailsModel(SampleStore store) : SamplePageModel
{
    public const string RefreshEvent = "orders:refresh";

    public OrderDetailsPage View { get; private set; } = default!;

    public IActionResult OnGet(int id)
    {
        if (store.FindOrder(id) is not { } order) return NotFound();
        View = OrderDetailsPage.Load(store, order);
        return Page();
    }

    public IActionResult OnPostStatus(int id, OrderStatus status)
    {
        if (!store.SetOrderStatus(id, status)) return NotFound();
        Response.HxMessage(MessageType.Success, $"Order #{id} {(status == OrderStatus.Cancelled ? "cancelled" : $"marked {SampleUi.Label(status).ToLowerInvariant()}")}")
            .HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect($"~/apps/ecommerce/orders/{id}");
    }
}
