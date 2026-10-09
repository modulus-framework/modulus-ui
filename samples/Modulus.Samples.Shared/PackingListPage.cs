using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.Samples.Shared;

/// <summary>The packing list templates page: the template list (filtered by a query string), the order chosen for the preview and
/// the output, and the filled sheet. Every sample reads the same query string, so the page's form works without script.</summary>
public sealed class PackingListPage
{
    public string Query { get; init; } = "";
    public string Buyer { get; init; } = "";
    public string Status { get; init; } = "";
    public IReadOnlyList<PackingTemplate> Templates { get; init; } = [];
    public IReadOnlyList<PackingOrder> Orders => PackingListDemo.Orders;
    public PackingOrder Order { get; init; } = PackingListDemo.Orders[0];
    public string Template { get; init; } = PackingListDemo.Templates[0].Key;
    public string Format { get; init; } = "xlsx";
    public PackingFill Fill { get; init; } = PackingListDemo.Fill(PackingListDemo.Orders[0]);
    public IReadOnlyList<SpreadsheetSheet> TemplateSheet => PackingListDemo.TemplateSheet(Template, Columns);

    /// <summary>The column headings of the template, in the order the user chose (the template's own order by default).</summary>
    public IReadOnlyList<string> Columns { get; init; } = [];

    /// <summary>The page for the query: the templates that match it, and the order and format asked for (the first order and Excel by default).</summary>
    public static PackingListPage Load(string? query, string? buyer, string? status, string? po, string? template, string? format, IReadOnlyList<string>? columns = null)
    {
        var templates = PackingListDemo.Templates
            .Where(t => string.IsNullOrWhiteSpace(query) || t.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(t => string.IsNullOrWhiteSpace(buyer) || t.Buyer == buyer)
            .Where(t => string.IsNullOrWhiteSpace(status) || t.Status == status)
            .ToList();
        var order = PackingListDemo.Orders.FirstOrDefault(o => o.Number == po) ?? PackingListDemo.Orders[0];
        var key = PackingListDemo.Templates.Any(t => t.Key == template) ? template! : PackingListDemo.Templates[0].Key;
        return new PackingListPage
        {
            Query = query ?? "",
            Buyer = buyer ?? "",
            Status = status ?? "",
            Templates = templates,
            Order = order,
            Template = key,
            Format = format == "pdf" ? "pdf" : "xlsx",
            Columns = Ordered(key, columns),
            Fill = PackingListDemo.Fill(order, key, columns),
        };
    }

    // The template's headings in the chosen order: the named ones first (unknown names dropped), the rest after them.
    private static IReadOnlyList<string> Ordered(string key, IReadOnlyList<string>? chosen)
    {
        var headings = PackingListDemo.Headings(key);
        var named = (chosen ?? []).Where(headings.Contains).Distinct().ToList();
        return [.. named, .. headings.Where(h => !named.Contains(h))];
    }
}
