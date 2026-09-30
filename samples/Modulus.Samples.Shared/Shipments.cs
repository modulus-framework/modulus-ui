using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Data;

namespace Modulus.Samples.Shared;

public enum ShipmentStatus
{
    Pending,
    Packed,
    Shipped,
    Delivered,
    Returned,
}

public sealed record Shipment(int Id, string Reference, string Customer, string City, ShipmentStatus Status, decimal Amount, DateOnly Date);

/// <summary>The large-data table's filters (query keys status, city, keyword); null or empty means any.</summary>
public sealed record ShipmentFilter(ShipmentStatus? Status = null, string? City = null, string? Keyword = null)
{
    public bool IsEmpty => Status is null && string.IsNullOrWhiteSpace(City) && string.IsNullOrWhiteSpace(Keyword);
}

/// <summary>
/// 100,000 generated shipments for the large-data table (Tables → Large data). They are queried through
/// <see cref="TableQuery{T}"/> as an <see cref="IQueryable{T}"/>, exactly as an Entity Framework Core <c>DbSet</c> would be:
/// swap <see cref="Query"/> for <c>db.Shipments</c> and the same code pages, sorts and searches in SQL.
/// </summary>
public static class Shipments
{
    public const int Count = 100_000;

    private static readonly Lazy<Shipment[]> All = new(Generate);

    /// <summary>Every shipment, as a queryable source.</summary>
    public static IQueryable<Shipment> Query => All.Value.AsQueryable();

    /// <summary>The table's sortable and searchable columns; any other field in the query string is ignored.</summary>
    public static readonly TableQuery<Shipment> Table = TableQuery<Shipment>.By(s => s.Id)
        .Sort("reference", s => s.Reference)
        .Sort("customer", s => s.Customer)
        .Sort("city", s => s.City)
        .Sort("amount", s => s.Amount)
        .Sort("date", s => s.Date)
        .SearchText("reference", s => s.Reference, ignoreCase: true)
        .SearchText("customer", s => s.Customer, ignoreCase: true)
        .SearchText("city", s => s.City, ignoreCase: true)
        .DefaultSort("date", descending: true);

    /// <summary>The cities shipments go to (the City filter's options).</summary>
    public static readonly IReadOnlyList<string> Cities = ["Amsterdam", "Athens", "Berlin", "Brussels", "Copenhagen", "Dublin", "Helsinki", "Lisbon", "London", "Madrid", "Oslo", "Paris", "Prague", "Rome", "Stockholm", "Vienna", "Warsaw", "Zurich"];

    /// <summary>Shipments with the given status (all when null), before the table's searches, sort and paging.</summary>
    public static IQueryable<Shipment> Where(ShipmentStatus? status) => Where(new ShipmentFilter(status));

    /// <summary>
    /// Shipments matching the filter, before the table's searches, sort and paging. The keyword matches the reference or
    /// the customer, ignoring case; every condition stays in the query (translatable by EF Core).
    /// </summary>
    public static IQueryable<Shipment> Where(ShipmentFilter filter)
    {
        var query = Query;
        if (filter.Status is { } status) query = query.Where(x => x.Status == status);
        if (filter.City?.Trim() is { Length: > 0 } city) query = query.Where(x => x.City == city);
        if (filter.Keyword?.Trim() is { Length: > 0 } keyword)
        {
            var k = (keyword.Length > TableQuery<Shipment>.MaxSearchLength ? keyword[..TableQuery<Shipment>.MaxSearchLength] : keyword).ToLowerInvariant();
            query = query.Where(x => x.Reference.ToLower().Contains(k) || x.Customer.ToLower().Contains(k));
        }
        return query;
    }

    public static TablePage<Shipment> Page(ShipmentStatus? status, ProTableRequest request, CountMode count) =>
        Table.Page(Where(status), request, count);

    public static TablePage<Shipment> Page(ShipmentFilter filter, ProTableRequest request, CountMode count) =>
        Table.Page(Where(filter), request, count);

    private static Shipment[] Generate()
    {
        string[] first = ["Acme", "Globex", "Initech", "Umbrella", "Stark", "Wayne", "Wonka", "Hooli", "Vandelay", "Soylent", "Tyrell", "Cyberdyne", "Aperture", "Gringotts", "Monarch", "Oscorp"];
        string[] second = ["Trading", "Industries", "Logistics", "Foods", "Retail", "Labs", "Supply", "Group", "Partners", "Systems"];
        string[] cities = ["Berlin", "London", "Paris", "Madrid", "Rome", "Vienna", "Prague", "Warsaw", "Lisbon", "Dublin", "Oslo", "Helsinki", "Athens", "Zurich", "Amsterdam", "Brussels", "Copenhagen", "Stockholm"];
        var statuses = Enum.GetValues<ShipmentStatus>();
        var random = new Random(20260930);
        var start = new DateOnly(2024, 1, 1);
        var rows = new Shipment[Count];
        for (var i = 0; i < Count; i++)
        {
            var id = i + 1;
            var customer = $"{first[random.Next(first.Length)]} {second[random.Next(second.Length)]}";
            var date = start.AddDays(random.Next(0, 1000));
            // Older shipments are mostly delivered; recent ones still moving.
            var status = date < start.AddDays(900) && random.Next(10) < 8 ? ShipmentStatus.Delivered : statuses[random.Next(statuses.Length)];
            rows[i] = new Shipment(id, $"SHP-{id:D6}", customer, cities[random.Next(cities.Length)], status,
                Math.Round((decimal)(random.NextDouble() * 4900 + 100), 2), date);
        }
        return rows;
    }
}
