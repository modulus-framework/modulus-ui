using System.Globalization;
using Modulus.Ui.Abstractions.Printing;

namespace Modulus.Samples.Shared;

/// <summary>What a printed page shows: a parts list that runs over several pages (its header repeats), and bin labels for an A4 sheet.</summary>
public static class PrintSamples
{
    private static readonly string[] Descriptions =
    [
        "Hex bolt M8 x 40", "Flange nut M8", "Washer 8 mm", "Cable tie 200 mm", "Pipe clamp 22 mm",
        "Gasket set, small", "Bearing 6204", "Spring pin 4 x 20", "Seal ring 35 mm", "Grease cartridge",
    ];

    /// <summary>The parts list: 120 rows, deterministic, so the printed numbers are the same every run.</summary>
    public static IReadOnlyList<PrintColumn> PartsColumns { get; } =
    [
        new("Part"),
        new("Description"),
        new("Qty", Right: true),
        new("Unit price", Right: true),
        new("Amount", Right: true),
    ];

    public static IReadOnlyList<IReadOnlyList<string>> PartRows { get; } = Enumerable.Range(1, 120).Select(i =>
    {
        var qty = (i * 7) % 50 + 1;
        var price = 2.5m + (i * 37 % 400) / 10m;
        return (IReadOnlyList<string>)[
            $"SKU-{i:000}",
            Descriptions[i % Descriptions.Length],
            qty.ToString(CultureInfo.InvariantCulture),
            price.ToString("N2", CultureInfo.InvariantCulture),
            (qty * price).ToString("N2", CultureInfo.InvariantCulture),
        ];
    }).ToArray();

    /// <summary>The total row: the quantity and the amount of every part.</summary>
    public static IReadOnlyList<string> PartTotal { get; } =
    [
        $"Total, {PartRows.Count} parts",
        "",
        PartRows.Sum(r => int.Parse(r[2], CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture),
        "",
        PartRows.Sum(r => decimal.Parse(r[4], NumberStyles.Number, CultureInfo.InvariantCulture)).ToString("N2", CultureInfo.InvariantCulture),
    ];

    /// <summary>Bin labels, thirty of them: one and a half A4 sheets of 21.</summary>
    public static IReadOnlyList<LabelItem> Labels { get; } = Enumerable.Range(1, 30).Select(i =>
        new LabelItem([$"Bin A-{i:00}", $"Zone {(char)('A' + (i - 1) / 10)}", $"Rack {(i - 1) % 10 + 1}, shelf {(i % 4) + 1}"])).ToArray();

    /// <summary>The print view's content: the table or the labels, with the page rule the view sets.</summary>
    public static PrintSheetPage Sheet(string? kind) => kind == "labels"
        ? new PrintSheetPage("Bin labels", PrintCss.Page(new PrintOptions(MarginMm: 0)), true, [], [], null, Labels, LabelLayout.A4ThreeBySeven)
        : new PrintSheetPage("Parts list", PrintCss.Page(new PrintOptions()), false, PartsColumns, PartRows, PartTotal, [], LabelLayout.A4ThreeBySeven);

    /// <summary>The overview page's counts.</summary>
    public static PrintOverview Overview { get; } = new(PartRows.Count, Labels.Count, LabelLayout.A4ThreeBySeven.PerSheet);
}

/// <summary>A print view's content. <see cref="Labels"/> says which of the two it is; the other's fields are empty.</summary>
public sealed record PrintSheetPage(
    string Title,
    string PageCss,
    bool Labels,
    IReadOnlyList<PrintColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    IReadOnlyList<string>? Total,
    IReadOnlyList<LabelItem> Items,
    LabelLayout Layout);

/// <summary>The counts the printing overview shows.</summary>
public sealed record PrintOverview(int Parts, int Labels, int LabelsPerSheet);
