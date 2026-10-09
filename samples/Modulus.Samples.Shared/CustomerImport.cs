using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Import;
using Modulus.Ui.Abstractions.Query;

namespace Modulus.Samples.Shared;

/// <summary>
/// The customer import sample: the fields a CSV file maps to, an example file, and the store the import adds the checked
/// rows to. The three apps run the same wizard over it (<c>/tables/import</c>).
/// </summary>
public static class CustomerImport
{
    public static readonly IReadOnlyList<GridOption> Countries =
    [
        new("NO", "Norway"),
        new("SE", "Sweden"),
        new("DK", "Denmark"),
        new("FI", "Finland"),
        new("IS", "Iceland"),
    ];

    public static readonly IReadOnlyList<ImportField> Fields =
    [
        new("name", "Name", QueryFieldKind.Text, Required: true, Aliases: ["Customer", "Full name"]),
        new("email", "Email", QueryFieldKind.Text, Aliases: ["E-mail"]),
        new("country", "Country", QueryFieldKind.Choice, Options: Countries),
        new("credit", "Credit limit", QueryFieldKind.Number, Aliases: ["Limit"]),
        new("joined", "Joined", QueryFieldKind.Date, Aliases: ["Start date"]),
    ];

    /// <summary>An example file: save it as a .csv file and upload it. Its last row has a problem on purpose.</summary>
    public const string Example = """
        Customer,E-mail,Country,Credit limit,Start date
        Ada Lovelace,ada@example.com,Norway,25000,2025-03-14
        Lars Berg,lars@example.com,Sweden,"12,500.50",2024-11-02
        Mette Holm,mette@example.com,Denmark,8000,2025-01-20
        Sami Virtanen,,Finland,15000,2023-06-30
        Ingrid Dahl,ingrid@example.com,Iceland,n/a,2025-02-29
        """;

    /// <summary>The message the import shows once it has run.</summary>
    public static string Saved(int imported, int total) =>
        $"Imported {imported:N0} customers. {total:N0} customers have been imported in this session.";
}

/// <summary>Holds the customers imported in this session (the sample's "server callback").</summary>
public sealed class CustomerImportStore
{
    private readonly List<ImportRow> _rows = [];
    private readonly Lock _lock = new();

    /// <summary>Adds checked rows and returns how many the store holds now.</summary>
    public int Add(IReadOnlyList<ImportRow> rows)
    {
        lock (_lock)
        {
            _rows.AddRange(rows);
            return _rows.Count;
        }
    }

    public int Count
    {
        get { lock (_lock) return _rows.Count; }
    }
}
