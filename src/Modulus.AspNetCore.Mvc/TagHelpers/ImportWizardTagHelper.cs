using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Import;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// The import wizard (<see cref="ImportWizardView"/>) as forms, the MVC front end of the Blazor <c>ImportWizard</c>:
/// <code>&lt;app-import-wizard view="@Model.View" action="/tables/import" /&gt;</code>
/// The page reads each step's posted form (the <c>step</c> button, <c>csv</c> and <c>m.{key}</c>) and renders the next
/// <see cref="ImportWizardView"/>; see <see cref="ImportWizardHtml"/>.
/// </summary>
[HtmlTargetElement("app-import-wizard", TagStructure = TagStructure.WithoutEndTag)]
public class ImportWizardTagHelper : TagHelper
{
    /// <summary>The wizard's current state.</summary>
    public ImportWizardView? View { get; set; }

    /// <summary>The page the forms post to.</summary>
    public string Action { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.Content.SetHtmlContent(ImportWizardHtml.Render(View ?? throw new InvalidOperationException("app-import-wizard needs a view."), Action));
    }
}
