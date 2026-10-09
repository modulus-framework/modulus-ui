using System.Globalization;
using Microsoft.AspNetCore.Http;
using Modulus.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;

namespace Modulus.Samples.Web;

/// <summary>
/// The tree grid behind MVC <c>TablesController</c> and the Razor Pages <c>Tables/Tree</c> page (/tables/tree). Every
/// part's children load from and post to <c>/tables/tree/level?level=…&amp;parent=…</c>, as the nested grid does.
/// </summary>
public static class TreeGridActions
{
    public const string LevelUrl = "/tables/tree/level";

    public static NestedLevelModel Page(BillOfMaterialsStore store, IQueryCollection query) =>
        new(Load(store, BillOfMaterials.Settings, new NestedGridRequest(0, null, PageSize: BillOfMaterials.Settings.Levels[0].PageSize)), LevelUrl);

    public static NestedLevelModel Get(BillOfMaterialsStore store, IQueryCollection query) =>
        new(Load(store, BillOfMaterials.Settings, NestedGridForm.ReadRequest(query, BillOfMaterials.Settings)), LevelUrl);

    /// <summary>A level's Save, Discard or row operation: the level to render again, and a toast when there is one.</summary>
    public static NestedLevelModel Post(BillOfMaterialsStore store, HttpRequest http, HttpResponse response)
    {
        var settings = BillOfMaterials.Settings;
        var request = NestedGridForm.ReadRequest(http.Query, settings);
        var form = http.Form;
        var parent = store.Find(request.ParentId);
        var post = EditableGridForm.Read(form, settings.ColumnsFor(request.Level, parent), NestedGridForm.FieldName(request.Level));

        switch (post.Verb)
        {
            case "save":
                var errors = GridSchema.Validate(post.Columns, post.Rows);
                if (errors.Count > 0)
                {
                    response.HxMessage(MessageType.Error, "Fix the highlighted cells, then save again.");
                    return new NestedLevelModel(Posted(store, settings, request, post, form, parent, errors), LevelUrl);
                }
                store.Save(NestedGridForm.Changes(post, form, request));
                response.HxMessage(MessageType.Success, "Saved");
                return new NestedLevelModel(Load(store, settings, request), LevelUrl);
            case "discard" or "":
                return new NestedLevelModel(Load(store, settings, request), LevelUrl);
            default:
                EditableGridForm.Apply(post, form, NestedGridForm.FieldName(request.Level));
                if (post.Error is { } error) response.HxMessage(MessageType.Warning, error);
                return new NestedLevelModel(Posted(store, settings, request, post, form, parent, []), LevelUrl);
        }
    }

    private static NestedGridView Load(BillOfMaterialsStore store, NestedGridSettings settings, NestedGridRequest request)
    {
        var parent = store.Find(request.ParentId);
        var page = store.Load(settings, request);
        return new NestedGridView(settings, request with { Page = NestedGridQuery.ClampPage(request, page.Total) },
            settings.ColumnsFor(request.Level, parent), page.Rows, page.Total, page.Rows.Select(r => r.Id).ToList(), parent,
            Title: Title(settings, request.Level, parent), Totals: page.Totals);
    }

    // The grid as posted (with its edits), for errors and row operations.
    private static NestedGridView Posted(BillOfMaterialsStore store, NestedGridSettings settings, NestedGridRequest request, EditableGridPost post, IFormCollection form, GridRecord? parent, IReadOnlyList<GridError> errors) =>
        new(settings, request, post.Columns, post.Rows, store.Load(settings, request).Total, NestedGridForm.LoadedIds(form, request.Level), parent,
            errors, Dirty: true, Title: Title(settings, request.Level, parent));

    // The top level is the bill itself; a child level reads "Parts · Jacket", as the Blazor NestedGrid shows it.
    private static string Title(NestedGridSettings settings, int level, GridRecord? parent)
    {
        if (parent is null) return "Bill of materials";
        var title = settings.Level(level)?.Title ?? "";
        if (settings.ColumnsFor(level - 1, null).FirstOrDefault() is not { } first) return title;
        return $"{title} · {GridSchema.Format(first, parent[first.Key], CultureInfo.CurrentCulture)}";
    }
}
