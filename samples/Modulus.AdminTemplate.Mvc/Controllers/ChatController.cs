using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Chat app: contacts and conversation are GETs; sending posts and redirects back (Post/Redirect/Get).</summary>
[Route("apps/chat")]
public class ChatController(SampleStore store) : SampleController
{
    [HttpGet("{id:int?}")]
    public IActionResult Index(int? id, string? keyword)
    {
        if (id is { } i && store.FindContact(i) is null) return NotFound();
        return Page("Chat", "Apps/Chat/Chat", ChatPage.Load(store, id, keyword));
    }

    [HttpPost("{id:int}/send")]
    public IActionResult Send(int id, string? text)
    {
        if (store.FindContact(id) is null) return NotFound();
        store.SendChat(id, text ?? "");
        return Redirect($"~/apps/chat/{id}");
    }
}
