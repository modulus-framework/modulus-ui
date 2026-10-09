using Modulus.Samples.Shared;
using Modulus.Samples.Web;
using Modulus.Ui.Abstractions.Import;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Import data: same URL as the MVC sample (/tables/import). Each step posts back here.</summary>
public class ImportModel(CustomerImportStore store) : SamplePageModel
{
    public ImportWizardView View { get; private set; } = default!;

    public async Task OnGetAsync() => View = await ImportPageActions.RespondAsync(Request, store);

    public async Task OnPostAsync() => View = await ImportPageActions.RespondAsync(Request, store);
}
