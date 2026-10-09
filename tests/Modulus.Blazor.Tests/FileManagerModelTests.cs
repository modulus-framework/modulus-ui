using Modulus.Ui.Abstractions.Files;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The file manager's model: the listing and its order, the breadcrumb, the search, the name rules, and the size and date text.</summary>
public class FileManagerModelTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0);

    private static readonly FileEntry[] Tree =
    [
        new(1, null, "Documents", true),
        new(2, null, "Zebra.txt", false, 10),
        new(3, 1, "Reports", true),
        new(4, 3, "Q3.pdf", false, 2048, new DateTime(2026, 10, 9, 8, 5, 0)),
        new(5, 1, "notes.md", false, 512),
    ];

    [Fact]
    public void TheTopLevel_ListsFoldersFirst_ThenNamesInOrder()
    {
        var view = FileManagerModel.Build(Tree, null, null, Now);

        Assert.Null(view.FolderId);
        Assert.Equal(["Documents", "Zebra.txt"], view.Items.Select(i => i.Entry.Name));
        Assert.Equal([new FileCrumb(null, "Files")], view.Crumbs);
    }

    [Fact]
    public void AFolder_ShowsItsChildren_AndTheBreadcrumbToIt()
    {
        var view = FileManagerModel.Build(Tree, 1, null, Now);

        Assert.Equal(1, view.FolderId);
        Assert.Equal(["Reports", "notes.md"], view.Items.Select(i => i.Entry.Name));
        Assert.Equal(["Files", "Documents"], view.Crumbs.Select(c => c.Name));
    }

    [Fact]
    public void AnUnknownOrFileFolder_ShowsTheTopLevel()
    {
        Assert.Null(FileManagerModel.Build(Tree, 99, null, Now).FolderId);
        Assert.Null(FileManagerModel.Build(Tree, 2, null, Now).FolderId);
    }

    [Fact]
    public void TheSearch_KeepsTheItemsWhoseNameContainsIt_IgnoringCase_WithinTheFolder()
    {
        var view = FileManagerModel.Build(Tree, 1, "  NOTES ", Now);

        Assert.Equal(["notes.md"], view.Items.Select(i => i.Entry.Name));
        Assert.Equal("NOTES", view.Search);
        Assert.Empty(FileManagerModel.Build(Tree, 1, "q3", Now).Items);
    }

    [Fact]
    public void NameRules_RefuseEmptyLongReservedBadAndDuplicateNames()
    {
        Assert.Equal("Give it a name.", FileManagerModel.NameError("  ", null, Tree));
        Assert.Equal($"Use {FileManagerModel.MaxNameLength} characters or fewer.", FileManagerModel.NameError(new string('a', FileManagerModel.MaxNameLength + 1), null, Tree));
        Assert.Equal("That name is reserved.", FileManagerModel.NameError("..", null, Tree));
        Assert.StartsWith("Names can't contain", FileManagerModel.NameError("a/b", null, Tree));
        Assert.Equal("A file or folder with that name is already here.", FileManagerModel.NameError("documents", null, Tree));
    }

    [Fact]
    public void NameRules_AllowASiblingNameElsewhere_AndKeepARenamedEntrysOwnName()
    {
        Assert.Null(FileManagerModel.NameError("Documents", 1, Tree));
        Assert.Null(FileManagerModel.NameError("Zebra.txt", null, Tree, exceptId: 2));
        Assert.Null(FileManagerModel.NameError("Reports", 1, Tree, exceptId: 3));
    }

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(2_411_520L, "2.3 MB")]
    [InlineData(27_262_976L, "26 MB")]
    public void SizeText_IsBytesThenKbMbGb(long bytes, string expected) =>
        Assert.Equal(expected, FileManagerModel.SizeText(bytes));

    [Fact]
    public void ModifiedText_IsTheTimeToday_YesterdayTheDayOrTheFullDate()
    {
        Assert.Equal("–", FileManagerModel.ModifiedText(null, Now));
        Assert.Equal("Today 08:05", FileManagerModel.ModifiedText(new DateTime(2026, 10, 9, 8, 5, 0), Now));
        Assert.Equal("Yesterday", FileManagerModel.ModifiedText(new DateTime(2026, 10, 8, 20, 0, 0), Now));
        Assert.Equal("Sep 3", FileManagerModel.ModifiedText(new DateTime(2026, 9, 3), Now));
        Assert.Equal("Jan 2, 2025", FileManagerModel.ModifiedText(new DateTime(2025, 1, 2), Now));
    }

    [Theory]
    [InlineData("contract.PDF", "PDF")]
    [InlineData("plan.docx", "Document")]
    [InlineData("budget.xlsx", "Spreadsheet")]
    [InlineData("logo.svg", "Image")]
    [InlineData("assets.zip", "Archive")]
    [InlineData("readme.md", "Text")]
    [InlineData("data.bin", "File")]
    public void KindOf_IsFromTheExtension(string name, string kind) =>
        Assert.Equal(kind, FileManagerModel.KindOf(new FileEntry(1, null, name, false)));
}
