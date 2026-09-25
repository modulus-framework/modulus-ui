using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Roles (cards + access matrix) and permissions (list); add / edit in the layout drawer.</summary>
[Route("apps")]
public class AccessController(SampleStore store) : SampleController
{
    private const string RoleForm = "~/Views/Shared/Ui/Apps/Roles/_RoleForm.cshtml";
    private const string PermissionForm = "~/Views/Shared/Ui/Apps/Roles/_PermissionForm.cshtml";

    [HttpGet("roles")]
    public IActionResult Roles() => Page("Roles", "Apps/Roles/Roles", RolesPage.Load(store));

    [HttpGet("roles/form/{id:int?}")]
    public IActionResult RoleFormView(int? id)
    {
        if (id is null) return Drawer(RoleForm, null, new RoleInput());
        return store.FindRole(id.Value) is { } role ? Drawer(RoleForm, id, RoleInput.From(role)) : NotFound();
    }

    [HttpPost("roles/save/{id:int?}")]
    public IActionResult SaveRole(int? id, RoleInput input)
    {
        if (id is { } existing && store.FindRole(existing) is null) return NotFound();
        if (!ModelState.IsValid) return Drawer(RoleForm, id, input);
        store.SaveRole(id, input);
        Response.HxMessage(MessageType.Success, id is null ? $"Added the {input.Name} role" : $"Saved the {input.Name} role")
            .HxCloseDrawer().HxTrigger("roles:refresh");
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/roles");
    }

    [HttpGet("permissions")]
    public IActionResult Permissions(string? keyword) => Page("Permissions", "Apps/Roles/Permissions", PermissionsPage.Load(store, keyword));

    [HttpGet("permissions/form/{id:int?}")]
    public IActionResult PermissionFormView(int? id)
    {
        if (id is null) return Drawer(PermissionForm, null, new PermissionInput());
        return store.Permissions().FirstOrDefault(p => p.Id == id) is { } p
            ? Drawer(PermissionForm, id, new PermissionInput { Name = p.Name, Roles = p.Roles, Core = p.Core })
            : NotFound();
    }

    [HttpPost("permissions/save/{id:int?}")]
    public IActionResult SavePermission(int? id, PermissionInput input)
    {
        if (id is { } existing && store.Permissions().All(p => p.Id != existing)) return NotFound();
        if (!ModelState.IsValid) return Drawer(PermissionForm, id, input);
        store.SavePermission(id, input);
        Response.HxMessage(MessageType.Success, id is null ? "Permission added" : "Permission saved")
            .HxCloseDrawer().HxTrigger("permissions:refresh");
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/permissions");
    }

    [HttpPost("permissions/delete/{id:int}")]
    public IActionResult DeletePermission(int id)
    {
        if (!store.DeletePermission(id)) return NotFound();
        Response.HxMessage(MessageType.Success, "Permission deleted").HxTrigger("permissions:refresh");
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/permissions");
    }

    private PartialViewResult Drawer(string view, int? id, object input)
    {
        ViewData["Id"] = id;
        return PartialView(view, input);
    }
}
