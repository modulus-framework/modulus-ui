using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Merchandising;

/// <summary>Packing list templates: the same URLs as the MVC sample (/merchandising/packing-lists and its download).</summary>
public class PackingListsModel : SamplePageModel
{
    public PackingListPage Form { get; private set; } = PackingListPage.Load(null, null, null, null, null, null);

    public void OnGet(string? q, string? buyer, string? status, string? po, string? template, string? format) =>
        Form = PackingListPage.Load(q, buyer, status, po, template, format);

    public IActionResult OnGetDownload(string? po, string? format, string? template) =>
        PackingListDemo.Download(po, format, template) is var (bytes, type, name) ? File(bytes, type, name) : NotFound();
}
