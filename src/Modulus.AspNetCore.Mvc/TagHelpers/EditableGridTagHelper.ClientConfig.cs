using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Plugins;
using SortDirection = Modulus.Ui.Abstractions.SortDirection;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

public partial class EditableGridTagHelper : TagHelper
{
    // ---- Client config ----

    // The data table's pager, run in the browser by modulusGrid (pageItems, goPage, setPageSize): the range, the page
    // window with ••• jumps (Paging.Window), and rows per page.
    private string Pager()
    {
        var sizes = string.Concat(Paging.SizesWith(PageSize).Select(s =>
            $"""<option value="{s}"{(s == PageSize ? " selected" : "")}>{s} / page</option>"""));
        return """<ul class="ant-pagination ant-pagination-mini ant-table-pagination ant-table-pagination-right m-grid-pager" aria-label="Pagination" x-show="matched > 0">""" +
               """<li class="ant-pagination-total-text" x-text="rangeText()"></li>""" +
               $"""<li class="ant-pagination-prev" :class="page <= 1 && 'ant-pagination-disabled'" :aria-disabled="page <= 1"><button type="button" class="ant-pagination-item-link" aria-label="Previous page" :disabled="page <= 1" @click="goPage(page - 1)">{AntIcons.Span("left")}</button></li>""" +
               """<template x-for="item in pageItems()" :key="item.key">""" +
               """<li :class="item.cls" :aria-current="item.active ? 'page' : null" :title="item.title">""" +
               """<a :class="item.page ? '' : 'ant-pagination-item-link'" href="#" role="button" :aria-label="item.title" @click.prevent="goPage(item.to)">""" +
               """<span x-show="item.page" x-text="item.page"></span>""" +
               """<span class="ant-pagination-item-container" x-show="!item.page"><span class="ant-pagination-item-ellipsis">•••</span></span></a></li></template>""" +
               $"""<li class="ant-pagination-next" :class="page >= pages() && 'ant-pagination-disabled'" :aria-disabled="page >= pages()"><button type="button" class="ant-pagination-item-link" aria-label="Next page" :disabled="page >= pages()" @click="goPage(page + 1)">{AntIcons.Span("right")}</button></li>""" +
               $"""<li class="ant-pagination-options"><select class="m-select m-select-sm m-pager-size" aria-label="Rows per page" @change="setPageSize($el.value)">{sizes}</select></li></ul>""";
    }

    private object ClientConfig(CultureInfo culture)
    {
        string currency;
        try { currency = new RegionInfo(culture.Name).ISOCurrencySymbol; }
        catch (ArgumentException) { currency = "USD"; }
        return new
        {
            name = Name,
            lang = string.IsNullOrEmpty(culture.Name) ? "en-US" : culture.Name,
            currency,
            datePattern = culture.DateTimeFormat.ShortDatePattern,
            dirty = Dirty,
            paged = Paged,
            pageSize = Math.Max(1, PageSize),
            dragLocked = !RowDraggable || ServerSorted,
            check = AntIcons.Span("check-circle", IconThemeType.Fill),
            file = $"{ExportFileName ?? HeaderTitle ?? "grid"}.csv",
            title = HeaderTitle,
            batch = (Batch ?? []).Where(a => GridSchema.IsBatchKey(a.Key)).ToDictionary(a => a.Key, a => new { label = a.Label, confirm = a.Confirm, danger = a.Danger, print = a.Print }),
            columns = Columns.ToDictionary(c => c.Key, c => new
            {
                t = c.Title,
                type = c.Type.ToString(),
                req = c.Required,
                min = c.Min,
                max = c.Max,
                right = c.AlignRight,
                avatar = c.Avatar,
                sub = c.SubtitleKey,
                o = c.Options.ToDictionary(o => o.Value, o => new[] { o.Label, o.Color, o.Icon is null ? null : AntIcons.Span(o.Icon) }),
            }),
        };
    }

    private static string Js(bool value) => value ? "true" : "false";
}
