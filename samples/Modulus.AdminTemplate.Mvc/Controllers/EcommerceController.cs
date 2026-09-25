using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>eCommerce: products (datagrid + drawer form), orders list and order details.</summary>
[Route("apps/ecommerce")]
public class EcommerceController(SampleStore store) : SampleController
{
    public const string ProductsRefresh = "products:refresh";
    public const string OrdersRefresh = "orders:refresh";
    private const string ProductForm = "~/Views/Shared/Ui/Apps/Ecommerce/_ProductForm.cshtml";

    [HttpGet("products")]
    public IActionResult Products([FromQuery] ProductQuery query, [FromQuery] ProTableRequest request) =>
        Page("Products", "Apps/Ecommerce/ProductList", ProductListPage.Load(store, query, request));

    [HttpGet("products/form/{id:int?}")]
    public IActionResult ProductFormView(int? id)
    {
        if (id is null) return Drawer(null, new ProductInput());
        return store.FindProduct(id.Value) is { } product ? Drawer(id, ProductInput.From(product)) : NotFound();
    }

    [HttpPost("products/save/{id:int?}")]
    public IActionResult SaveProduct(int? id, ProductInput input)
    {
        if (!ModelState.IsValid) return Drawer(id, input);
        if (id is { } existing && store.FindProduct(existing) is null) return NotFound();
        store.SaveProduct(id, input);
        Response.HxMessage(MessageType.Success, id is null ? $"Added {input.Name}" : $"Saved {input.Name}")
            .HxCloseDrawer().HxTrigger(ProductsRefresh);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/ecommerce/products");
    }

    [HttpPost("products/delete")]
    public IActionResult DeleteProducts([FromForm] int[] ids)
    {
        var count = store.DeleteProducts(ids);
        Response.HxMessage(MessageType.Success, count == 1 ? "Product deleted" : $"{count} products deleted").HxTrigger(ProductsRefresh);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/ecommerce/products");
    }

    [HttpGet("orders")]
    public IActionResult Orders([FromQuery] OrderQuery query, [FromQuery] ProTableRequest request) =>
        Page("Orders", "Apps/Ecommerce/OrderList", OrderListPage.Load(store, query, request));

    [HttpGet("orders/{id:int}")]
    public IActionResult Order(int id) =>
        store.FindOrder(id) is { } order ? Page($"Order {order.Number}", "Apps/Ecommerce/OrderDetails", OrderDetailsPage.Load(store, order)) : NotFound();

    [HttpPost("orders/{id:int}/status")]
    public IActionResult OrderStatus(int id, OrderStatus status)
    {
        if (!store.SetOrderStatus(id, status)) return NotFound();
        Response.HxMessage(MessageType.Success, $"Order #{id} {(status == Samples.Shared.OrderStatus.Cancelled ? "cancelled" : $"marked {SampleUi.Label(status).ToLowerInvariant()}")}")
            .HxTrigger(OrdersRefresh);
        return Request.IsHtmx() ? NoContent() : Redirect($"~/apps/ecommerce/orders/{id}");
    }

    private PartialViewResult Drawer(int? id, ProductInput input)
    {
        ViewData["Id"] = id;
        return PartialView(ProductForm, input);
    }
}
