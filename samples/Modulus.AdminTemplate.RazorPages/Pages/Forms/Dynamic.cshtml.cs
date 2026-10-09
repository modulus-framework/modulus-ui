using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Forms;

public class DynamicModel : SamplePageModel
{
    public DynamicFormState State { get; private set; } = DynamicFormDemo.Empty;

    public void OnGet() { }

    // The posted fields are read as a dictionary: the schema decides which of them count. The page shows what was saved.
    public void OnPost() =>
        State = DynamicFormDemo.Submit(Request.Form.ToDictionary(f => f.Key, f => (string?)f.Value.LastOrDefault()));
}
