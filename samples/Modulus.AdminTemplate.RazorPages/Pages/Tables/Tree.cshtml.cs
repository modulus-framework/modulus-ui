using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Tree grid: same URLs as the MVC sample (/tables/tree, /tables/tree/level).</summary>
public class TreeModel(BillOfMaterialsStore store) : SamplePageModel
{
    private const string LevelView = "~/Views/Shared/Ui/Tables/_NestedLevel.cshtml";

    public NestedLevelModel Grid { get; private set; } = default!;

    public void OnGet() => Grid = TreeGridActions.Page(store, Request.Query);

    public IActionResult OnGetLevel() => Partial(LevelView, TreeGridActions.Get(store, Request.Query));

    public IActionResult OnPostLevel() => Partial(LevelView, TreeGridActions.Post(store, Request, Response));
}
