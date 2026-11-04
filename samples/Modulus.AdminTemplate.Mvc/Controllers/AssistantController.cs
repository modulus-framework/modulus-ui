using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Assistant;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>The AI assistant: a question answered from the help centre (cited) or by an agent with tools. The conversation rides in the form.</summary>
[Route("apps/assistant")]
public class AssistantController : SampleController
{
    [HttpGet("")]
    public async Task<IActionResult> Index() => Page("AI assistant", "Apps/Assistant", await AssistantPage.AskAsync(null, null, null, AssistantPage.Model()));

    [HttpPost("")]
    public async Task<IActionResult> Ask([FromForm] string? mode, [FromForm] string? transcript, [FromForm] string? question) =>
        Page("AI assistant", "Apps/Assistant", await AssistantPage.AskAsync(transcript, mode, question, AssistantPage.Model()));

    /// <summary>Adds the uploaded files to the knowledge base (a purchase order in them starts its approval).</summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] string? mode, [FromForm] string? transcript, [FromForm] List<IFormFile>? documents)
    {
        var files = new List<(string Name, byte[] Bytes)>();
        foreach (var file in documents ?? [])
        {
            using var stream = new MemoryStream();
            if (file.Length <= DocumentText.MaxDocumentBytes) await file.CopyToAsync(stream);
            files.Add((Path.GetFileName(file.FileName), stream.ToArray()));
        }
        return Page("AI assistant", "Apps/Assistant", await AssistantPage.UploadAsync(transcript, mode, files));
    }

    /// <summary>The buyer's answer to an approval card.</summary>
    [HttpPost("decide")]
    public async Task<IActionResult> Decide([FromForm] string? mode, [FromForm] string? transcript, [FromForm] string? approval) =>
        Page("AI assistant", "Apps/Assistant", await AssistantPage.DecideAsync(transcript, mode, approval));
}
