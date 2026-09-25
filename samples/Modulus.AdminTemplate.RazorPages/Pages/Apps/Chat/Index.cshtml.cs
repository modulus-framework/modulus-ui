using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Chat;

/// <summary>Chat app: same URLs as the MVC sample (/apps/chat/{id}, POST /apps/chat/{id}/send).</summary>
public class ChatModel(SampleStore store) : SamplePageModel
{
    public ChatPage View { get; private set; } = default!;

    public IActionResult OnGet(int? id, string? keyword)
    {
        if (id is { } i && store.FindContact(i) is null) return NotFound();
        View = ChatPage.Load(store, id, keyword);
        return Page();
    }

    public IActionResult OnPostSend(int id, string? text)
    {
        if (store.FindContact(id) is null) return NotFound();
        store.SendChat(id, text ?? "");
        return Redirect($"~/apps/chat/{id}");
    }
}
