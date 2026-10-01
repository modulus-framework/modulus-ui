using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Data grid: same URLs as the MVC sample (/tables/data-grid, /tables/data-grid/level).</summary>
public class DataGridModel(PurchaseStore store) : SamplePageModel
{
    private const string LevelView = "~/Views/Shared/Ui/Tables/_NestedLevel.cshtml";

    public PurchaseTablePage Grid { get; private set; } = default!;

    public void OnGet([FromQuery] ProTableRequest request) => Grid = PurchaseGridActions.Page(store, Request.Query, request);

    public IActionResult OnGetLevel() => Partial(LevelView, PurchaseGridActions.Get(store, Request.Query));

    public IActionResult OnPostLevel() => Partial(LevelView, PurchaseGridActions.Post(store, Request, Response));
}
