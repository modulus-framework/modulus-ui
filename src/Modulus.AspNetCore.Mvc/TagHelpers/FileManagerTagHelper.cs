using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Files;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// A file manager (<see cref="FileManagerView"/>), the MVC front end of the Blazor <c>FileManager</c>:
/// <code>&lt;app-file-manager view="@Model" url="/apps/files" /&gt;</code>
/// Folders, the breadcrumb and the search are boosted GETs to <c>{url}?folder=&amp;q=</c>. New folders, renames and deletes post
/// over htmx to <c>{url}/folder</c>, <c>{url}/rename</c> and <c>{url}/delete/{id}</c>; each change fires <see cref="RefreshEvent"/>,
/// which reloads the listing.
/// </summary>
[HtmlTargetElement("app-file-manager", TagStructure = TagStructure.WithoutEndTag)]
public class FileManagerTagHelper : TagHelper
{
    /// <summary>The event a change fires (htmx) so the listing reloads.</summary>
    public const string RefreshEvent = "files:refresh";

    // Alpine state and htmx handlers: their braces are values here, not interpolation holes.
    private const string RootState = "{ creating: false }";
    private const string RowState = "{ renaming: false }";
    private const string AfterCreate = "if (event.detail.successful) { $el.reset(); creating = false }";

    /// <summary>The folder and search to draw.</summary>
    public FileManagerView? View { get; set; }

    /// <summary>The page the manager links to; its folders and searches are GETs under this path.</summary>
    public string Url { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var view = View ?? throw new InvalidOperationException("app-file-manager needs a view.");
        var url = Html.E(Url);
        var sb = new StringBuilder();
        sb.Append($"""<div id="file-manager" class="m-files" data-m-files hx-boost="true" hx-target="#file-manager" hx-select="#file-manager" hx-swap="outerHTML" hx-push-url="true" x-data="{RootState}">""");
        sb.Append($"""<div hidden hx-get="{url}{Html.E(Query(view.FolderId, view.Search))}" hx-trigger="{RefreshEvent} from:body" hx-push-url="false"></div>""");
        Bar(sb, view, url);
        Create(sb, view, url);
        List(sb, view, url);
        sb.Append("</div>");
        output.TagName = null;
        output.Content.SetHtmlContent(sb.ToString());
    }

    private static void Bar(StringBuilder sb, FileManagerView view, string url)
    {
        sb.Append("""<div class="m-files-bar"><nav class="m-files-crumbs" aria-label="Breadcrumb">""");
        for (var i = 0; i < view.Crumbs.Count; i++)
        {
            var crumb = view.Crumbs[i];
            var last = i == view.Crumbs.Count - 1;
            if (i > 0) sb.Append("""<span class="m-files-sep" aria-hidden="true">/</span>""");
            sb.Append($"""<a class="m-files-crumb" href="{url}{Html.E(Query(crumb.Id, ""))}"{(last ? " aria-current=\"page\"" : "")}>{Html.E(crumb.Name)}</a>""");
        }
        sb.Append("</nav>");
        sb.Append($"""<form class="m-files-search" method="get" action="{url}">""");
        if (view.FolderId is { } folder) sb.Append($"""<input type="hidden" name="folder" value="{folder.ToString(CultureInfo.InvariantCulture)}" />""");
        sb.Append($"""<input class="ant-input" name="q" value="{Html.E(view.Search)}" placeholder="Search this folder" aria-label="Search this folder" /></form>""");
        sb.Append($"""<button type="button" class="ant-btn ant-btn-primary" @click="creating = true">{ShellRenderer.Icon("plus")}<span>New folder</span></button>""");
        sb.Append("</div>");
    }

    private static void Create(StringBuilder sb, FileManagerView view, string url)
    {
        sb.Append($"""<form class="m-files-create" x-show="creating" x-cloak hx-post="{url}/folder" hx-swap="none" @htmx:after-request="{AfterCreate}">""");
        if (view.FolderId is { } folder) sb.Append($"""<input type="hidden" name="ParentId" value="{folder.ToString(CultureInfo.InvariantCulture)}" />""");
        sb.Append($"""<input class="ant-input" name="Name" placeholder="Folder name" aria-label="New folder name" maxlength="{FileManagerModel.MaxNameLength}" required />""");
        sb.Append("""<div class="m-flex m-gap-8"><button type="submit" class="ant-btn ant-btn-primary ant-btn-sm"><span>Create</span></button>""");
        sb.Append("""<button type="button" class="ant-btn ant-btn-default ant-btn-sm" @click="creating = false"><span>Cancel</span></button></div></form>""");
    }

    private static void List(StringBuilder sb, FileManagerView view, string url)
    {
        sb.Append("""<div class="m-files-list" role="table" aria-label="Files in this folder">""");
        sb.Append("""<div class="m-files-row m-files-head" role="row"><span role="columnheader">Name</span><span role="columnheader">Size</span><span role="columnheader">Modified</span><span role="columnheader"><span class="m-sr-only">Actions</span></span></div>""");
        foreach (var item in view.Items)
        {
            var entry = item.Entry;
            var id = entry.Id.ToString(CultureInfo.InvariantCulture);
            sb.Append($"""<div class="m-files-row" role="row" data-id="{id}" x-data="{RowState}">""");
            sb.Append("""<div class="m-files-name" role="cell">""");
            if (entry.IsFolder)
                sb.Append($"""<a class="m-files-open" href="{url}{Html.E(Query(entry.Id, ""))}">{ShellRenderer.Icon(item.Icon)}<span>{Html.E(entry.Name)}</span></a>""");
            else
                sb.Append($"""<span class="m-files-file">{ShellRenderer.Icon(item.Icon)}<span title="{Html.E(item.Kind)}">{Html.E(entry.Name)}</span></span>""");
            sb.Append($"""<form class="m-files-rename" x-show="renaming" x-cloak hx-post="{url}/rename" hx-swap="none" @htmx:after-request="if (event.detail.successful) renaming = false">""");
            sb.Append($"""<input type="hidden" name="Id" value="{id}" /><input class="ant-input" name="Name" value="{Html.E(entry.Name)}" aria-label="New name for {Html.E(entry.Name)}" maxlength="{FileManagerModel.MaxNameLength}" required />""");
            sb.Append("""<div class="m-flex m-gap-8"><button type="submit" class="ant-btn ant-btn-primary ant-btn-sm"><span>Save</span></button>""");
            sb.Append("""<button type="button" class="ant-btn ant-btn-default ant-btn-sm" @click="renaming = false"><span>Cancel</span></button></div></form>""");
            sb.Append("</div>");
            sb.Append($"""<span class="m-files-size" role="cell">{Html.E(item.SizeText)}</span>""");
            sb.Append($"""<span class="m-files-date" role="cell">{Html.E(item.ModifiedText)}</span>""");
            sb.Append("""<div class="m-files-actions" role="cell">""");
            sb.Append($"""<button type="button" class="ant-btn ant-btn-text ant-btn-sm ant-btn-icon-only" aria-label="Rename {Html.E(entry.Name)}" title="Rename" @click="renaming = true">{ShellRenderer.Icon("edit")}</button>""");
            sb.Append($"""<button type="button" class="ant-btn ant-btn-text ant-btn-sm ant-btn-icon-only" aria-label="Delete {Html.E(entry.Name)}" title="Delete" hx-post="{url}/delete/{id}" hx-swap="none" hx-confirm="{Html.E($"Delete \"{entry.Name}\"{(entry.IsFolder ? " and everything in it" : "")}? This can't be undone.")}" data-confirm-danger data-confirm-ok="Delete">{ShellRenderer.Icon("delete")}</button>""");
            sb.Append("</div></div>");
        }
        if (view.Items.Count == 0)
            sb.Append($"""<div class="m-files-empty" role="row">{Html.E(view.Search.Length > 0 ? $"Nothing here matches \"{view.Search}\"." : "This folder is empty.")}</div>""");
        sb.Append("</div>");
    }

    /// <summary>The query for a folder and search: <c>?folder=3&amp;q=text</c>, or empty for the top level with no search.</summary>
    private static string Query(int? folder, string search)
    {
        var parts = new List<string>();
        if (folder is { } f) parts.Add("folder=" + f.ToString(CultureInfo.InvariantCulture));
        if (search.Length > 0) parts.Add("q=" + Uri.EscapeDataString(search));
        return parts.Count == 0 ? "" : "?" + string.Join('&', parts);
    }
}
