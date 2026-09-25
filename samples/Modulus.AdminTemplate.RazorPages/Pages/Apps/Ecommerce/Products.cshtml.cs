using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Ecommerce;

/// <summary>Products datagrid with the add / edit drawer: same URLs as the MVC sample.</summary>
public class ProductsModel(SampleStore store) : SamplePageModel
{
    public const string RefreshEvent = "products:refresh";
    private const string FormView = "~/Views/Shared/Ui/Apps/Ecommerce/_ProductForm.cshtml";

    public ProductListPage View { get; private set; } = default!;

    public void OnGet([FromQuery] ProductQuery query, [FromQuery] ProTableRequest request) =>
        View = ProductListPage.Load(store, query, request);

    public IActionResult OnGetForm(int? id)
    {
        if (id is null) return Drawer(null, new ProductInput());
        return store.FindProduct(id.Value) is { } product ? Drawer(id, ProductInput.From(product)) : NotFound();
    }

    public IActionResult OnPostSave(int? id, ProductInput input)
    {
        if (!ModelState.IsValid) return Drawer(id, input);
        if (id is { } existing && store.FindProduct(existing) is null) return NotFound();
        store.SaveProduct(id, input);
        Response.HxMessage(MessageType.Success, id is null ? $"Added {input.Name}" : $"Saved {input.Name}")
            .HxCloseDrawer().HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/ecommerce/products");
    }

    public IActionResult OnPostDelete([FromForm] int[] ids)
    {
        var count = store.DeleteProducts(ids);
        Response.HxMessage(MessageType.Success, count == 1 ? "Product deleted" : $"{count} products deleted").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/ecommerce/products");
    }

    private PartialViewResult Drawer(int? id, ProductInput input) => Partial(FormView, input, ("Id", id));
}
