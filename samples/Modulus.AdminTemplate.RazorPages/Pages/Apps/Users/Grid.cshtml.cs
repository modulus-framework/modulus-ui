using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Users;

/// <summary>The users as cards: role filter, search and paging are plain GETs.</summary>
public class UserGridModel(SampleStore store) : SamplePageModel
{
    public UserGridPage Grid { get; private set; } = default!;

    public void OnGet(string? q, string? role, int current = 1) => Grid = UserGridPage.Load(store, q, role, current);
}
