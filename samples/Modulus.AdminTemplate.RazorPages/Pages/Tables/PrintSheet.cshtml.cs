using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Printing;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>A print view on a blank page: the parts list (kind=table) or the bin labels (kind=labels).</summary>
public class PrintSheetModel : SamplePageModel
{
    public PrintSheetPage Sheet { get; private set; } = default!;

    public void OnGet(string? kind) => Sheet = PrintSamples.Sheet(kind);
}
