using System.Globalization;
using Modulus.Ui.Abstractions;

namespace Modulus.Samples.Shared;

/// <summary>The row type the data grid sample (/tables/data-grid) discovers its columns from, twice over.</summary>
public enum PurchaseStatus
{
    Draft,
    Sent,
    Confirmed,
    Received,
}

/// <summary>A complex property of <see cref="PurchaseOrder"/>: the grid flattens it one level ("Customer name").</summary>
public sealed class PurchaseCustomer
{
    public string Name { get; set; } = "";

    public string City { get; set; } = "";

    public string? Country { get; set; }
}

/// <summary>An order row: every property except <c>Id</c> becomes a column, searched and sorted by the database.</summary>
public sealed class PurchaseOrder
{
    public int Id { get; set; }

    public string Number { get; set; } = "";

    public PurchaseCustomer? Customer { get; set; }

    public DateOnly Ordered { get; set; }

    /// <summary>Nullable on purpose: the date column's search and sort stay null-safe.</summary>
    public DateOnly? Expected { get; set; }

    public decimal Total { get; set; }

    public PurchaseStatus Status { get; set; }
}

/// <summary>A line row: zero-config columns for the schema engine's child level (DataGridAuto.SchemaColumns).</summary>
public sealed class PurchaseLine
{
    public int Id { get; set; }

    public string Sku { get; set; } = "";

    public string Description { get; set; } = "";

    public int Quantity { get; set; }

    public decimal Price { get; set; }
}

/// <summary>The nested levels of the sample's schema-driven grid: the columns are the types' own, discovered.</summary>
public static class PurchaseGrid
{
    /// <summary>Orders with their lines one level down, every level searched, sorted and editable in place.</summary>
    public static NestedGridSettings Settings { get; } = new()
    {
        Levels =
        [
            new NestedGridLevel
            {
                Title = "Orders",
                PageSize = 5,
                Columns = AutoColumns<PurchaseOrder>(),
            },
            new NestedGridLevel
            {
                Title = "Lines",
                PageSize = 5,
                Columns = AutoColumns<PurchaseLine>(),
            },
        ],
    };

    /// <summary>A read-only lines level, for an expanded typed row (no editing, so no Save).</summary>
    public static NestedGridSettings LinesSettings { get; } = new()
    {
        Levels = [new NestedGridLevel { Title = "Lines", PageSize = 5, Editable = false, AllowAdd = false, AllowDelete = false, Columns = AutoColumns<PurchaseLine>() }],
    };

    /// <summary>The type's discovered columns as grid columns, the first one pinned like an id column usually is.</summary>
    public static List<GridColumn> AutoColumns<T>()
    {
        var columns = DataGridAuto.SchemaColumns<T>();
        if (columns.Count > 0) columns[0].Fixed = GridFixed.Left;
        return columns;
    }
}

/// <summary>In-memory orders and lines for the data grid sample: one store behind both engines.</summary>
public sealed class PurchaseStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, List<PurchaseLine>> _lines = [];
    private int _nextOrder = 1, _nextLine = 1;

    public PurchaseStore() => Seed();

    public List<PurchaseOrder> Orders { get; } = [];

    public IReadOnlyList<PurchaseLine> Lines(int orderId)
    {
        lock (_gate) return _lines.GetValueOrDefault(orderId) ?? [];
    }

    /// <summary>The schema grid's levels: orders at level 0, an order's lines (by ParentId) at level 1.</summary>
    public NestedGridPage Load(NestedGridSettings settings, NestedGridRequest request)
    {
        lock (_gate)
        {
            if (request.Level == 0)
                return NestedGridQuery.Apply(DataGridAuto.Records(Orders), settings.ColumnsFor(0, null), request);
            return ParseId(request.ParentId) is { } orderId && _lines.TryGetValue(orderId, out var lines)
                ? NestedGridQuery.Apply(DataGridAuto.Records(lines), settings.ColumnsFor(1, null), request)
                : new NestedGridPage([], 0);
        }
    }

    /// <summary>A schema grid's Save, written back into the typed rows (the two engines share one store).</summary>
    public void Save(NestedGridChanges changes)
    {
        lock (_gate)
        {
            if (changes.Level == 0) SaveOrders(changes);
            else if (ParseId(changes.ParentId) is { } orderId) SaveLines(orderId, changes);
        }
    }

    /// <summary>The typed grid's row detail: an order's lines as grid records, read-only.</summary>
    public NestedGridPage LinesPage(int orderId, NestedGridRequest request) =>
        NestedGridQuery.Apply(DataGridAuto.Records(Lines(orderId)), PurchaseGrid.LinesSettings.ColumnsFor(0, null), request);

    public PurchaseOrder AddOrder()
    {
        lock (_gate)
        {
            var order = new PurchaseOrder { Id = _nextOrder++, Number = $"PO-{1100 + Orders.Count}", Ordered = DateOnly.FromDateTime(DateTime.Today), Status = PurchaseStatus.Draft };
            Orders.Add(order);
            return order;
        }
    }

    public void Remove(IEnumerable<PurchaseOrder> orders)
    {
        lock (_gate)
        {
            foreach (var order in orders)
            {
                Orders.Remove(order);
                _lines.Remove(order.Id);
            }
        }
    }

    private void SaveOrders(NestedGridChanges changes)
    {
        foreach (var id in changes.DeletedIds)
            if (ParseId(id) is { } deleted) Orders.RemoveAll(o => o.Id == deleted);
        foreach (var row in changes.Rows)
        {
            var order = Orders.FirstOrDefault(o => IdOf(o) == row.Id) ?? AddOrderInsideLock();
            order.Number = row["number"] ?? "";
            if (order.Customer is null) order.Customer = new PurchaseCustomer();
            order.Customer.Name = row["customerName"] ?? "";
            order.Customer.City = row["customerCity"] ?? "";
            order.Customer.Country = row["customerCountry"];
            if (TryDate(row["ordered"], out var ordered)) order.Ordered = ordered;
            order.Expected = TryDate(row["expected"], out var expected) ? expected : null;
            if (TryNumber(row["total"], out var total)) order.Total = total;
            if (Enum.TryParse(row["status"], ignoreCase: true, out PurchaseStatus status)) order.Status = status;
        }
        if (changes.Ordered) GridSchema.Reorder(Orders, changes.Rows.Select(r => r.Id), IdOf);
    }

    private void SaveLines(int orderId, NestedGridChanges changes)
    {
        if (!_lines.TryGetValue(orderId, out var lines)) _lines[orderId] = lines = [];
        foreach (var id in changes.DeletedIds)
            if (ParseId(id) is { } deleted) lines.RemoveAll(l => l.Id == deleted);
        foreach (var row in changes.Rows)
        {
            var line = lines.FirstOrDefault(l => IdOf(l) == row.Id);
            if (line is null)
            {
                line = new PurchaseLine { Id = _nextLine++ };
                lines.Add(line);
            }
            line.Sku = row["sku"] ?? "";
            line.Description = row["description"] ?? "";
            if (TryNumber(row["quantity"], out var quantity)) line.Quantity = (int)quantity;
            if (TryNumber(row["price"], out var price)) line.Price = price;
        }
        if (changes.Ordered) GridSchema.Reorder(lines, changes.Rows.Select(r => r.Id), IdOf);
    }

    private PurchaseOrder AddOrderInsideLock()
    {
        var order = new PurchaseOrder { Id = _nextOrder++ };
        Orders.Add(order);
        return order;
    }

    private static string IdOf(PurchaseOrder order) => order.Id.ToString(CultureInfo.InvariantCulture);

    private static string IdOf(PurchaseLine line) => line.Id.ToString(CultureInfo.InvariantCulture);

    private static int? ParseId(string? id) => int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static bool TryDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text, GridSchema.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryNumber(string? text, out decimal number) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out number);

    private void Seed()
    {
        var rng = new Random(20261001);
        string[] names = ["Northwind Traders", "Contoso Fabrics", "Fabrikam Mills", "Tailspin Textiles", "Adatum Supply", "Proseware Cotton", "Lucerne Looms"];
        string[] cities = ["Bologna", "Como", "Prato", "Bergamo", "Vicenza"];
        string[] countries = ["IT", "IT", "IT", "CH", "AT"];
        string[] skus = ["CTN-POP", "CTN-TWL", "CTN-JSY", "CTN-FLC", "CTN-LIN", "CTN-RIP"];
        string[] descriptions = ["Cotton poplin 120g", "Stretch twill 260g", "Single jersey 180g", "Loopback fleece 340g", "Linen slub 140g", "Ripstop 70d"];

        for (var o = 0; o < 16; o++)
        {
            var order = new PurchaseOrder
            {
                Id = _nextOrder++,
                Number = $"PO-{1001 + o}",
                Customer = new PurchaseCustomer { Name = names[rng.Next(names.Length)], City = cities[rng.Next(cities.Length)], Country = countries[rng.Next(countries.Length)] },
                Ordered = new DateOnly(2026, 1, 5).AddDays(rng.Next(0, 270)),
                Expected = rng.Next(5) == 0 ? null : new DateOnly(2026, 2, 1).AddDays(rng.Next(0, 320)),
                Status = (PurchaseStatus)rng.Next(4),
            };
            Orders.Add(order);
            var lines = new List<PurchaseLine>();
            for (var l = 0; l < rng.Next(2, 6); l++)
            {
                var i = rng.Next(skus.Length);
                lines.Add(new PurchaseLine { Id = _nextLine++, Sku = skus[i], Description = descriptions[i], Quantity = rng.Next(10, 400), Price = Math.Round((decimal)(1.5 + rng.NextDouble() * 12), 2) });
            }
            order.Total = lines.Sum(l => l.Quantity * l.Price);
            _lines[order.Id] = lines;
        }
    }
}
