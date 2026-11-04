using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Assistant;
using Modulus.Ui.Abstractions.State;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>The AI assistant: a question answered from the help centre (cited) or by an agent with tools. The conversation rides in the form.</summary>
[Route("apps/assistant")]
public class AssistantController : SampleController
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromServices] IUiStateStore store) =>
        Page("AI assistant", "Apps/Assistant", await Saved(store, await AssistantHistory.LoadAsync(store, User), null, null));

    [HttpPost("")]
    public async Task<IActionResult> Ask([FromForm] string? mode, [FromForm] string? transcript, [FromForm] string? question, [FromServices] IUiStateStore store) =>
        Page("AI assistant", "Apps/Assistant", await Saved(store, transcript, mode, question));

    /// <summary>Adds the uploaded files to the knowledge base (a purchase order in them starts its approval).</summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] string? mode, [FromForm] string? transcript, [FromForm] List<IFormFile>? documents, [FromServices] IUiStateStore store)
    {
        var files = new List<(string Name, byte[] Bytes)>();
        foreach (var file in documents ?? [])
        {
            using var stream = new MemoryStream();
            if (file.Length <= DocumentText.MaxDocumentBytes) await file.CopyToAsync(stream);
            files.Add((Path.GetFileName(file.FileName), stream.ToArray()));
        }
        var view = await AssistantPage.UploadAsync(transcript, mode, files);
        await AssistantHistory.SaveAsync(store, User, view.Transcript);
        return Page("AI assistant", "Apps/Assistant", view);
    }

    /// <summary>The buyer's answer to an approval card.</summary>
    [HttpPost("decide")]
    public async Task<IActionResult> Decide([FromForm] string? mode, [FromForm] string? transcript, [FromForm] string? approval, [FromServices] IUiStateStore store)
    {
        var view = await AssistantPage.DecideAsync(transcript, mode, approval);
        await AssistantHistory.SaveAsync(store, User, view.Transcript);
        return Page("AI assistant", "Apps/Assistant", view);
    }

    /// <summary>The page after a question (or none): the conversation so far, kept on the server for a signed-in user.</summary>
    private async Task<AssistantView> Saved(IUiStateStore store, string? transcript, string? mode, string? question)
    {
        var view = await AssistantPage.AskAsync(transcript, mode, question, AssistantPage.Model());
        await AssistantHistory.SaveAsync(store, User, view.Transcript);
        return view;
    }
}
