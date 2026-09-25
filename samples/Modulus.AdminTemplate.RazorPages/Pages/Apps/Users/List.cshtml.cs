using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Users;

/// <summary>
/// Users list (Tabler datagrid over htmx): same URLs as the MVC sample — GET filters/pages the grid,
/// /form/{id} loads the drawer, /save/{id} and /delete return 204 with a refresh event.
/// </summary>
public class UserListModel(SampleStore store) : SamplePageModel
{
    public const string RefreshEvent = "users:refresh";
    private const string FormView = "~/Views/Shared/Ui/Apps/Users/_UserForm.cshtml";

    public UserListPage View { get; private set; } = default!;

    public void OnGet([FromQuery] UserQuery query, [FromQuery] ProTableRequest request) =>
        View = UserListPage.Load(store, query, request);

    public IActionResult OnGetForm(int? id)
    {
        if (id is null) return Drawer(null, new UserInput());
        return store.FindUser(id.Value) is { } user ? Drawer(id, UserInput.From(user)) : NotFound();
    }

    public IActionResult OnPostSave(int? id, UserInput input)
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

    public IActionResult OnPostDelete([FromForm] int[] ids)
    {
        var count = store.DeleteUsers(ids);
        Response.HxMessage(MessageType.Success, count == 1 ? "User deleted" : $"{count} users deleted").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/users/list");
    }

    private PartialViewResult Drawer(int? id, UserInput input) => Partial(FormView, input, ("Id", id));
}
