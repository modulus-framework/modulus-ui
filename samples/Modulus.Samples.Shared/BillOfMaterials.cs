using System.Globalization;
using Modulus.Ui.Abstractions;

namespace Modulus.Samples.Shared;

/// <summary>
/// The tree grid sample (/tables/tree): a bill of materials, one self-referencing list of parts. A part's children are
/// the parts whose parent it is; quantities are per parent. A part's extended cost is quantity × unit cost, or, for an
/// assembly, quantity × the extended cost of its parts, so the top level's total is the cost of one bill. Each level
/// totals its own rows (<see cref="NestedGridTree.Rollup"/>), so a sub-assembly's parts add up under it.
/// </summary>
public static class BillOfMaterials
{
    public static readonly GridOption[] Kinds = [new("assembly", "Assembly", "blue"), new("component", "Component", "purple"), new("material", "Material", "green")];

    public static NestedGridSettings Settings { get; } = new()
    {
        Levels =
        [
            new NestedGridLevel
            {
                Title = "Parts",
                PageSize = 5,
                Columns =
                [
                    new() { Key = "code", Title = "Code", Width = 130, Fixed = GridFixed.Left, Required = true },
                    new() { Key = "name", Title = "Part", Width = 240, Required = true },
                    new() { Key = "kind", Title = "Kind", Type = GridCellType.Select, Width = 150, Options = Kinds, Filterable = true },
                    new() { Key = "qty", Title = "Qty per parent", Type = GridCellType.Number, Width = 150, Min = 0 },
                    new() { Key = "cost", Title = "Unit cost", Type = GridCellType.Currency, Width = 130, Min = 0 },
                    new() { Key = "ext", Title = "Extended cost", Type = GridCellType.Currency, Width = 150, Editable = false },
                    new() { Key = "children", Title = "Children", Type = GridCellType.Number, Width = 110, Editable = false },
                ],
            },
        ],
        Tree = new NestedGridTree { MaxDepth = 4, ChildCountField = "children", Rollup = ["ext"] },
    };
}

/// <summary>
/// In-memory parts of the tree grid sample, kept per parent (the root's key is empty). Extended cost and child counts
/// are worked out on each read, so they follow the quantities and costs as edited.
/// </summary>
public sealed class BillOfMaterialsStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, List<GridRecord>> _children = [];
    private readonly Dictionary<string, string> _parentOf = [];
    private int _next = 1;

    public BillOfMaterialsStore() => Seed();

    public GridRecord? Find(string? id)
    {
        if (id is null) return null;
        lock (_gate) return _parentOf.ContainsKey(id) ? Copy(Row(id)!) : null;
    }

    public NestedGridPage Load(NestedGridSettings settings, NestedGridRequest request)
    {
        lock (_gate)
        {
            var rows = _children.GetValueOrDefault(Key(request.ParentId))?.Select(Decorate).ToList() ?? [];
            return NestedGridQuery.Apply(rows, settings.ColumnsFor(0, null), request, settings.Tree?.Rollup);
        }
    }

    /// <summary>Applies a level's Save: updates and adds the posted parts under the parent, deletes the removed ones with their parts.</summary>
    public void Save(NestedGridChanges changes)
    {
        lock (_gate)
        {
            var key = Key(changes.ParentId);
            if (!_children.TryGetValue(key, out var list)) _children[key] = list = [];
            var editable = BillOfMaterials.Settings.Levels[0].Columns.Where(c => c.Editable).Select(c => c.Key).ToList();
            foreach (var id in changes.DeletedIds) Delete(list, id);
            var order = new List<string>();
            var added = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in changes.Rows)
            {
                var existing = list.Find(r => r.Id == row.Id);
                if (existing is null)
                {
                    var created = new GridRecord($"P{_next++}", editable.ToDictionary(k => k, k => row[k], StringComparer.Ordinal));
                    list.Add(created);
                    _parentOf[created.Id] = key;
                    added.Add(created.Id);
                    order.Add(created.Id);
                }
                else
                {
                    foreach (var k in editable) existing[k] = row[k];
                    order.Add(existing.Id);
                }
            }
            if (changes.Ordered) NestedGridQuery.StoreOrder(list, order, added);
        }
    }

    private void Delete(List<GridRecord> list, string id)
    {
        if (list.RemoveAll(r => r.Id == id) == 0 || !_parentOf.Remove(id)) return;
        if (_children.Remove(id, out var children))
            foreach (var child in children.ToList()) Delete(children, child.Id);
    }

    // The stored part, or null for an id the store does not hold.
    private GridRecord? Row(string id) => _children.Values.SelectMany(l => l).FirstOrDefault(r => r.Id == id);

    // The part as the grid shows it: its extended cost and how many parts it has.
    private GridRecord Decorate(GridRecord row)
    {
        var values = new Dictionary<string, string?>(row.Values, StringComparer.Ordinal);
        var qty = GridSchema.Number(row["qty"]) ?? 0;
        var cost = GridSchema.Number(row["cost"]) ?? 0;
        values["ext"] = GridSchema.FromNumber(Math.Round(Extended(row.Id, qty, cost), 2));
        values["children"] = (_children.GetValueOrDefault(row.Id)?.Count ?? 0).ToString(CultureInfo.InvariantCulture);
        return new GridRecord(row.Id, values);
    }

    // A part with parts is its quantity times what its parts cost; a leaf is its quantity times its unit cost.
    private decimal Extended(string id, decimal qty, decimal cost)
    {
        if (!_children.TryGetValue(id, out var parts) || parts.Count == 0) return qty * cost;
        return qty * parts.Sum(p => Extended(p.Id, GridSchema.Number(p["qty"]) ?? 0, GridSchema.Number(p["cost"]) ?? 0));
    }

    private static string Key(string? parent) => parent ?? "";

    private static GridRecord Copy(GridRecord r) => new(r.Id, r.Values);

    private void Add(string? parent, string id, Dictionary<string, string?> values)
    {
        var key = Key(parent);
        if (!_children.TryGetValue(key, out var list)) _children[key] = list = [];
        list.Add(new GridRecord(id, values));
        _parentOf[id] = key;
    }

    // A jacket and a tote bag: a sub-assembly with its own parts, and parts with no children.
    private void Seed()
    {
        Add(null, "P-JK", Part("JK-100", "Jacket", "assembly", "1", "0"));
        Add("P-JK", "P-BODY", Part("SUB-10", "Body", "assembly", "1", "0"));
        Add("P-BODY", "P-FAB1", Part("FAB-201", "Shell fabric", "material", "1.4", "8.50"));
        Add("P-BODY", "P-FAB2", Part("FAB-305", "Lining", "material", "0.9", "3.20"));
        Add("P-JK", "P-SLV", Part("SUB-20", "Sleeves", "assembly", "2", "0"));
        Add("P-SLV", "P-FAB3", Part("FAB-202", "Sleeve fabric", "material", "0.6", "8.50"));
        Add("P-JK", "P-ZIP", Part("ACC-410", "Zip", "component", "1", "0.75"));
        Add("P-JK", "P-BTN", Part("ACC-420", "Buttons", "component", "6", "0.12"));
        Add("P-JK", "P-LBL", Part("PKG-501", "Care label", "component", "1", "0.05"));
        Add(null, "P-TOTE", Part("TB-200", "Tote bag", "assembly", "1", "0"));
        Add("P-TOTE", "P-CAN", Part("FAB-410", "Canvas", "material", "0.8", "6.40"));
        Add("P-TOTE", "P-HDL", Part("ACC-430", "Handles", "component", "2", "1.10"));
    }

    private static Dictionary<string, string?> Part(string code, string name, string kind, string qty, string cost) =>
        new(StringComparer.Ordinal) { ["code"] = code, ["name"] = name, ["kind"] = kind, ["qty"] = qty, ["cost"] = cost };
}
