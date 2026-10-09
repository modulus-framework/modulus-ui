using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Forms;

/// <summary>Form elements: every control of the Tabler form-elements page, bound to one read-only model.</summary>
public class ElementsModel : SamplePageModel
{
    public FormElementsPage Form { get; } = FormElementsPage.Load();

    public void OnGet() { }
}
