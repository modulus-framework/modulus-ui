using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Access;

/// <summary>Roles: cards and access matrix; /form/{id} loads the drawer, /save/{id} returns 204 + refresh.</summary>
public class RolesModel(SampleStore store) : SamplePageModel
{
    private const string FormView = "~/Views/Shared/Ui/Apps/Roles/_RoleForm.cshtml";

    public RolesPage View { get; private set; } = default!;

    public void OnGet() => View = RolesPage.Load(store);

    public IActionResult OnGetForm(int? id)
    {
        if (id is null) return Partial(FormView, new RoleInput(), ("Id", null));
        return store.FindRole(id.Value) is { } role ? Partial(FormView, RoleInput.From(role), ("Id", id)) : NotFound();
    }

    public IActionResult OnPostSave(int? id, RoleInput input)
    {
        if (id is { } existing && store.FindRole(existing) is null) return NotFound();
        if (!ModelState.IsValid) return Partial(FormView, input, ("Id", id));
        store.SaveRole(id, input);
        Response.HxMessage(MessageType.Success, id is null ? $"Added the {input.Name} role" : $"Saved the {input.Name} role")
            .HxCloseDrawer().HxTrigger("roles:refresh");
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/roles");
    }
}
