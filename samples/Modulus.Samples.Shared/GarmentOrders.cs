using Modulus.Ui.Abstractions;

namespace Modulus.Samples.Shared;

/// <summary>
/// The nested grid sample (/tables/nested): garment orders → styles → material groups → items. One settings object
/// drives every level; the items' columns come from their group's category (fabric, accessory or packing).
/// </summary>
public static class GarmentGrid
{
    public static readonly GridOption[] Buyers =
        [new("northwind", "Northwind"), new("contoso", "Contoso"), new("fabrikam", "Fabrikam"), new("tailspin", "Tailspin"), new("adatum", "Adatum")];

    public static readonly GridOption[] Seasons = [new("ss27", "SS 2027"), new("aw27", "AW 2027"), new("ss28", "SS 2028")];

    public static readonly GridOption[] OrderStatuses =
    [
        new("draft", "Draft", "default"), new("sampling", "Sampling", "processing"), new("production", "In production", "warning"),
        new("shipped", "Shipped", "success"),
    ];

    public static readonly GridOption[] Colors =
        [new("navy", "Navy", "blue"), new("black", "Black"), new("white", "White"), new("olive", "Olive", "green"), new("rust", "Rust", "volcano")];

    public static readonly GridOption[] Categories =
        [new("fabric", "Fabric", "blue", "skin"), new("accessory", "Accessory", "purple", "tag"), new("packing", "Packing", "gold", "inbox")];

    public static readonly GridOption[] Suppliers =
        [new("tessile", "Tessile Moda"), new("ykk", "YKK"), new("avery", "Avery Dennison"), new("coats", "Coats"), new("smurfit", "Smurfit Kappa")];

    public static readonly GridOption[] Sizes = [new("s", "Small"), new("m", "Medium"), new("l", "Large")];

    public static readonly GridOption[] Finishes = [new("matte", "Matte"), new("gloss", "Gloss"), new("antique", "Antique brass")];

    public static readonly GridOption[] PackingMaterials = [new("kraft", "Kraft board"), new("poly", "Recycled poly"), new("paper", "Tissue paper")];

    /// <summary>The settings for <paramref name="depth"/> levels (1 to 4): fewer levels, shallower nesting.</summary>
    public static NestedGridSettings Settings(int depth = 4) => new()
    {
        Levels = new[]
        {
            new NestedGridLevel
            {
                Title = "Orders",
                PageSize = 5,
                Columns =
                [
                    new() { Key = "order", Title = "Order", Width = 140, Fixed = GridFixed.Left, Required = true },
                    new() { Key = "buyer", Title = "Buyer", Type = GridCellType.Select, Width = 170, Options = Buyers, Required = true, Avatar = true, Filterable = true },
                    new() { Key = "season", Title = "Season", Type = GridCellType.Select, Width = 130, Options = Seasons, Filterable = true },
                    new() { Key = "qty", Title = "Quantity", Type = GridCellType.Number, Width = 120, Min = 0 },
                    new() { Key = "delivery", Title = "Delivery", Type = GridCellType.Date, Width = 140 },
                    new() { Key = "status", Title = "Status", Type = GridCellType.Tag, Width = 140, Options = OrderStatuses, Required = true, Filterable = true },
                ],
            },
            new NestedGridLevel
            {
                Title = "Styles",
                PageSize = 4,
                Columns =
                [
                    new() { Key = "style", Title = "Style", Width = 230, Required = true, SubtitleKey = "description" },
                    new() { Key = "description", Title = "Description", Width = 220, Hidden = true },
                    new() { Key = "color", Title = "Colour", Type = GridCellType.Select, Width = 130, Options = Colors, Filterable = true },
                    new() { Key = "qty", Title = "Quantity", Type = GridCellType.Number, Width = 110, Min = 0 },
                    new() { Key = "fob", Title = "FOB price", Type = GridCellType.Currency, Width = 120, Min = 0 },
                ],
            },
            new NestedGridLevel
            {
                Title = "Materials",
                PageSize = 5,
                Sortable = false,
                Columns =
                [
                    new() { Key = "category", Title = "Category", Type = GridCellType.Tag, Width = 140, Options = Categories, Required = true, Filterable = true },
                    new() { Key = "supplier", Title = "Supplier", Type = GridCellType.Select, Width = 190, Options = Suppliers, Avatar = true, Filterable = true },
                    new() { Key = "lead", Title = "Lead time (days)", Type = GridCellType.Number, Width = 150, Min = 0 },
                    new() { Key = "approved", Title = "Approved", Type = GridCellType.Switch, Width = 110, Filterable = true },
                ],
            },
            new NestedGridLevel
            {
                Title = "Items",
                PageSize = 5,
                // Each material group's category picks its items' columns.
                ColumnSetField = "category",
                Columns = [new() { Key = "item", Title = "Item", Width = 200, Required = true }, new() { Key = "price", Title = "Unit price", Type = GridCellType.Currency, Width = 120 }],
                ColumnSets = new Dictionary<string, IReadOnlyList<GridColumn>>
                {
                    ["fabric"] =
                    [
                        new() { Key = "item", Title = "Fabric", Width = 180, Required = true },
                        new() { Key = "composition", Title = "Composition", Width = 190 },
                        new() { Key = "gsm", Title = "GSM", Type = GridCellType.Number, Width = 90, Min = 0 },
                        new() { Key = "width", Title = "Width (in)", Type = GridCellType.Number, Width = 110, Min = 0 },
                        new() { Key = "consumption", Title = "Cons. (m/pc)", Type = GridCellType.Number, Width = 120, Min = 0 },
                        new() { Key = "price", Title = "Price (m)", Type = GridCellType.Currency, Width = 110, Min = 0 },
                    ],
                    ["accessory"] =
                    [
                        new() { Key = "item", Title = "Accessory", Width = 180, Required = true },
                        new() { Key = "size", Title = "Size", Type = GridCellType.Select, Width = 110, Options = Sizes },
                        new() { Key = "finish", Title = "Finish", Type = GridCellType.Select, Width = 150, Options = Finishes },
                        new() { Key = "perGarment", Title = "Per garment", Type = GridCellType.Number, Width = 120, Min = 0 },
                        new() { Key = "price", Title = "Unit price", Type = GridCellType.Currency, Width = 110, Min = 0 },
                    ],
                    ["packing"] =
                    [
                        new() { Key = "item", Title = "Packing item", Width = 180, Required = true },
                        new() { Key = "material", Title = "Material", Type = GridCellType.Select, Width = 150, Options = PackingMaterials },
                        new() { Key = "perCarton", Title = "Per carton", Type = GridCellType.Number, Width = 110, Min = 0 },
                        new() { Key = "price", Title = "Unit price", Type = GridCellType.Currency, Width = 110, Min = 0 },
                    ],
                },
            },
        }.Take(Math.Clamp(depth, 1, 4)).ToList(),
    };
}

/// <summary>In-memory rows of the nested grid sample, kept per level and parent.</summary>
public sealed class GarmentStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, List<GridRecord>> _children = [];
    private readonly Dictionary<string, (int Level, string? Parent, GridRecord Row)> _byId = [];
    private int _next = 1;

    public GarmentStore() => Seed();

    public GridRecord? Find(string? id)
    {
        if (id is null) return null;
        lock (_gate) return _byId.TryGetValue(id, out var entry) ? Copy(entry.Row) : null;
    }

    public NestedGridPage Load(NestedGridSettings settings, NestedGridRequest request)
    {
        lock (_gate)
        {
            var parent = request.ParentId is { } p && _byId.TryGetValue(p, out var e) ? e.Row : null;
            if (request.Level > 0 && parent is null) return new NestedGridPage([], 0);
            var rows = _children.GetValueOrDefault(Key(request.Level, request.ParentId)) ?? [];
            var page = NestedGridQuery.Apply(rows, settings.ColumnsFor(request.Level, parent), request);
            return page with { Rows = page.Rows.Select(Copy).ToList() };
        }
    }

    /// <summary>Applies a level's Save: updates and adds the posted rows, deletes the removed ones and their children.</summary>
    public void Save(NestedGridChanges changes)
    {
        lock (_gate)
        {
            var key = Key(changes.Level, changes.ParentId);
            if (!_children.TryGetValue(key, out var list)) _children[key] = list = [];
            foreach (var id in changes.DeletedIds) Delete(list, id);
            foreach (var row in changes.Rows)
            {
                var existing = list.Find(r => r.Id == row.Id);
                if (existing is null)
                {
                    var added = new GridRecord($"R{_next++}", row.Values);
                    list.Add(added);
                    _byId[added.Id] = (changes.Level, changes.ParentId, added);
                }
                else foreach (var c in changes.Columns) existing[c.Key] = row[c.Key];
            }
        }
    }

    private void Delete(List<GridRecord> list, string id)
    {
        if (list.RemoveAll(r => r.Id == id) == 0 || !_byId.Remove(id, out var entry)) return;
        if (_children.Remove(Key(entry.Level + 1, id), out var children))
            foreach (var child in children.ToList()) Delete(children, child.Id);
    }

    private static string Key(int level, string? parent) => $"{level}:{parent}";

    private static GridRecord Copy(GridRecord r) => new(r.Id, r.Values);

    private GridRecord Add(int level, string? parent, string id, Dictionary<string, string?> values)
    {
        var row = new GridRecord(id, values);
        var key = Key(level, parent);
        if (!_children.TryGetValue(key, out var list)) _children[key] = list = [];
        list.Add(row);
        _byId[id] = (level, parent, row);
        return row;
    }

    private void Seed()
    {
        var rng = new Random(20260927);
        string Pick(GridOption[] options) => options[rng.Next(options.Length)].Value;
        string N(int min, int max) => rng.Next(min, max + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string M(double min, double max) => GridSchema.FromNumber(Math.Round((decimal)(min + rng.NextDouble() * (max - min)), 2))!;
        string[] styles = ["Oxford shirt", "Chino trouser", "Field jacket", "Crew tee", "Hooded sweat", "Cargo short", "Linen blazer", "Rib polo"];
        string[] fabrics = ["Cotton poplin", "Stretch twill", "Waxed canvas", "Single jersey", "Loopback fleece", "Ripstop", "Linen slub"];
        string[] compositions = ["100% cotton", "98% cotton 2% elastane", "65% polyester 35% cotton", "100% linen", "80% cotton 20% polyester"];
        string[] accessories = ["Main label", "Care label", "Zip #5", "Horn button 18L", "Snap 15mm", "Drawcord", "Hang tag"];
        string[] packing = ["Polybag", "Master carton", "Tissue wrap", "Hanger", "Size sticker"];

        for (var o = 0; o < 14; o++)
        {
            var order = Add(0, null, $"PO-{1001 + o}", new()
            {
                ["order"] = $"PO-{1001 + o}", ["buyer"] = Pick(GarmentGrid.Buyers), ["season"] = Pick(GarmentGrid.Seasons),
                ["qty"] = N(12, 90) + "00", ["delivery"] = GridSchema.FromDate(new DateOnly(2027, 1, 15).AddDays(rng.Next(0, 300))),
                ["status"] = Pick(GarmentGrid.OrderStatuses),
            });
            for (var s = 0; s < rng.Next(2, 6); s++)
            {
                var name = styles[rng.Next(styles.Length)];
                var style = Add(1, order.Id, $"S{_next++}", new()
                {
                    ["style"] = $"ST-{rng.Next(100, 999)}", ["description"] = name, ["color"] = Pick(GarmentGrid.Colors),
                    ["qty"] = N(4, 30) + "00", ["fob"] = M(6, 38),
                });
                foreach (var category in GarmentGrid.Categories.Where(_ => rng.Next(6) > 0))
                {
                    var group = Add(2, style.Id, $"G{_next++}", new()
                    {
                        ["category"] = category.Value, ["supplier"] = Pick(GarmentGrid.Suppliers), ["lead"] = N(10, 60),
                        ["approved"] = GridSchema.FromFlag(rng.Next(3) > 0),
                    });
                    for (var i = 0; i < rng.Next(2, 5); i++)
                    {
                        Dictionary<string, string?> item = category.Value switch
                        {
                            "fabric" => new() { ["item"] = fabrics[rng.Next(fabrics.Length)], ["composition"] = compositions[rng.Next(compositions.Length)], ["gsm"] = N(110, 340), ["width"] = N(54, 62), ["consumption"] = M(0.6, 2.2), ["price"] = M(2, 9) },
                            "accessory" => new() { ["item"] = accessories[rng.Next(accessories.Length)], ["size"] = Pick(GarmentGrid.Sizes), ["finish"] = Pick(GarmentGrid.Finishes), ["perGarment"] = N(1, 8), ["price"] = M(0.02, 0.9) },
                            _ => new() { ["item"] = packing[rng.Next(packing.Length)], ["material"] = Pick(GarmentGrid.PackingMaterials), ["perCarton"] = N(6, 48), ["price"] = M(0.05, 1.5) },
                        };
                        Add(3, group.Id, $"I{_next++}", item);
                    }
                }
            }
        }
    }
}
