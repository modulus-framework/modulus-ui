using System.Globalization;

namespace Modulus.Ui.Abstractions;

/// <summary>
/// The pager every Modulus table shows (data table, editable grid, nested grid levels; Blazor and MVC, mirrored by
/// modulusGrid in modulus-widgets.js): "1–10 of 42", Ant Design's page window with ••• jumps, and a page size choice.
/// </summary>
public static class Paging
{
    /// <summary>The page sizes offered ("10 / page").</summary>
    public static IReadOnlyList<int> Sizes { get; } = [10, 20, 50, 100];

    /// <summary>The page sizes offered, with <paramref name="size"/> added in order when it isn't one of them (a level's own size).</summary>
    public static IReadOnlyList<int> SizesWith(int size) =>
        size < 1 || Sizes.Contains(size) ? Sizes : [.. Sizes.Append(size).Order()];

    /// <summary>Largest page size accepted from a request.</summary>
    public const int MaxSize = 200;

    /// <summary>A page size from a request: one of <see cref="SizesWith"/> for the default, otherwise the default.</summary>
    public static int Size(int? requested, int fallback) =>
        requested is { } s && SizesWith(fallback).Contains(s) ? s : fallback;

    /// <summary>Number of pages (at least one).</summary>
    public static int Pages(int total, int size) => Math.Max(1, (int)Math.Ceiling(Math.Max(0, total) / (double)Math.Max(1, size)));

    /// <summary>The page, kept within the pages there are.</summary>
    public static int Clamp(int page, int total, int size) => Math.Clamp(page, 1, Pages(total, size));

    /// <summary>"1–10 of 42" (numbers in the current culture); "0 of 0" when empty.</summary>
    public static string Range(int page, int size, int total)
    {
        var culture = CultureInfo.CurrentCulture;
        if (total <= 0) return "0 of 0";
        var current = Clamp(page, total, size);
        var from = (current - 1) * size + 1;
        var to = Math.Min(total, current * size);
        return $"{from.ToString("N0", culture)}–{to.ToString("N0", culture)} of {total.ToString("N0", culture)}";
    }

    /// <summary>
    /// The page buttons to show, as Ant Design (rc-pagination) shows them: every page up to 7, otherwise the first and
    /// last pages, two either side of the current one (five at the ends), and 0 for a ••• jump.
    /// </summary>
    public static IReadOnlyList<int> Window(int current, int pages)
    {
        if (pages <= 7) return [.. Enumerable.Range(1, Math.Max(1, pages))];
        current = Math.Clamp(current, 1, pages);
        var left = Math.Max(1, current - 2);
        var right = Math.Min(pages, current + 2);
        if (current - 1 <= 2) right = 5;
        if (pages - current <= 2) left = pages - 4;
        var result = new List<int>();
        if (left != 1) { result.Add(1); if (left > 2) result.Add(0); }
        result.AddRange(Enumerable.Range(left, right - left + 1));
        if (right != pages) { if (right < pages - 1) result.Add(0); result.Add(pages); }
        return result;
    }
}
