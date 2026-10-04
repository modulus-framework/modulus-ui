using System.Text.Json;
using Modulus.Documents;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Plugins;
using Xunit;

namespace Modulus.Blazor.Tests;

public class SpreadsheetXlsxTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void BudgetRoundTripsNamesNumbersTextAndFormulas()
    {
        var sheets = SpreadsheetXlsx.Read(new MemoryStream(SpreadsheetXlsx.Write(PluginsContent.Budget)));

        Assert.Equal(3, sheets.Count); // Budget, Orders and Notes
        var budget = sheets[0];
        Assert.Equal("Budget", budget.Name);
        Assert.Equal(PluginsContent.Budget[0].Rows.Count, budget.Rows.Count);
        Assert.Equal("Licences", budget.Rows[1][0]);
        Assert.Equal(1200L, budget.Rows[1][1]);
        Assert.Equal("=SUM(B2:E2)", budget.Rows[1][5]); // formulas survive as strings
    }

    [Fact]
    public void PostedJsonCellsRoundTrip()
    {
        // The sheet posts its cells as JSON; they must write exactly like the CLR values.
        var posted = JsonSerializer.Deserialize<IReadOnlyList<SpreadsheetSheet>>(
            JsonSerializer.Serialize(PluginsContent.Budget, Json), Json);
        Assert.NotNull(posted);

        var sheets = SpreadsheetXlsx.Read(new MemoryStream(SpreadsheetXlsx.Write(posted!)));
        Assert.Equal(3, sheets.Count);
        Assert.Equal("Licences", sheets[0].Rows[1][0]);
        Assert.Equal(1200L, sheets[0].Rows[1][1]);
        Assert.Equal("=SUM(B2:E2)", sheets[0].Rows[1][5]);
    }

    [Fact]
    public void FrozenPanesAndColumnWidthsRoundTrip()
    {
        var sheet = new SpreadsheetSheet("Frozen", [["Head", "B"], [1, 2]], FrozenRows: 1, FrozenColumns: 1, ColumnWidths: [140, 0, 96]);

        var back = Assert.Single(SpreadsheetXlsx.Read(new MemoryStream(SpreadsheetXlsx.Write([sheet]))));

        Assert.Equal(1, back.FrozenRows);
        Assert.Equal(1, back.FrozenColumns);
        Assert.NotNull(back.ColumnWidths);
        Assert.Equal(140, back.ColumnWidths![0]);
        Assert.Equal(0, back.ColumnWidths[1]); // the default width stays the default
    }

    [Fact]
    public void PlainSheetsKeepTheirShortJson()
    {
        // Sheets without frozen panes or widths serialise as before: just name and rows.
        var json = JsonSerializer.Serialize(new SpreadsheetSheet("A", [["x"]]), Json);
        Assert.Equal("""{"name":"A","rows":[["x"]]}""", json);

        var full = JsonSerializer.Deserialize<SpreadsheetSheet>("""{"name":"A","rows":[["x"]],"frozenRows":2,"columnWidths":[120]}""", Json);
        Assert.Equal(2, full!.FrozenRows);
        Assert.Equal([120], full.ColumnWidths!);
    }

    [Fact]
    public void BooleansDoublesAndNegativesRoundTrip()
    {
        var sheet = new SpreadsheetSheet("Mix", [[true, false, 1.5, -2, 0.1, "=1+2", "text"]]);

        var back = Assert.Single(SpreadsheetXlsx.Read(new MemoryStream(SpreadsheetXlsx.Write([sheet]))));

        var row = back.Rows[0];
        Assert.Equal(true, row[0]);
        Assert.Equal(false, row[1]);
        Assert.Equal(1.5, row[2]);
        Assert.Equal(-2L, row[3]);
        Assert.Equal(0.1, row[4]);
        Assert.Equal("=1+2", row[5]);
        Assert.Equal("text", row[6]);
    }

    [Fact]
    public void EmptyCellsReadBackAsNull()
    {
        var sheet = new SpreadsheetSheet("Holes", [["A", null, "C"], [null, "B", null]]);

        var back = Assert.Single(SpreadsheetXlsx.Read(new MemoryStream(SpreadsheetXlsx.Write([sheet]))));

        Assert.Null(back.Rows[0][1]);
        Assert.Null(back.Rows[1][0]);
        Assert.Equal(2, back.Rows[1].Count); // trailing empty cells are trimmed, as the grid trims them
        Assert.Equal("C", back.Rows[0][2]);
    }

    [Fact]
    public void SheetNamesAreShortenedUniquenedAndCleaned()
    {
        var sheets = new[] { new SpreadsheetSheet("A:B/c?*d", [["x"]]), new SpreadsheetSheet("A:B/c?*d", [["x"]]),
            new SpreadsheetSheet(new string('x', 40), [["x"]]), new SpreadsheetSheet("", [["x"]]) };

        var back = SpreadsheetXlsx.Read(new MemoryStream(SpreadsheetXlsx.Write(sheets)));

        Assert.Equal("A B c  d", back[0].Name); // Excel forbids : \ / ? * [ ]
        Assert.Equal("A B c  d (2)", back[1].Name); // duplicates get a suffix
        Assert.Equal(31, back[2].Name.Length); // Excel caps names at 31 characters
        Assert.Equal("Sheet4", back[3].Name); // blanks fall back to the sheet number
    }

    [Fact]
    public void ASheetWithoutRowsRoundTripsEmpty()
    {
        var sheets = new[] { new SpreadsheetSheet("Blank", []) };

        var back = Assert.Single(SpreadsheetXlsx.Read(new MemoryStream(SpreadsheetXlsx.Write(sheets))));

        Assert.Equal("Blank", back.Name);
        Assert.Empty(back.Rows);
    }

    [Fact]
    public void ANonWorkbookReportsTheImportMessage()
    {
        var (sheets, error) = PluginsContent.ImportSpreadsheet(new MemoryStream([1, 2, 3, 4]));

        Assert.Null(sheets);
        Assert.Equal("That file is not a readable xlsx workbook.", error);
    }

    [Fact]
    public void UploadsAreCheckedForNameSizeAndEmptiness()
    {
        Assert.Equal("The file is empty.", PluginsContent.CheckSpreadsheetUpload(null, 0));
        Assert.Equal("The file is empty.", PluginsContent.CheckSpreadsheetUpload("budget.xls", 0));
        Assert.Equal("Only .xlsx files are allowed.", PluginsContent.CheckSpreadsheetUpload("budget.csv", 80));
        Assert.Equal("Larger than 5 MB.", PluginsContent.CheckSpreadsheetUpload("budget.xlsx", PluginsContent.MaxUploadMb * 1024L * 1024 + 1));
        Assert.Null(PluginsContent.CheckSpreadsheetUpload("budget.xlsx", 80));
    }

    [Fact]
    public void TheViewFallsBackToBudgetAndBuildsUrls()
    {
        Assert.Equal("budget", PluginsContent.SpreadsheetView(null));
        Assert.Equal("budget", PluginsContent.SpreadsheetView("nonsense"));
        Assert.Equal("blank", PluginsContent.SpreadsheetView("blank"));
        Assert.Equal("/plugins/spreadsheet", PluginsContent.SpreadsheetUrl("budget"));
        Assert.Equal("/plugins/spreadsheet?view=readonly", PluginsContent.SpreadsheetUrl("readonly"));
        Assert.Equal("Sheet1", PluginsContent.BlankSheets[0].Name);
        Assert.Empty(PluginsContent.BlankSheets[0].Rows);
    }
}
