using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Users;

/// <summary>One user: profile, plan and tabs; suspend / reactivate over htmx, password via Post/Redirect/Get.</summary>
public class UserViewModel(SampleStore store) : SamplePageModel
{
    public UserViewPage View { get; private set; } = default!;

    public IActionResult OnGet(int id, string? tab)
    {
        if (store.FindUser(id) is not { } user) return NotFound();
        View = UserViewPage.Load(store, user, tab);
        return Page();
    }

    public IActionResult OnPostStatus(int id, UserStatus status)
    {
        if (id == CurrentUser.Id || !store.SetUserStatus(id, status)) return NotFound();
        Response.HxMessage(MessageType.Success, status == UserStatus.Inactive ? "User suspended" : "User reactivated").HxTrigger(UserListModel.RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect($"~/apps/users/view/{id}");
    }

    public IActionResult OnPostPassword(int id, [Bind(Prefix = "Password")] UserPasswordInput input)
    {
        if (store.FindUser(id) is not { } user) return NotFound();
        if (!ModelState.IsValid)
        {
            View = UserViewPage.Load(store, user, "security", input);
            return Page();
        }
        Flash($"Password changed for {user.Name}");
        return Redirect($"~/apps/users/view/{id}?tab=security");
    }
}
