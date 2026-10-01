using System.Globalization;
using Microsoft.AspNetCore.Http;
using Modulus.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.Samples.Web;

/// <summary>
/// The data grid page (/tables/data-grid): the typed orders table (its columns discovered from
/// <see cref="PurchaseOrder"/> by <see cref="DataGridAuto"/>, searched, sorted and paged through the same
/// <see cref="TableQuery{T}"/> the Blazor page builds) over the same <see cref="PurchaseStore"/> that feeds the
/// schema-driven orders-and-lines grid (<see cref="PurchaseGrid.Settings"/>).
/// </summary>
public static class PurchaseGridActions
{
    public static string LevelUrl() => "/tables/data-grid/level";

    /// <summary>The page: one page of the orders, and the top level of the nested grid.</summary>
    public static PurchaseTablePage Page(PurchaseStore store, IQueryCollection query, ProTableRequest request)
    {
        request.Searches = query.ColumnSearches();
        // The store is in memory, so text compares case-insensitively (a database collation would decide instead).
        var table = DataGridAuto.Query<PurchaseOrder>("number", ignoreCase: true);
        var page = table.Page(store.Orders.AsQueryable(), request);
        var settings = PurchaseGrid.Settings;
        return new PurchaseTablePage(new ProTableResponse<PurchaseOrder> { Data = page.Items, Total = page.Total ?? 0 }, request,
            new NestedLevelModel(Level(store, settings, new NestedGridRequest(0, null, PageSize: settings.Levels[0].PageSize)), LevelUrl()));
    }

    /// <summary>A nested level, from /tables/data-grid/level.</summary>
    public static NestedLevelModel Get(PurchaseStore store, IQueryCollection query)
    {
        var settings = PurchaseGrid.Settings;
        return new NestedLevelModel(Level(store, settings, NestedGridForm.ReadRequest(query, settings)), LevelUrl());
    }

    /// <summary>A nested level's Save, Discard or row operation: the level to render again, and a toast when there is one.</summary>
    public static NestedLevelModel Post(PurchaseStore store, HttpRequest http, HttpResponse response)
    {
        var settings = PurchaseGrid.Settings;
        var request = NestedGridForm.ReadRequest(http.Query, settings);
        var form = http.Form;
        var parent = Parent(store, request);
        var post = EditableGridForm.Read(form, settings.ColumnsFor(request.Level, parent), NestedGridForm.FieldName(request.Level));
        var url = LevelUrl();

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
                return new NestedLevelModel(Level(store, settings, request), url);
            case "discard" or "":
                return new NestedLevelModel(Level(store, settings, request), url);
            default:
                EditableGridForm.Apply(post, form, NestedGridForm.FieldName(request.Level));
                if (post.Error is { } error) response.HxMessage(MessageType.Warning, error);
                return new NestedLevelModel(Posted(store, settings, request, post, form, parent, []), url);
        }
    }

    private static NestedGridView Level(PurchaseStore store, NestedGridSettings settings, NestedGridRequest request)
    {
        var parent = Parent(store, request);
        var page = store.Load(settings, request);
        return new NestedGridView(settings, request with { Page = NestedGridQuery.ClampPage(request, page.Total) },
            settings.ColumnsFor(request.Level, parent), page.Rows, page.Total, page.Rows.Select(r => r.Id).ToList(), parent,
            Title: Title(settings, request.Level, parent));
    }

    // The grid as posted (with its edits), for errors and row operations.
    private static NestedGridView Posted(PurchaseStore store, NestedGridSettings settings, NestedGridRequest request, EditableGridPost post, IFormCollection form, GridRecord? parent, IReadOnlyList<GridError> errors) =>
        new(settings, request, post.Columns, post.Rows, store.Load(settings, request).Total, NestedGridForm.LoadedIds(form, request.Level), parent,
            errors, Dirty: true, Title: Title(settings, request.Level, parent));

    // The parent order as a grid record (a level-1 ParentId is the order's record id), so the level's
    // title names it. The lines level's columns do not depend on the parent; null only loses the title suffix.
    private static GridRecord? Parent(PurchaseStore store, NestedGridRequest request) =>
        request.Level > 0 && int.TryParse(request.ParentId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? DataGridAuto.Records(store.Orders.Where(o => o.Id == id)).FirstOrDefault()
            : null;

    // "Lines · PO-1003": the level title and the parent's first column, as the Blazor NestedGrid shows it.
    private static string Title(NestedGridSettings settings, int level, GridRecord? parent)
    {
        var title = settings.Level(level)?.Title ?? "";
        if (parent is null || settings.ColumnsFor(level - 1, null).FirstOrDefault() is not { } first) return title;
        return $"{title} · {GridSchema.Format(first, parent[first.Key], CultureInfo.CurrentCulture)}";
    }
}
