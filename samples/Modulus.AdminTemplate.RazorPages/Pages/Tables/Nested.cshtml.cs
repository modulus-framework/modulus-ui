using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Nested grid: same URLs as the MVC sample (/tables/nested, /tables/nested/level).</summary>
public class NestedModel(GarmentStore store) : SamplePageModel
{
    private const string LevelView = "~/Views/Shared/Ui/Tables/_NestedLevel.cshtml";

    public NestedPageModel Grid { get; private set; } = default!;

    public void OnGet() => Grid = GarmentGridActions.Page(store, Request.Query);

    public IActionResult OnGetLevel() => Partial(LevelView, GarmentGridActions.Get(store, Request.Query));

    public IActionResult OnPostLevel() => Partial(LevelView, GarmentGridActions.Post(store, Request, Response));
}
