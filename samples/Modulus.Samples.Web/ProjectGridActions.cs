using Microsoft.AspNetCore.Http;
using Modulus.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.Samples.Web;

/// <summary>
/// The POST behind the editable grid (MVC <c>TablesController.Editable</c> and the Razor Pages
/// <c>Tables/Editable</c> page): applies the clicked operation to the posted grid and returns the page to render,
/// with a toast message when there is something to say.
/// </summary>
public static class ProjectGridActions
{
    public const string FieldName = "grid";

    public static (ProjectGridPage Page, MessageType? Type, string? Message) Handle(IFormCollection form, ProjectGridStore store)
    {
        var post = EditableGridForm.Read(form, ProjectGridStore.SeedColumns(), FieldName);
        switch (post.Verb)
        {
            case "save":
                var errors = GridSchema.Validate(post.Columns, post.Rows);
                if (errors.Count > 0)
                    return (new ProjectGridPage(post.Columns, post.Rows, errors, true), MessageType.Error, "Fix the highlighted cells, then save again.");
                store.Save(post.Columns, post.Rows);
                return (ProjectGridPage.Saved(store), MessageType.Success, "Projects saved.");
            case "discard" or "":
                return (ProjectGridPage.Saved(store), null, null);
            case "reset":
                store.Reset();
                return (ProjectGridPage.Saved(store), MessageType.Info, "Sample data restored.");
            default:
                EditableGridForm.Apply(post, form, FieldName);
                return (new ProjectGridPage(post.Columns, post.Rows, [], true), null, null);
        }
    }
}
