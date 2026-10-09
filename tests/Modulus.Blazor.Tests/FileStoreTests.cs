using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Files;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The file manager sample's store: the seeded tree, creates and renames with the name rules, and a folder's delete taking its contents.</summary>
public class FileStoreTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0);

    [Fact]
    public void ItStartsWithTheSeededTree()
    {
        var store = new FileStore();

        Assert.Contains(store.Entries(), e => e.Name == "Documents" && e.ParentId is null);
        Assert.True(store.Entries().Count > 5);
        Assert.Equal(3, FileManagerModel.Build(store.Entries(), null, null, Now).Items.Count(i => i.Entry.ParentId is null));
    }

    [Fact]
    public void CreateAndRename_CheckTheName_AndDeleteTakesTheContents()
    {
        var store = new FileStore();

        Assert.Null(store.Create(new FileCreate(null, "Scratch"), Now));
        Assert.Equal("A file or folder with that name is already here.", store.Create(new FileCreate(null, "scratch"), Now));
        var scratch = store.Entries().Single(e => e.Name == "Scratch");
        Assert.Null(store.Create(new FileCreate(scratch.Id, "Inside.txt", IsFolder: false), Now));
        Assert.Null(store.Rename(new FileRename(scratch.Id, "Scratch 2"), Now));
        Assert.Equal("Give it a name.", store.Rename(new FileRename(scratch.Id, " "), Now));

        Assert.True(store.Delete(scratch.Id));
        Assert.DoesNotContain(store.Entries(), e => e.Name is "Scratch 2" or "Inside.txt");
        Assert.False(store.Delete(scratch.Id));
    }
}
