using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Ecommerce;

/// <summary>Orders datagrid (filters, paging and sorting are boosted GETs).</summary>
public class OrdersModel(SampleStore store) : SamplePageModel
{
    public OrderListPage View { get; private set; } = default!;

    public void OnGet([FromQuery] OrderQuery query, [FromQuery] ProTableRequest request) =>
        View = OrderListPage.Load(store, query, request);
}
