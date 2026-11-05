using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;
using Modulus.Ui.Abstractions.State;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps;

/// <summary>The AI assistant (the same page as MVC's /apps/assistant): a question is answered from the knowledge base or by an agent.</summary>
public class AssistantModel : SamplePageModel
{
    public AssistantView View { get; private set; } = default!;

    public async Task OnGetAsync([FromServices] IUiStateStore store) =>
        View = await Saved(store, await AssistantHistory.LoadAsync(store, User), null, null);

    public async Task<IActionResult> OnPostAsync([FromForm] string? mode, [FromForm] string? transcript, [FromForm] string? question, [FromServices] IUiStateStore store)
    {
        View = await Saved(store, transcript, mode, question);
        return Page();
    }

    /// <summary>Adds the uploaded files to the knowledge base (a purchase order in them starts its approval).</summary>
    public async Task<IActionResult> OnPostUploadAsync([FromForm] string? mode, [FromForm] string? transcript, [FromForm] List<IFormFile>? documents, [FromServices] IUiStateStore store)
    {
        var files = new List<(string Name, byte[] Bytes)>();
        foreach (var file in documents ?? [])
        {
            using var stream = new MemoryStream();
            if (file.Length <= Modulus.Ui.Abstractions.Assistant.DocumentText.MaxDocumentBytes) await file.CopyToAsync(stream);
            files.Add((Path.GetFileName(file.FileName), stream.ToArray()));
        }
        View = await AssistantPage.UploadAsync(transcript, mode, files);
        await AssistantHistory.SaveAsync(store, User, View.Transcript);
        return Page();
    }

    /// <summary>The buyer's answer to an approval card.</summary>
    public async Task<IActionResult> OnPostDecideAsync([FromForm] string? mode, [FromForm] string? transcript, [FromForm] string? approval, [FromServices] IUiStateStore store)
    {
        View = await AssistantPage.DecideAsync(transcript, mode, approval);
        await AssistantHistory.SaveAsync(store, User, View.Transcript);
        return Page();
    }

    private async Task<AssistantView> Saved(IUiStateStore store, string? transcript, string? mode, string? question)
    {
        var view = await AssistantPage.AskAsync(transcript, mode, question, AssistantPage.Model());
        await AssistantHistory.SaveAsync(store, User, view.Transcript);
        return view;
    }
}
