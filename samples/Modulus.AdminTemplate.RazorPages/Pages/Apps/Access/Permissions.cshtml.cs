using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Access;

/// <summary>Permissions list; add / edit in the drawer, delete over htmx (core permissions are kept).</summary>
public class PermissionsModel(SampleStore store) : SamplePageModel
{
    private const string FormView = "~/Views/Shared/Ui/Apps/Roles/_PermissionForm.cshtml";

    public PermissionsPage View { get; private set; } = default!;

    public void OnGet(string? keyword) => View = PermissionsPage.Load(store, keyword);

    public IActionResult OnGetForm(int? id)
    {
        if (id is null) return Partial(FormView, new PermissionInput(), ("Id", null));
        return store.Permissions().FirstOrDefault(p => p.Id == id) is { } p
            ? Partial(FormView, new PermissionInput { Name = p.Name, Roles = p.Roles, Core = p.Core }, ("Id", id))
            : NotFound();
    }

    public IActionResult OnPostSave(int? id, PermissionInput input)
    {
        if (id is { } existing && store.Permissions().All(p => p.Id != existing)) return NotFound();
        if (!ModelState.IsValid) return Partial(FormView, input, ("Id", id));
        store.SavePermission(id, input);
        Response.HxMessage(MessageType.Success, id is null ? "Permission added" : "Permission saved")
            .HxCloseDrawer().HxTrigger("permissions:refresh");
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/permissions");
    }

    public IActionResult OnPostDelete(int id)
    {
        if (!store.DeletePermission(id)) return NotFound();
        Response.HxMessage(MessageType.Success, "Permission deleted").HxTrigger("permissions:refresh");
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/permissions");
    }
}
