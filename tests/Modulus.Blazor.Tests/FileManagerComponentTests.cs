using AngleSharp.Dom;
using AntDesign;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions.Files;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The Blazor FileManager: the listing draws, folders open, names are checked before they are sent, and deletes confirm first.</summary>
public class FileManagerComponentTests : BunitContext
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0);

    private static readonly FileEntry[] Tree =
    [
        new(1, null, "Documents", true),
        new(2, null, "Zebra.txt", false, 10),
        new(3, 1, "Reports", true),
    ];

    public FileManagerComponentTests()
    {
        // The AntDesign modal and dropdown measure themselves after render; loose interop answers with empty results.
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<AntDesign.JsInterop.HtmlElement>(_ => true).SetResult(new AntDesign.JsInterop.HtmlElement());
        JSInterop.Setup<AntDesign.JsInterop.DomRect>(_ => true).SetResult(new AntDesign.JsInterop.DomRect());
        Services.AddAntDesign();
    }

    private IRenderedComponent<FileManager> Mount(int? folder = null, Func<FileNavigation, Task>? onNavigate = null,
        Func<FileCreate, Task>? onCreate = null, Func<FileRename, Task>? onRename = null, Func<int, Task>? onDelete = null) =>
        Render<FileManager>(p => p
            .Add(x => x.Entries, Tree)
            .Add(x => x.Folder, folder)
            .Add(x => x.Now, Now)
            .Add(x => x.OnNavigate, EventCallback.Factory.Create(this, onNavigate ?? (_ => Task.CompletedTask)))
            .Add(x => x.OnCreate, EventCallback.Factory.Create(this, onCreate ?? (_ => Task.CompletedTask)))
            .Add(x => x.OnRename, EventCallback.Factory.Create(this, onRename ?? (_ => Task.CompletedTask)))
            .Add(x => x.OnDelete, EventCallback.Factory.Create(this, onDelete ?? (_ => Task.CompletedTask))));

    [Fact]
    public void DrawsTheTopLevel_FoldersFirst()
    {
        var cut = Mount();

        Assert.Equal(["Documents", "Zebra.txt"], cut.FindAll(".m-files-row:not(.m-files-head) .m-files-name").Select(n => n.TextContent.Trim()));
        Assert.Equal(["Files"], cut.FindAll(".m-files-crumb").Select(c => c.TextContent.Trim()));
    }

    [Fact]
    public void OpeningAFolder_AsksForIt()
    {
        FileNavigation? asked = null;
        var cut = Mount(onNavigate: n =>
        {
            asked = n;
            return Task.CompletedTask;
        });

        cut.Find("button.m-files-open").Click();

        Assert.Equal(new FileNavigation(1), asked);
    }

    [Fact]
    public void AFolderShowsItsChildren_WithABreadcrumbBackUp()
    {
        var cut = Mount(folder: 1);

        Assert.Equal(["Files", "Documents"], cut.FindAll(".m-files-crumb").Select(c => c.TextContent.Trim()));
        Assert.Single(cut.FindAll(".m-files-open"));
    }

    [Fact]
    public void ANameThatBreaksTheRules_IsNotSent()
    {
        FileCreate? created = null;
        var cut = Mount(onCreate: c =>
        {
            created = c;
            return Task.CompletedTask;
        });

        cut.Find("button.ant-btn-primary").Click();
        cut.Find(".m-files-create input").Change("documents");
        cut.Find("form.m-files-create").TriggerEvent("onsubmit", new EventArgs());

        Assert.Null(created);
        Assert.Contains("already here", cut.Markup);
    }

    [Fact]
    public void ANewFolder_IsSentWithItsParent()
    {
        FileCreate? created = null;
        var cut = Mount(folder: 1, onCreate: c =>
        {
            created = c;
            return Task.CompletedTask;
        });

        cut.Find("button.ant-btn-primary").Click();
        cut.Find(".m-files-create input").Change("  Drafts ");
        cut.Find("form.m-files-create").TriggerEvent("onsubmit", new EventArgs());

        Assert.Equal(new FileCreate(1, "Drafts"), created);
    }

    [Fact]
    public void ARename_IsSentWithTheNewName()
    {
        FileRename? renamed = null;
        var cut = Mount(onRename: r =>
        {
            renamed = r;
            return Task.CompletedTask;
        });

        cut.Find("button[aria-label='Rename Zebra.txt']").Click();
        cut.Find(".m-files-rename input").Change("Zebra2.txt");
        cut.Find("form.m-files-rename").TriggerEvent("onsubmit", new EventArgs());

        Assert.Equal(new FileRename(2, "Zebra2.txt"), renamed);
    }

    [Fact]
    public void ADelete_WaitsForTheConfirmation()
    {
        var deleted = new List<int>();
        var cut = Mount(onDelete: id =>
        {
            deleted.Add(id);
            return Task.CompletedTask;
        });

        cut.Find("button[aria-label='Delete Documents']").Click();
        Assert.Empty(deleted);
        Assert.Contains("everything in it", cut.Markup);
    }
}
