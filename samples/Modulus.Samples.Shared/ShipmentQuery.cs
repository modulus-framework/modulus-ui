using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Data;
using Modulus.Ui.Abstractions.Query;

namespace Modulus.Samples.Shared;

/// <summary>
/// The visual filter page (Tables → Query builder) over the shipments: the builder's fields, the filter as the page
/// applies it (through <see cref="TableQuery{T}.Filters"/> and <see cref="ProTableRequest.Where"/>), and the rows it leaves.
/// The same in all three samples.
/// </summary>
public sealed record QueryPage(IReadOnlyList<QueryField> Schema, FilterGroup Filter, string Sentence, int Matches, decimal Total, IReadOnlyList<Shipment> Rows)
{
    public const string Path = "/tables/query";

    /// <summary>The rows the page lists at most.</summary>
    public const int ShowRows = 25;

    public static readonly GridOption[] Statuses = Enum.GetValues<ShipmentStatus>().Select(s => new GridOption(s.ToString(), s.ToString())).ToArray();

    /// <summary>The fields a shipment filter may ask about.</summary>
    public static readonly QueryBuilder<Shipment> Builder = QueryBuilder<Shipment>.Create()
        .Text("customer", "Customer", s => s.Customer)
        .Text("city", "City", s => s.City)
        .Choice("status", "Status", s => s.Status.ToString(), Statuses)
        .Number("amount", "Amount", s => s.Amount)
        .Date("date", "Date", s => s.Date);

    /// <summary>The table's query: the rows, filtered by the request's <see cref="ProTableRequest.Where"/>.</summary>
    public static readonly TableQuery<Shipment> Table = TableQuery<Shipment>.By(s => s.Id).Filters(Builder);

    /// <summary>The page for a filter: its rows, how many match, and their total amount.</summary>
    public static QueryPage Load(FilterGroup filter)
    {
        var filtered = Table.Filter(Shipments.Query, new ProTableRequest { Where = FilterJson.ToText(filter) });
        var matches = filtered.Count();
        var total = filtered.Sum(s => s.Amount);
        var rows = filtered.OrderBy(s => s.Id).Take(ShowRows).ToList();
        return new QueryPage(Builder.Schema, filter, Builder.Describe(filter), matches, total, rows);
    }
}
