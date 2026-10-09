using System.Globalization;
using System.Text.RegularExpressions;
using Modulus.Documents;
using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.Samples.Shared;

// Packing lists for a garment buyer (merchandising, /merchandising/packing-lists): an Excel template with ERP fields, filled
// from a purchase order, and the result as xlsx or PDF. The template is one sheet of rows; a {{Items[]...}} row repeats once
// per order line, and every other {{field}} takes its value from the order. A field that the order does not have is an error.

/// <summary>One colour, size and carton of an order.</summary>
public sealed record PackingLine(string Style, string Color, string Size, int Quantity, string CartonNo, decimal GrossWeight, decimal NetWeight, string Remarks = "-", string Upc = "");

/// <summary>A purchase order to pack: its header, its lines and the cartons it fills.</summary>
public sealed record PackingOrder(string Number, string Buyer, DateTime PoDate, DateTime Shipment, string StyleCode, string StyleName, string Color, string Season, int Cartons, IReadOnlyList<PackingLine> Lines);

/// <summary>A saved packing list template: the buyer's layout, its version and whether it is in use.</summary>
/// The layout is the template sheet: five header rows, a blank row, the column headings, one item row and the total row.
public sealed record PackingTemplate(string Key, string Name, string Buyer, string Version, string Status, string[][] Layout);

/// <summary>One ERP field the template can insert: its token, shown as <c>{{Token}}</c>, and its label.</summary>
public sealed record PackingField(string Token, string Label);

/// <summary>A group of fields in the Insert ERP Field panel.</summary>
public sealed record PackingFieldGroup(string Title, IReadOnlyList<PackingField> Fields);

/// <summary>The result of filling a template: the rows of the sheet, and the fields that could not be filled.</summary>
public sealed record PackingFill(IReadOnlyList<IReadOnlyList<object?>> Rows, IReadOnlyList<string> Errors);

public static partial class PackingListDemo
{
    public const string Company = "Demo Fashion Ltd.";
    public const string DownloadUrl = "/merchandising/packing-lists/download";

    public static readonly string[] Buyers = ["H&M", "Zara", "Walmart", "Generic"];
    public static readonly string[] Statuses = ["Active", "Draft"];
    public static readonly string[] Formats = ["xlsx", "pdf"];

    public static PackingTemplate[] Templates =>
    [
        new("hm", "H&M Packing List", "H&M", "v3", "Active", TemplateRows),
        new("zara", "Zara Packing List", "Zara", "v2", "Active", ZaraLayout),
        new("walmart", "Walmart Packing List", "Walmart", "v5", "Active", WalmartLayout),
        new("basic", "Basic Packing List", "Generic", "v1", "Active", BasicLayout),
    ];

    public static readonly PackingOrder[] Orders =
    [
        new("PO-2025-001", "H&M", new DateTime(2025, 8, 25), new DateTime(2025, 8, 30), "ST-00123", "Crew neck tee", "White / Black", "SS26", 52,
        [
            new("ST-00123", "White", "S", 500, "1-10", 120.5m, 110.0m),
            new("ST-00123", "White", "M", 600, "11-20", 145.2m, 132.0m),
            new("ST-00123", "White", "L", 600, "21-30", 145.2m, 132.0m),
            new("ST-00123", "Black", "S", 400, "31-38", 98.3m, 88.0m),
            new("ST-00123", "Black", "M", 500, "39-45", 122.6m, 112.8m),
            new("ST-00123", "Black", "L", 500, "46-52", 125.4m, 112.8m),
        ]),
        new("PO-2025-004", "Zara", new DateTime(2025, 9, 2), new DateTime(2025, 9, 15), "ST-00456", "Relaxed shirt", "Navy", "AW25", 24,
        [
            new("ST-00456", "Navy", "M", 240, "1-8", 62.0m, 55.4m),
            new("ST-00456", "Navy", "L", 240, "9-16", 64.5m, 57.2m),
            new("ST-00456", "Navy", "XL", 180, "17-24", 58.8m, 52.1m),
        ]),
        new("PO-2025-007", "Walmart", new DateTime(2025, 9, 10), new DateTime(2025, 9, 26), "WM-88012", "Cotton polo", "Blue", "SS26", 12,
        [
            new("WM-88012", "Blue", "S", 300, "1-4", 48.0m, 42.5m, "-", "0123456789012"),
            new("WM-88012", "Blue", "M", 400, "5-8", 64.0m, 56.8m, "-", "0123456789029"),
            new("WM-88012", "Blue", "L", 300, "9-12", 52.5m, 46.1m, "-", "0123456789036"),
        ]),
    ];

    /// <summary>The fields the Insert ERP Field panel offers, grouped as in the design.</summary>
    public static readonly PackingFieldGroup[] FieldGroups =
    [
        new("Order", [new("PO.Number", "Order no"), new("Buyer.Name", "Buyer name"), new("PO.Date", "PO date"), new("Shipment.Date", "Shipment date")]),
        new("Style", [new("Style.Code", "Style code"), new("Style.Name", "Style name"), new("Color.Name", "Colour name"), new("Season", "Season")]),
        new("Item", [new("Items[].Size", "Size"), new("Items[].Quantity", "Quantity"), new("Items[].UOM", "UOM")]),
        new("Carton", [new("Items[].CartonNo", "Carton no"), new("Items[].GW", "Gross weight"), new("Items[].NW", "Net weight")]),
        new("Totals", [new("Total.Quantity", "Total quantity"), new("Total.Cartons", "Total cartons"), new("Total.GrossWeight", "Total gross weight"), new("Total.NetWeight", "Total net weight")]),
    ];

    /// <summary>The template sheet: the header block, the column headings, one item row (repeated per line), and the total row.</summary>
    public static readonly string[][] TemplateRows =
    [
        ["PACKING LIST", "", "", "", "", "", "", "Date:", "{{Shipment.Date}}"],
        ["Buyer:", "{{Buyer.Name}}", "", "", "", "Shipment Date:", "", "", "{{Shipment.Date}}"],
        ["PO No.:", "{{PO.Number}}", "", "", "", "Total Cartons:", "", "", "{{Total.Cartons}}"],
        ["Style:", "{{Style.Code}}", "", "", "", "Total Gross Weight:", "", "", "{{Total.GrossWeight}} KGS"],
        ["Color:", "{{Color.Name}}", "", "", "", "Total Net Weight:", "", "", "{{Total.NetWeight}} KGS"],
        ["", "", "", "", "", "", "", "", ""],
        ["#", "Style", "Color", "Size", "Quantity", "Carton No.", "G.W. (KGS)", "N.W. (KGS)", "Remarks"],
        ["{{Items[].No}}", "{{Items[].Style}}", "{{Items[].Color}}", "{{Items[].Size}}", "{{Items[].Quantity}}", "{{Items[].CartonNo}}", "{{Items[].GW}}", "{{Items[].NW}}", "{{Items[].Remarks}}"],
        ["Total", "", "", "", "{{Total.Quantity}}", "{{Total.Cartons}}", "{{Total.GrossWeight}}", "{{Total.NetWeight}}", ""],
    ];

    /// <summary>The template as the Excel editor shows it: one sheet, its header rows frozen above the table.</summary>
    public static IReadOnlyList<SpreadsheetSheet> TemplateSheet(string? template) =>
        [new SpreadsheetSheet("PackingList", LayoutOf(template).Select(r => (IReadOnlyList<object?>)r.Cast<object?>().ToList()).ToList(),
            FrozenRows: 7, ColumnWidths: [40, 110, 90, 60, 80, 90, 80, 80, 120])];

    /// <summary>Zara's sheet: its own headings, no remarks column, the season in the header.</summary>
    public static readonly string[][] ZaraLayout =
    [
        ["ZARA PACKING LIST", "", "", "", "", "", "", "Date:", "{{Shipment.Date}}"],
        ["Supplier:", "{{Company.Name}}", "", "", "", "Season:", "", "", "{{Season}}"],
        ["Order:", "{{PO.Number}}", "", "", "", "Cartons:", "", "", "{{Total.Cartons}}"],
        ["Article:", "{{Style.Code}}", "", "", "", "Gross / Net (kg):", "", "", "{{Total.GrossWeight}} / {{Total.NetWeight}}"],
        ["Colour:", "{{Color.Name}}", "", "", "", "Shipment:", "", "", "{{Shipment.Date}}"],
        ["", "", "", "", "", "", "", "", ""],
        ["Line", "Article", "Colour", "Size", "Qty", "Carton", "Gross (kg)", "Net (kg)"],
        ["{{Items[].No}}", "{{Items[].Style}}", "{{Items[].Color}}", "{{Items[].Size}}", "{{Items[].Quantity}}", "{{Items[].CartonNo}}", "{{Items[].GW}}", "{{Items[].NW}}"],
        ["Total", "", "", "", "{{Total.Quantity}}", "{{Total.Cartons}}", "{{Total.GrossWeight}}", "{{Total.NetWeight}}"],
    ];

    /// <summary>Walmart's sheet: a UPC column for each size, and the department in the header.</summary>
    public static readonly string[][] WalmartLayout =
    [
        ["WALMART PACKING LIST", "", "", "", "", "", "", "Date:", "{{Shipment.Date}}"],
        ["Vendor:", "{{Company.Name}}", "", "", "", "Department:", "", "", "{{Style.Name}}"],
        ["PO No.:", "{{PO.Number}}", "", "", "", "Total Cartons:", "", "", "{{Total.Cartons}}"],
        ["Style:", "{{Style.Code}}", "", "", "", "Total Gross Weight:", "", "", "{{Total.GrossWeight}} KGS"],
        ["Colour:", "{{Color.Name}}", "", "", "", "Total Net Weight:", "", "", "{{Total.NetWeight}} KGS"],
        ["", "", "", "", "", "", "", "", ""],
        ["Line", "UPC", "Article", "Colour", "Size", "Qty", "Carton", "Gross (kg)", "Net (kg)"],
        ["{{Items[].No}}", "{{Items[].Upc}}", "{{Items[].Style}}", "{{Items[].Color}}", "{{Items[].Size}}", "{{Items[].Quantity}}", "{{Items[].CartonNo}}", "{{Items[].GW}}", "{{Items[].NW}}"],
        ["Total", "", "", "", "", "{{Total.Quantity}}", "{{Total.Cartons}}", "{{Total.GrossWeight}}", "{{Total.NetWeight}}"],
    ];

    /// <summary>The basic sheet: a quantity line in the header, no weights.</summary>
    public static readonly string[][] BasicLayout =
    [
        ["PACKING LIST", "", "", "", "", "", "", "Date:", "{{Shipment.Date}}"],
        ["Buyer:", "{{Buyer.Name}}", "", "", "", "", "", "", ""],
        ["PO No.:", "{{PO.Number}}", "", "", "", "", "", "", ""],
        ["Style:", "{{Style.Code}}", "", "", "", "", "", "", ""],
        ["Quantity:", "{{Total.Quantity}}", "", "", "", "Cartons:", "", "", "{{Total.Cartons}}"],
        ["", "", "", "", "", "", "", "", ""],
        ["#", "Style", "Color", "Size", "Quantity", "Carton", "Remarks"],
        ["{{Items[].No}}", "{{Items[].Style}}", "{{Items[].Color}}", "{{Items[].Size}}", "{{Items[].Quantity}}", "{{Items[].CartonNo}}", "{{Items[].Remarks}}"],
        ["Total", "", "", "", "{{Total.Quantity}}", "{{Total.Cartons}}", ""],
    ];

    private static readonly Regex Token = new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

    /// <summary>The field names the template may use: the panel's fields and the item columns.</summary>
    public static IEnumerable<string> KnownTokens =>
        FieldGroups.SelectMany(g => g.Fields.Select(f => f.Token))
            .Concat(["Items[].No", "Items[].Style", "Items[].Color", "Items[].Quantity", "Items[].CartonNo", "Items[].GW", "Items[].NW", "Items[].Remarks", "Items[].Upc"])
            .Distinct();

    /// <summary>Fills the template for one order. Unknown fields stay in the sheet and are listed as errors.</summary>
    public static PackingFill Fill(PackingOrder order) => FillWith(order, TemplateRows);

    /// <summary>The layout of a template by key (the H&amp;M layout when the key is unknown).</summary>
    public static string[][] LayoutOf(string? key) => Templates.FirstOrDefault(t => t.Key == key)?.Layout ?? TemplateRows;

    /// <summary>Fills the layout of the given template for one order.</summary>
    public static PackingFill Fill(PackingOrder order, string? template) => FillWith(order, LayoutOf(template));

    /// <summary>Fills the given template rows for one order (the sheet the page shows, or a copy of it).</summary>
    public static PackingFill FillWith(PackingOrder order, string[][] templateRows)
    {
        var scalars = new Dictionary<string, object?>
        {
            ["Company.Name"] = Company,
            ["PO.Number"] = order.Number,
            ["Buyer.Name"] = order.Buyer,
            ["PO.Date"] = order.PoDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            ["Shipment.Date"] = order.Shipment.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            ["Style.Code"] = order.StyleCode,
            ["Style.Name"] = order.StyleName,
            ["Color.Name"] = order.Color,
            ["Season"] = order.Season,
            ["Total.Quantity"] = order.Lines.Sum(l => l.Quantity),
            ["Total.Cartons"] = order.Cartons,
            ["Total.GrossWeight"] = order.Lines.Sum(l => l.GrossWeight).ToString("0.0", CultureInfo.InvariantCulture),
            ["Total.NetWeight"] = order.Lines.Sum(l => l.NetWeight).ToString("0.0", CultureInfo.InvariantCulture),
        };
        var errors = new List<string>();
        var rows = new List<IReadOnlyList<object?>>();

        foreach (var template in templateRows)
        {
            if (template.Any(c => c.Contains("{{Items[].")))
            {
                for (var n = 0; n < order.Lines.Count; n++)
                {
                    var line = order.Lines[n];
                    var items = new Dictionary<string, object?>
                    {
                        ["Items[].No"] = n + 1,
                        ["Items[].Style"] = line.Style,
                        ["Items[].Color"] = line.Color,
                        ["Items[].Size"] = line.Size,
                        ["Items[].Quantity"] = line.Quantity,
                        ["Items[].CartonNo"] = line.CartonNo,
                        ["Items[].GW"] = line.GrossWeight.ToString("0.0", CultureInfo.InvariantCulture),
                        ["Items[].NW"] = line.NetWeight.ToString("0.0", CultureInfo.InvariantCulture),
                        ["Items[].Remarks"] = line.Remarks,
                        ["Items[].Upc"] = line.Upc,
                    };
                    rows.Add(template.Select(c => Resolve(c, items, scalars, errors)).ToList());
                }
            }
            else
            {
                rows.Add(template.Select(c => Resolve(c, scalars, scalars, errors)).ToList());
            }
        }
        return new PackingFill(rows, errors.Distinct().ToList());
    }

    // A cell that is one token takes the field's value (a number stays a number); a token inside text is replaced in the text.
    private static object? Resolve(string cell, IDictionary<string, object?> first, IDictionary<string, object?> second, List<string> errors)
    {
        var whole = Token.Match(cell);
        if (whole.Success && whole.Length == cell.Length)
            return Lookup(whole.Groups[1].Value, first, second, errors);
        if (!cell.Contains("{{")) return cell;

        return Token.Replace(cell, m => Lookup(m.Groups[1].Value, first, second, errors)?.ToString() ?? "");
    }

    private static object? Lookup(string name, IDictionary<string, object?> first, IDictionary<string, object?> second, List<string> errors)
    {
        if (first.TryGetValue(name, out var value) || second.TryGetValue(name, out value)) return value;
        errors.Add($"Unmapped field {{{{{name}}}}}");
        return "{{" + name + "}}";
    }

    /// <summary>The filled template as an Excel workbook.</summary>
    public static byte[] Excel(PackingOrder order, string? template) =>
        SpreadsheetXlsx.Write([new SpreadsheetSheet("PackingList", Fill(order, template).Rows, ColumnWidths: [40, 110, 90, 60, 80, 90, 80, 80, 120])]);

    /// <summary>The filled template as a PDF: the header lines, then the item table with its total.</summary>
    public static byte[] Pdf(PackingOrder order, string? template)
    {
        var fill = Fill(order, template);
        var header = fill.Rows.Take(5).Select(r => string.Join("   ", r.Where(c => c is not null && c.ToString()!.Length > 0).Select(c => c!.ToString()))).ToArray();
        var table = fill.Rows.Skip(6).Select(r => r.Select(c => c?.ToString() ?? "").ToArray()).ToArray();
        return DocumentPdf.Write(
        [
            DocBlock.Title("Packing list"),
            .. header.Select(h => DocBlock.Paragraph(h)),
            DocBlock.Table(table),
        ]);
    }

    /// <summary>The download of a packing list: the order's file in the requested format, or null when the order is unknown.</summary>
    public static (byte[] Bytes, string ContentType, string FileName)? Download(string? number, string? format, string? template)
    {
        var order = Orders.FirstOrDefault(o => o.Number == number);
        if (order is null) return null;
        var name = $"Packing_List_{order.Buyer.Replace("&", "")}_{order.Number}";
        return format == "pdf"
            ? (Pdf(order, template), DocumentPdf.ContentType, name + ".pdf")
            : (Excel(order, template), SpreadsheetXlsx.ContentType, name + ".xlsx");
    }
}
