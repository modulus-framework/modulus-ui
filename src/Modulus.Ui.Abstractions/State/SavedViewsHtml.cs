using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.Ui.Abstractions.State;

/// <summary>What the "Views" dropdown of a page shows.</summary>
/// <param name="Id">Element id of the dropdown (its form is <c>{Id}-form</c>).</param>
/// <param name="Page">The page key the views are kept under (usually its path).</param>
/// <param name="Path">The page's path: a view's link is this path with the view's query.</param>
/// <param name="Query">The current URL's query.</param>
/// <param name="Views">The user's views of the page.</param>
/// <param name="CanSave">False for an anonymous user: the list shows (empty), the save form does not.</param>
/// <param name="Error">A message from the last save, shown above the form.</param>
public sealed record SavedViewsModel(string Id, string Page, string Path, IReadOnlyList<KeyValuePair<string, string?>> Query,
    IReadOnlyList<SavedView> Views, bool CanSave, string? Error = null)
{
    /// <summary>The view the current URL shows, if it is one of the saved ones.</summary>
    public SavedView? Current { get; } = SavedViews.Current(Views, Query);
}

/// <summary>
/// The "Views" dropdown (Odoo's favourites) of a list, grid or pivot page: the saved views as links, a star that makes
/// one the default, delete, and "Save current view" with a name. The MVC <c>&lt;app-saved-views&gt;</c> renders it with
/// one form that posts to <see cref="Endpoint"/>; the Blazor <c>AppSavedViews</c> renders the same DOM with buttons.
/// </summary>
public static class SavedViewsHtml
{
    /// <summary>Where the MVC form posts (<c>MapModulusSavedViews</c>).</summary>
    public const string Endpoint = "/_modulus/views";

    /// <summary>The query key that carries a save error back to the page (never part of a view).</summary>
    public const string ErrorKey = "views-error";

    /// <summary>The dropdown for MVC: buttons and inputs submit the hidden form (<c>form="{id}-form"</c>), so it nests nowhere.</summary>
    /// <param name="antiforgery">The antiforgery field's name and token, rendered into the form.</param>
    public static string Render(SavedViewsModel model, (string Name, string Token)? antiforgery = null)
    {
        var form = model.Id + "-form";
        var sb = new StringBuilder();
        Open(sb, model);
        foreach (var view in model.Views)
        {
            Item(sb, model, view);
            sb.Append($"""<button type="submit" form="{E(form)}" name="op" value="{E("default:" + view.Name)}" class="m-views-action{(view.IsDefault ? " m-views-action-on" : "")}" title="{(view.IsDefault ? "Default view (click to clear)" : "Open the page in this view")}" aria-label="{E((view.IsDefault ? "Clear default: " : "Make default: ") + view.Name)}" aria-pressed="{(view.IsDefault ? "true" : "false")}">{Star(view.IsDefault)}</button>""")
              .Append($"""<button type="submit" form="{E(form)}" name="op" value="{E("delete:" + view.Name)}" class="m-views-action m-views-delete" title="Delete" aria-label="{E("Delete view: " + view.Name)}">{ShellRenderer.Icon("delete")}</button>""")
              .Append("</li>");
        }
        Middle(sb, model);
        if (model.CanSave)
        {
            sb.Append("""<div class="m-views-save">""")
              .Append($"""<input type="text" class="ant-input ant-input-sm" name="name" form="{E(form)}" required maxlength="{SavedViews.MaxName}" placeholder="Name of this view" aria-label="Name of this view" value="{E(model.Current?.Name)}" />""")
              .Append($"""<label class="m-views-default"><input type="checkbox" name="default" value="true" form="{E(form)}" /> <span>Open by default</span></label>""")
              .Append($"""<button type="submit" form="{E(form)}" name="op" value="save" class="ant-btn ant-btn-primary ant-btn-sm">Save current view</button>""")
              .Append("</div>");
        }
        sb.Append("</div>");
        sb.Append($"""<form id="{E(form)}" method="post" action="{Endpoint}" hidden>""")
          .Append(Hidden("page", model.Page)).Append(Hidden("path", model.Path))
          .Append(Hidden("query", SavedViews.Canonical(model.Query)));
        if (antiforgery is { } af) sb.Append(Hidden(af.Name, af.Token));
        sb.Append("</form></details>");
        return sb.ToString();
    }

    /// <summary>The dropdown up to the first view (shared with the Blazor component).</summary>
    public static void Open(StringBuilder sb, SavedViewsModel model)
    {
        var current = model.Current;
        sb.Append($"""<details class="m-dropdown m-views" id="{E(model.Id)}"{(model.Error is null ? "" : " open")}>""")
          .Append($"""<summary class="ant-btn ant-btn-default m-dropdown-trigger{(current is null ? "" : " m-views-trigger-on")}" aria-haspopup="menu" title="Saved views">""")
          .Append(Star(current is not null))
          .Append($"<span>{E(current?.Name ?? "Views")}</span>").Append(ShellRenderer.Icon("down"))
          .Append("""</summary><div class="ant-dropdown m-dropdown-panel m-dropdown-left m-views-panel">""")
          .Append("""<ul class="ant-dropdown-menu ant-dropdown-menu-root ant-dropdown-menu-vertical ant-dropdown-menu-light m-views-list" role="menu" aria-label="Saved views">""");
    }

    /// <summary>A view's row up to its actions: the link (current view selected, default starred).</summary>
    public static void Item(StringBuilder sb, SavedViewsModel model, SavedView view)
    {
        var on = model.Current == view;
        sb.Append($"""<li class="ant-dropdown-menu-item m-views-item{(on ? " ant-dropdown-menu-item-selected" : "")}" role="none">""")
          .Append($"""<a class="m-views-link" role="menuitem" href="{E(SavedViews.Url(model.Path, view))}"{(on ? " aria-current=\"true\"" : "")}><span>{E(view.Name)}</span></a>""");
    }

    /// <summary>Between the views and the save form: the empty state and the error.</summary>
    public static void Middle(StringBuilder sb, SavedViewsModel model)
    {
        if (model.Views.Count == 0)
            sb.Append($"""<li class="m-views-empty" role="none">{(model.CanSave ? "No saved views yet." : "Sign in to save views.")}</li>""");
        sb.Append("</ul>");
        if (model.Error is not null) sb.Append($"""<div class="m-views-error" role="alert">{E(model.Error)}</div>""");
    }

    /// <summary>A star: filled for the default (or current) view, outlined otherwise.</summary>
    public static string Star(bool filled) =>
        filled && AntIconSvg.Svg("star", AntIconSvg.Fill) is { } svg
            ? svg.Replace("<svg ", """<svg class="m-ti m-ai m-views-star m-views-star-on" data-m-icon="star" """, StringComparison.Ordinal)
            : ShellRenderer.Icon("star", "m-views-star");

    private static string Hidden(string name, string value) => $"""<input type="hidden" name="{E(name)}" value="{E(value)}" />""";

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
}
