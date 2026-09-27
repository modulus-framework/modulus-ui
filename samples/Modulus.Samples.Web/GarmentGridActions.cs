using System.Globalization;
using Microsoft.AspNetCore.Http;
using Modulus.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;

namespace Modulus.Samples.Web;

/// <summary>A level of the nested grid page and the endpoint it loads from and posts to.</summary>
public sealed record NestedLevelModel(NestedGridView View, string Url);

/// <summary>The nested grid page (/tables/nested): how many levels, and the top level.</summary>
public sealed record NestedPageModel(int Depth, NestedLevelModel Top);

/// <summary>
/// The nested grid behind MVC <c>TablesController</c> and the Razor Pages <c>Tables/Nested</c> page. Every level
/// loads from and posts to <c>/tables/nested/level?depth=…&amp;level=…&amp;parent=…</c>.
/// </summary>
public static class GarmentGridActions
{
    public static int Depth(IQueryCollection query) =>
        int.TryParse(query["depth"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? Math.Clamp(d, 1, 4) : 4;

    public static string LevelUrl(int depth) => $"/tables/nested/level?depth={depth.ToString(CultureInfo.InvariantCulture)}";

    public static NestedPageModel Page(GarmentStore store, IQueryCollection query)
    {
        var depth = Depth(query);
        var settings = GarmentGrid.Settings(depth);
        return new NestedPageModel(depth, new NestedLevelModel(Load(store, settings, new NestedGridRequest(0, null, PageSize: settings.Levels[0].PageSize)), LevelUrl(depth)));
    }

    public static NestedLevelModel Get(GarmentStore store, IQueryCollection query)
    {
        var depth = Depth(query);
        var settings = GarmentGrid.Settings(depth);
        return new NestedLevelModel(Load(store, settings, NestedGridForm.ReadRequest(query, settings)), LevelUrl(depth));
    }

    /// <summary>A level's Save, Discard or row operation: the level to render again, and a toast when there is one.</summary>
    public static NestedLevelModel Post(GarmentStore store, HttpRequest http, HttpResponse response)
    {
        var depth = Depth(http.Query);
        var settings = GarmentGrid.Settings(depth);
        var request = NestedGridForm.ReadRequest(http.Query, settings);
        var form = http.Form;
        var parent = store.Find(request.ParentId);
        var post = EditableGridForm.Read(form, settings.ColumnsFor(request.Level, parent), NestedGridForm.FieldName(request.Level));
        var url = LevelUrl(depth);

        switch (post.Verb)
        {
            case "save":
                var errors = GridSchema.Validate(post.Columns, post.Rows);
                if (errors.Count > 0)
                {
                    response.HxMessage(MessageType.Error, "Fix the highlighted cells, then save again.");
                    return new NestedLevelModel(Posted(store, settings, request, post, form, parent, errors), url);
                }
                store.Save(NestedGridForm.Changes(post, form, request));
                response.HxMessage(MessageType.Success, "Saved");
                return new NestedLevelModel(Load(store, settings, request), url);
            case "discard" or "":
                return new NestedLevelModel(Load(store, settings, request), url);
            default:
                EditableGridForm.Apply(post, form, NestedGridForm.FieldName(request.Level));
                if (post.Error is { } error) response.HxMessage(MessageType.Warning, error);
                return new NestedLevelModel(Posted(store, settings, request, post, form, parent, []), url);
        }
    }

    private static NestedGridView Load(GarmentStore store, NestedGridSettings settings, NestedGridRequest request)
    {
        var parent = store.Find(request.ParentId);
        var page = store.Load(settings, request);
        return new NestedGridView(settings, request with { Page = NestedGridQuery.ClampPage(request, page.Total) },
            settings.ColumnsFor(request.Level, parent), page.Rows, page.Total, page.Rows.Select(r => r.Id).ToList(), parent,
            Title: Title(settings, request.Level, parent));
    }

    // The grid as posted (with its edits), for errors and row operations.
    private static NestedGridView Posted(GarmentStore store, NestedGridSettings settings, NestedGridRequest request, EditableGridPost post, IFormCollection form, GridRecord? parent, IReadOnlyList<GridError> errors) =>
        new(settings, request, post.Columns, post.Rows, store.Load(settings, request).Total, NestedGridForm.LoadedIds(form, request.Level), parent,
            errors, Dirty: true, Title: Title(settings, request.Level, parent));

    // "Styles · PO-1003": the level title and the parent's first column, as the Blazor NestedGrid shows it.
    private static string Title(NestedGridSettings settings, int level, GridRecord? parent)
    {
        var title = settings.Level(level)?.Title ?? "";
        if (parent is null || settings.ColumnsFor(level - 1, null).FirstOrDefault() is not { } first) return title;
        return $"{title} · {GridSchema.Format(first, parent[first.Key], CultureInfo.CurrentCulture)}";
    }
}
