using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;
using Modulus.Samples.Web.Models;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Editable grid: same URL and posts as the MVC sample (/tables/editable).</summary>
public class EditableModel(ProjectGridStore store) : SamplePageModel
{
    public ProjectGridPage Grid { get; private set; } = default!;

    public void OnGet() => Grid = ProjectGridPage.Saved(store);

    /// <summary>Every grid button (Save, column operations, Reset) posts the whole grid here; htmx swaps the card.</summary>
    public IActionResult OnPost()
    {
        var (page, type, message) = ProjectGridActions.Handle(Request.Form, store);
        Grid = page;
        if (message is not null)
        {
            if (Request.IsHtmx()) Response.HxMessage(type!.Value, message);
            else Flash(message, type!.Value.ToString().ToLowerInvariant());
        }
        return Page();
    }
}
