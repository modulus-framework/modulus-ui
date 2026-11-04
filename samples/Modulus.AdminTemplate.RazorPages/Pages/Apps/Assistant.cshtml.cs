using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps;

/// <summary>The AI assistant (the same page as MVC's /apps/assistant): a question is answered from the help centre or by an agent.</summary>
public class AssistantModel : SamplePageModel
{
    public AssistantView View { get; private set; } = default!;

    public async Task OnGetAsync() => View = await AssistantPage.AskAsync(null, null, null, AssistantPage.Model());

    public async Task<IActionResult> OnPostAsync([FromForm] string? mode, [FromForm] string? transcript, [FromForm] string? question)
    {
        View = await AssistantPage.AskAsync(transcript, mode, question, AssistantPage.Model());
        return Page();
    }

    /// <summary>Adds the uploaded files to the knowledge base (a purchase order in them starts its approval).</summary>
    public async Task<IActionResult> OnPostUploadAsync([FromForm] string? mode, [FromForm] string? transcript, [FromForm] List<IFormFile>? documents)
    {
        var files = new List<(string Name, byte[] Bytes)>();
        foreach (var file in documents ?? [])
        {
            using var stream = new MemoryStream();
            if (file.Length <= Modulus.Ui.Abstractions.Assistant.DocumentText.MaxDocumentBytes) await file.CopyToAsync(stream);
            files.Add((Path.GetFileName(file.FileName), stream.ToArray()));
        }
        View = await AssistantPage.UploadAsync(transcript, mode, files);
        return Page();
    }

    /// <summary>The buyer's answer to an approval card.</summary>
    public async Task<IActionResult> OnPostDecideAsync([FromForm] string? mode, [FromForm] string? transcript, [FromForm] string? approval)
    {
        View = await AssistantPage.DecideAsync(transcript, mode, approval);
        return Page();
    }
}
