using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Merchandising: the packing list templates page, and the download of a generated packing list.</summary>
[Route("merchandising")]
public class MerchandisingController : SampleController
{
    [HttpGet("packing-lists")]
    public IActionResult PackingLists(string? q, string? buyer, string? status, string? po, string? template, string? format, string[]? cols) =>
        Page("Packing list templates", "Merchandising/PackingLists", PackingListPage.Load(q, buyer, status, po, template, format, cols));

    [HttpGet("packing-lists/download")]
    public IActionResult Download(string? po, string? format, string? template, string[]? cols) =>
        PackingListDemo.Download(po, format, template, cols) is var (bytes, type, name) ? File(bytes, type, name) : NotFound();
}
