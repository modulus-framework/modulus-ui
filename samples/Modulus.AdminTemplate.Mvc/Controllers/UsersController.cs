using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>
/// Users list (Tabler datagrid over htmx). Filtering, paging and sorting are plain GETs of the page
/// (boosted); the add / edit drawer loads a partial and saves return 204 with a refresh event.
/// </summary>
[Route("apps/users")]
public class UsersController(SampleStore store) : SampleController
{
    public const string RefreshEvent = "users:refresh";
    private const string FormView = "~/Views/Shared/Ui/Apps/Users/_UserForm.cshtml";

    [HttpGet("list")]
    public IActionResult List([FromQuery] UserQuery query, [FromQuery] ProTableRequest request) =>
        Page("Users", "Apps/Users/UserList", UserListPage.Load(store, query, request));

    [HttpGet("list/form/{id:int?}")]
    public IActionResult Form(int? id)
    {
        if (id is null) return Drawer(null, new UserInput());
        return store.FindUser(id.Value) is { } user ? Drawer(id, UserInput.From(user)) : NotFound();
    }

    [HttpPost("list/save/{id:int?}")]
    public IActionResult Save(int? id, UserInput input)
    {
        if (!ModelState.IsValid) return Drawer(id, input);
        if (id is { } existing)
        {
            if (!store.UpdateUser(existing, input)) return NotFound();
        }
        else store.AddUser(input);

        Response.HxMessage(MessageType.Success, id is null ? $"Added {input.Name}" : $"Saved {input.Name}")
            .HxCloseDrawer().HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/users/list");
    }

    [HttpPost("list/delete")]
    public IActionResult Delete([FromForm] int[] ids)
    {
        var count = store.DeleteUsers(ids);
        Response.HxMessage(MessageType.Success, count == 1 ? "User deleted" : $"{count} users deleted").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/users/list");
    }

    [HttpGet("view/{id:int}")]
    public IActionResult View(int id, string? tab) =>
        store.FindUser(id) is { } user ? Page(user.Name, "Apps/Users/UserView", UserViewPage.Load(store, user, tab)) : NotFound();

    [HttpPost("view/{id:int}/status")]
    public IActionResult Status(int id, UserStatus status)
    {
        if (id == CurrentUser.Id || !store.SetUserStatus(id, status)) return NotFound();
        Response.HxMessage(MessageType.Success, status == UserStatus.Inactive ? "User suspended" : "User reactivated").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect($"~/apps/users/view/{id}");
    }

    [HttpPost("view/{id:int}/password")]
    public IActionResult Password(int id, [Bind(Prefix = "Password")] UserPasswordInput input)
    {
        if (store.FindUser(id) is not { } user) return NotFound();
        if (!ModelState.IsValid) return Page(user.Name, "Apps/Users/UserView", UserViewPage.Load(store, user, "security", input));
        Flash($"Password changed for {user.Name}");
        return Redirect($"~/apps/users/view/{id}?tab=security");
    }

    private PartialViewResult Drawer(int? id, UserInput input)
    {
        ViewData["Id"] = id;
        return PartialView(FormView, input);
    }
}
