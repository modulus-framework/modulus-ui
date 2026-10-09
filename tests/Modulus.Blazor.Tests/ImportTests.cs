using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Import;
using Modulus.Ui.Abstractions.Query;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The import wizard's pure side: the CSV reader, the mapping, the row checks, the steps and the markup.</summary>
public class ImportTests
{
    private static readonly GridOption[] Countries = [new("NO", "Norway"), new("SE", "Sweden")];

    private static readonly IReadOnlyList<ImportField> Fields =
    [
        new("name", "Name", QueryFieldKind.Text, Required: true, Aliases: ["Customer"]),
        new("email", "Email", QueryFieldKind.Text, Aliases: ["E-mail"]),
        new("country", "Country", QueryFieldKind.Choice, Options: Countries),
        new("credit", "Credit limit", QueryFieldKind.Number),
        new("joined", "Joined", QueryFieldKind.Date),
    ];

    // The mapping a file with the field names as headers gets.
    private static ImportMapping Mapping(params (string Key, int Column)[] pairs) => new(pairs.ToDictionary(p => p.Key, p => p.Column));

    [Fact]
    public void CsvReader_ReadsQuotedCells_EscapedQuotes_LineBreaksInsideQuotes_AndCrLf()
    {
        var table = CsvReader.Parse("Name,Note\r\n\"Lee, Ann\",\"said \"\"hi\"\"\"\r\n\"two\nlines\",x\r\n");

        Assert.Equal(["Name", "Note"], table.Headers);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(["Lee, Ann", "said \"hi\""], table.Rows[0]);
        Assert.Equal(["two\nlines", "x"], table.Rows[1]);
    }

    [Fact]
    public void CsvReader_PicksTheDelimiterTheHeaderUses_SkipsBlankLines_AndStripsTheBom()
    {
        var semicolon = CsvReader.Parse("﻿Name;Credit\n\nAda;\"1,5\"\n\n");
        var tab = CsvReader.Parse("Name\tCity\nAda\tOslo\n");

        Assert.Equal(["Name", "Credit"], semicolon.Headers);
        Assert.Single(semicolon.Rows);
        Assert.Equal(["Ada", "1,5"], semicolon.Rows[0]);
        Assert.Equal(["Name", "City"], tab.Headers);
        Assert.Equal(["Ada", "Oslo"], tab.Rows[0]);
    }

    [Fact]
    public void CsvReader_OfNothing_HasNoHeadersAndNoRows()
    {
        var table = CsvReader.Parse("   \n\n");

        Assert.Empty(table.Headers);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public void Suggest_MatchesTitlesKeysAndAliases_IgnoringCaseAndPunctuation_AndUsesEachColumnOnce()
    {
        var headers = new[] { "E-mail", "customer", "Credit limit", "Country", "Name" };

        var mapping = ImportMapping.Suggest(headers, Fields);

        Assert.Equal(0, mapping.Column("email"));
        Assert.Equal(4, mapping.Column("name"));
        Assert.Equal(2, mapping.Column("credit"));
        Assert.Equal(3, mapping.Column("country"));
        Assert.Null(mapping.Column("joined"));
    }

    [Fact]
    public void Suggest_PrefersATitleToAnAlias()
    {
        var fields = new List<ImportField> { new("name", "Name", QueryFieldKind.Text, Aliases: ["Customer"]) };

        var mapping = ImportMapping.Suggest(["Customer", "Name"], fields);

        Assert.Equal(1, mapping.Column("name"));
    }

    [Fact]
    public void Suggest_DoesNotMapTwoFieldsToOneColumn()
    {
        var fields = new List<ImportField> { new("name", "Name", QueryFieldKind.Text), new("title", "Title", QueryFieldKind.Text, Aliases: ["Name"]) };

        var mapping = ImportMapping.Suggest(["Name"], fields);

        Assert.Equal(0, mapping.Column("name"));
        Assert.Null(mapping.Column("title"));
    }

    [Fact]
    public void Read_TakesTheFormsColumns_AndIgnoresEmptyOrOutOfRangeOnes()
    {
        var posted = new Dictionary<string, string?>
        {
            ["m.name"] = "2",
            ["m.email"] = "",
            ["m.country"] = "9",
            ["m.credit"] = "x",
        };

        var mapping = ImportMapping.Read(Fields, key => posted.GetValueOrDefault(key), columnCount: 3);

        Assert.Equal(2, mapping.Column("name"));
        Assert.Null(mapping.Column("email"));
        Assert.Null(mapping.Column("country"));
        Assert.Null(mapping.Column("credit"));
    }

    [Fact]
    public void Validate_ParsesEachKind_AndReportsTheRowAndCell()
    {
        var table = new CsvTable(["Name", "Email", "Country", "Credit", "Joined"],
        [
            ["Ada", "ada@example.com", "norway", "12,500.50", "2025-03-14"],
            ["Lee", "", "Sweden", "7", "2025-02-30"],
            ["", "", "Denmark", "n/a", "14/03/2025"],
        ]);

        var preview = ImportValidator.Validate(table, Mapping(("name", 0), ("email", 1), ("country", 2), ("credit", 3), ("joined", 4)), Fields);

        Assert.Empty(preview.Problems);
        Assert.Equal(3, preview.Rows.Count);
        Assert.Equal(1, preview.Rows[0].Row);
        Assert.Empty(preview.Rows[0].Issues);
        Assert.Equal("Ada", preview.Rows[0].Get<string>("name"));
        Assert.Equal("NO", preview.Rows[0].Get<string>("country"));
        Assert.Equal(new DateOnly(2025, 3, 14), preview.Rows[0].Get<DateOnly>("joined"));

        Assert.False(preview.Rows[1].Valid);
        Assert.Contains(new ImportIssue("joined", "Use a date as yyyy-MM-dd"), preview.Rows[1].Issues);

        Assert.Contains(new ImportIssue("name", "Required"), preview.Rows[2].Issues);
        Assert.Contains(new ImportIssue("country", "Not one of Norway, Sweden"), preview.Rows[2].Issues);
        Assert.Contains(new ImportIssue("credit", "Not a number"), preview.Rows[2].Issues);
        Assert.Equal("n/a", preview.Rows[2].Values["credit"]);
        Assert.Equal(2, preview.InvalidCount);
        Assert.Equal(1, preview.ValidCount);
    }

    [Fact]
    public void Validate_NeedsEveryRequiredFieldMapped_AndAtLeastOneRow()
    {
        var table = new CsvTable(["Email"], [["ada@example.com"]]);
        Assert.Contains("Map a column to Name.", ImportValidator.Validate(table, Mapping(("email", 0)), Fields).Problems);

        var empty = ImportValidator.Validate(new CsvTable(["Name"], []), Mapping(("name", 0)), Fields);
        Assert.Equal(["The file has no data rows."], empty.Problems);
        Assert.False(empty.CanCommit);
    }

    [Fact]
    public void Validate_RefusesAFileOverTheRowLimit()
    {
        var rows = Enumerable.Range(0, ImportLimits.MaxRows + 1).Select(i => new[] { $"Customer {i}" }).ToArray();

        var preview = ImportValidator.Validate(new CsvTable(["Name"], rows), Mapping(("name", 0)), Fields);

        Assert.Empty(preview.Rows);
        Assert.Contains("The file has 2,001 rows; the limit is 2,000.", preview.Problems);
    }

    [Fact]
    public void Validate_CanCommit_OnlyWhenEveryRowIsValid()
    {
        var mapping = Mapping(("name", 0));

        var good = ImportValidator.Validate(new CsvTable(["Name"], [["Ada"], ["Lee"]]), mapping, Fields);
        var bad = ImportValidator.Validate(new CsvTable(["Name"], [["Ada"], [""]]), mapping, Fields);

        Assert.True(good.CanCommit);
        Assert.False(bad.CanCommit);
    }

    [Fact]
    public void Validate_RefusesTextLongerThanTheLimit()
    {
        var tooLong = new string('x', ImportLimits.MaxTextLength + 1);

        var preview = ImportValidator.Validate(new CsvTable(["Name"], [[tooLong]]), Mapping(("name", 0)), Fields);

        Assert.Equal("Longer than 500 characters", preview.Rows[0].Issues.Single().Message);
    }

    [Fact]
    public void Load_SuggestsTheMappingFromTheHeader()
    {
        var view = ImportFlow.Load("Customer,E-mail\nAda,ada@example.com\n", Fields);

        Assert.Equal(ImportStep.Map, view.Step);
        Assert.Null(view.Error);
        Assert.Equal(0, view.Mapping!.Column("name"));
        Assert.Equal(1, view.Mapping.Column("email"));
    }

    [Fact]
    public void Load_RefusesANoFile_ANoHeader_TooManyColumns_AndTooLargeAFile()
    {
        Assert.Equal("Choose a CSV file.", ImportFlow.Load(null, Fields).Error);
        Assert.Equal("The file has no header row.", ImportFlow.Load("\n  \n", Fields).Error);

        var wide = string.Join(',', Enumerable.Range(0, ImportLimits.MaxColumns + 1).Select(i => $"c{i}"));
        Assert.Equal($"The file has more than {ImportLimits.MaxColumns} columns.", ImportFlow.Load(wide, Fields).Error);

        var big = "Name\n" + new string('x', ImportLimits.MaxBytes);
        var refused = ImportFlow.Load(big, Fields);
        Assert.Equal(ImportStep.Upload, refused.Step);
        Assert.Equal(ImportFlow.TooLarge, refused.Error);
    }

    [Fact]
    public void Check_ReturnsThePreviewStep_WithTheRowsChecked()
    {
        var view = ImportFlow.Check("Name,Credit limit\nAda,5\n", Mapping(("name", 0), ("credit", 1)), Fields);

        Assert.Equal(ImportStep.Preview, view.Step);
        Assert.True(view.Preview!.CanCommit);
        Assert.Equal(5m, view.Preview.Rows[0].Get<decimal>("credit"));
    }

    [Fact]
    public void Html_EscapesHeadersAndTheHiddenFile_AndDisablesImportWhileRowsAreInvalid()
    {
        var csv = "Name,Credit limit\n\"<b>Ada</b>\",\"\"\"n/a\"\"\"\n";
        var table = CsvReader.Parse(csv);
        var view = ImportFlow.Check(csv, Mapping(("name", 0), ("credit", 1)), Fields) with { Table = table };

        var html = ImportWizardHtml.Render(view, "/tables/import");

        Assert.DoesNotContain("<b>Ada</b>", html);
        Assert.Contains("&lt;b&gt;Ada&lt;/b&gt;", html);
        Assert.Contains("name=\"csv\"", html);
        Assert.Contains("data-valid=\"false\"", html);
        Assert.Contains("disabled", html[html.IndexOf("value=\"import\"", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void Html_OnTheMapStep_OffersEveryHeader_AndSelectsTheMapped()
    {
        var csv = "Name,Email\nAda,ada@example.com\n";
        var view = ImportFlow.Load(csv, Fields);

        var html = ImportWizardHtml.Render(view, "/tables/import");

        Assert.Contains("<option value=\"1\" selected>Email</option>", html);
        Assert.Contains("<option value=\"0\">Name</option>", html);
        Assert.Contains("name=\"m.name\"", html);
        Assert.Contains("data-state=\"current\"", html);
    }

    [Fact]
    public void Html_DoneStep_ShowsTheMessage()
    {
        var html = ImportWizardHtml.Render(new ImportWizardView(ImportStep.Done, Fields, Message: "Imported 2."), "/tables/import");

        Assert.Contains("<p>Imported 2.</p>", html);
    }
}
