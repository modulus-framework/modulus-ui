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
        // Read-only and locked rows keep their stored values, whatever the browser posted.
        GridSchema.Protect(post.Columns, post.Rows, store.StoredRows());
        switch (post.Verb)
        {
            case "save":
                var errors = GridSchema.Validate(post.Columns, post.Rows, ProjectGridStore.CheckRow);
                if (errors.Count > 0)
                    return (new ProjectGridPage(post.Columns, post.Rows, errors, true), MessageType.Error, "Fix the highlighted cells and rows, then save again.");
                store.Save(post.Columns, post.Rows);
                return (ProjectGridPage.Saved(store), MessageType.Success, "Projects saved.");
            case "discard" or "":
                return (ProjectGridPage.Saved(store), null, null);
            case "batch":
                // Approve or Reject the selected pending rows (Print stays in the browser).
                var batch = EditableGridForm.Batch(post, ProjectGridStore.BatchActions);
                var message = batch is null ? null : store.ApplyBatch(batch);
                var type = batch is { Rows.Count: > 0 } ? MessageType.Success : batch is null ? (MessageType?)null : MessageType.Warning;
                return (new ProjectGridPage(post.Columns, post.Rows, [], store.Differs(post.Columns, post.Rows)), type, message);
            case "reset":
                store.Reset();
                return (ProjectGridPage.Saved(store), MessageType.Info, "Sample data restored.");
            default:
                EditableGridForm.Apply(post, form, FieldName);
                return (new ProjectGridPage(post.Columns, post.Rows, [], true), post.Error is null ? null : MessageType.Warning, post.Error);
        }
    }
}
