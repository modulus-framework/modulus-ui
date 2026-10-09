using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Import;
using Modulus.Ui.Abstractions.Query;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The Blazor ImportWizard: a file goes through the upload, mapping, check and import steps, as the library runs them.</summary>
public class ImportWizardComponentTests : BunitContext
{
    private static readonly IReadOnlyList<ImportField> Fields =
    [
        new("name", "Name", QueryFieldKind.Text, Required: true),
        new("credit", "Credit limit", QueryFieldKind.Number),
    ];

    private IRenderedComponent<ImportWizard> Mount(Func<IReadOnlyList<ImportRow>, Task<string>>? onImport = null) =>
        Render<ImportWizard>(p => p
            .Add(x => x.Fields, Fields)
            .Add(x => x.OnImport, onImport ?? (rows => Task.FromResult($"Imported {rows.Count}."))));

    private static IElement Button(IRenderedComponent<ImportWizard> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private static void Upload(IRenderedComponent<ImportWizard> cut, string csv) =>
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(csv, "customers.csv"));

    [Fact]
    public void StartsOnTheUploadStep_WithTheColumnsItReads()
    {
        var cut = Mount();

        Assert.Contains("<li class=\"m-import-step\" data-state=\"current\">Upload</li>", cut.Markup);
        Assert.Contains("Credit limit", cut.Markup);
        Assert.Contains("required", cut.Markup);
    }

    [Fact]
    public void AFile_SuggestsTheColumns_AndTheCheckShowsTheRows()
    {
        var cut = Mount();

        Upload(cut, "Name,Credit limit\nAda,5\nLee,n/a\n");

        Assert.Equal(2, cut.FindAll(".m-import-select").Count);
        Assert.Equal("Name", cut.FindAll(".m-import-select")[0].QuerySelector("option[selected]")!.TextContent);

        Button(cut, "Check rows").Click();

        Assert.Contains("1 ready", cut.Markup);
        Assert.Contains("1 with problems", cut.Markup);
        Assert.Contains("Credit limit: Not a number", cut.Markup);
        Assert.True(Button(cut, "Import 1 rows").HasAttribute("disabled"));
    }

    [Fact]
    public void ChangingAColumn_IsCheckedAgainstTheNewMapping()
    {
        var cut = Mount();
        Upload(cut, "Name,Other,Credit limit\nAda,x,5\n");

        cut.FindAll(".m-import-select")[1].Change("2");
        Button(cut, "Check rows").Click();

        Assert.Contains("1 ready", cut.Markup);
        Assert.Contains("0 with problems", cut.Markup);
    }

    [Fact]
    public void ImportingAValidFile_CallsTheCallback_AndShowsItsMessage()
    {
        IReadOnlyList<ImportRow>? imported = null;
        var cut = Mount(rows =>
        {
            imported = rows;
            return Task.FromResult($"Saved {rows.Count} customers.");
        });
        Upload(cut, "Name,Credit limit\nAda,5\nLee,7.5\n");
        Button(cut, "Check rows").Click();

        Button(cut, "Import 2 rows").Click();

        Assert.NotNull(imported);
        Assert.Equal(2, imported!.Count);
        Assert.Equal(7.5m, imported[1].Get<decimal>("credit"));
        Assert.Contains("Saved 2 customers.", cut.Markup);
        Assert.Contains("Import another file", cut.Markup);
    }

    [Fact]
    public void AFileWithoutAHeader_IsRefused_OnTheUploadStep()
    {
        var cut = Mount();

        Upload(cut, "\n\n");

        Assert.Contains("The file has no header row.", cut.Find(".m-import-error").TextContent);
        Assert.Empty(cut.FindAll(".m-import-select"));
    }

    [Fact]
    public void StartOver_ReturnsToTheUploadStep()
    {
        var cut = Mount();
        Upload(cut, "Name\nAda\n");

        Button(cut, "Start over").Click();

        Assert.Empty(cut.FindAll(".m-import-select"));
        Assert.Contains("Columns this import reads", cut.Markup);
    }
}
