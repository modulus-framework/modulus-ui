using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Import;
using Modulus.Ui.Abstractions.Query;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-import-wizard: the wizard's forms for the step it is given.</summary>
public class ImportWizardTagHelperTests
{
    private static readonly IReadOnlyList<ImportField> Fields =
    [
        new("name", "Name", QueryFieldKind.Text, Required: true),
        new("credit", "Credit limit", QueryFieldKind.Number),
    ];

    private static async Task<string> RenderAsync(ImportWizardTagHelper helper)
    {
        var output = new TagHelperOutput("app-import-wizard", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-import-wizard", [], new Dictionary<object, object>(), "iw1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task UploadStep_PostsAFileToTheAction()
    {
        var html = await RenderAsync(new ImportWizardTagHelper { View = ImportWizardView.Start(Fields), Action = "/tables/import" });

        Assert.Contains("<form class=\"m-import-form\" method=\"post\" enctype=\"multipart/form-data\" action=\"/tables/import\">", html);
        Assert.Contains("name=\"file\"", html);
        Assert.Contains("name=\"step\" value=\"upload\"", html);
    }

    [Fact]
    public async Task PreviewStep_CarriesTheFileAndTheMapping_AndDisablesImportWhileRowsAreBad()
    {
        var csv = "Name,Credit limit\nAda,5\nLee,n/a\n";
        var view = ImportFlow.Check(csv, new ImportMapping(new Dictionary<string, int> { ["name"] = 0, ["credit"] = 1 }), Fields);

        var html = await RenderAsync(new ImportWizardTagHelper { View = view, Action = "/tables/import" });

        Assert.Contains("name=\"csv\" value=\"Name,Credit limit", html);
        Assert.Contains("name=\"m.name\" value=\"0\"", html);
        Assert.Contains("1 ready", html);
        Assert.Contains("Credit limit: Not a number", html);
        Assert.Contains("name=\"step\" value=\"import\" class=\"ant-btn ant-btn-primary\" disabled", html);
    }

    [Fact]
    public async Task WithoutAView_TheHelperSaysWhatItNeeds()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => RenderAsync(new ImportWizardTagHelper()));
    }
}
