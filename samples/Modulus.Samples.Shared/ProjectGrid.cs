using Modulus.Ui.Abstractions;

namespace Modulus.Samples.Shared;

/// <summary>
/// The editable grid demo (Tables → Editable grid): a project portfolio whose columns are data. Users can edit
/// cells, add and delete rows, add columns, pin, hide and reorder them, and save. Saved state is kept in memory
/// for the lifetime of the app. Register as a singleton.
/// </summary>
public sealed class ProjectGridStore
{
    private readonly Lock _gate = new();
    private List<GridColumn> _columns = SeedColumns();
    private List<GridRecord> _rows = SeedRows();

    /// <summary>A copy of the saved schema and rows, safe to edit.</summary>
    public (List<GridColumn> Columns, List<GridRecord> Rows) Load()
    {
        lock (_gate) return (Copy(_columns), Copy(_rows));
    }

    public void Save(IEnumerable<GridColumn> columns, IEnumerable<GridRecord> rows)
    {
        lock (_gate)
        {
            _columns = Copy(columns);
            _rows = Copy(rows);
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _columns = SeedColumns();
            _rows = SeedRows();
        }
    }

    private static List<GridColumn> Copy(IEnumerable<GridColumn> columns) => columns.Select(c => c.Clone()).ToList();

    private static List<GridRecord> Copy(IEnumerable<GridRecord> rows) => rows.Select(r => new GridRecord(r.Id, r.Values)).ToList();

    public static readonly GridOption[] Owners =
    [
        new("olivia", "Olivia Bennett"), new("liam", "Liam Okafor"), new("amelia", "Amelia Lindqvist"),
        new("noah", "Noah Tanaka"), new("sofia", "Sofia Reyes"), new("mateo", "Mateo Hartmann"),
    ];

    public static readonly GridOption[] Statuses =
    [
        new("planned", "Planned", "default"), new("active", "Active", "processing"), new("hold", "On hold", "warning"),
        new("risk", "At risk", "error"), new("done", "Done", "success"),
    ];

    public static readonly GridOption[] Priorities = [new("low", "Low"), new("medium", "Medium"), new("high", "High"), new("urgent", "Urgent")];

    public static readonly GridOption[] Teams =
    [
        new("web", "Web", "blue"), new("mobile", "Mobile", "purple"), new("data", "Data", "cyan"),
        new("design", "Design", "magenta"), new("infra", "Infrastructure", "gold"),
    ];

    public static List<GridColumn> SeedColumns() =>
    [
        new() { Key = "project", Title = "Project", Type = GridCellType.Text, Width = 220, Fixed = GridFixed.Left, Required = true },
        new() { Key = "owner", Title = "Owner", Type = GridCellType.Select, Width = 180, Options = Owners, Required = true },
        new() { Key = "status", Title = "Status", Type = GridCellType.Tag, Width = 140, Options = Statuses, Required = true },
        new() { Key = "priority", Title = "Priority", Type = GridCellType.Select, Width = 130, Options = Priorities },
        new() { Key = "teams", Title = "Teams", Type = GridCellType.MultiSelect, Width = 240, Options = Teams },
        new() { Key = "budget", Title = "Budget", Type = GridCellType.Currency, Width = 140, Min = 0, Required = true },
        new() { Key = "spent", Title = "Spent", Type = GridCellType.Currency, Width = 140, Min = 0 },
        new() { Key = "progress", Title = "Progress", Type = GridCellType.Progress, Width = 180, Min = 0, Max = 100 },
        new() { Key = "start", Title = "Start", Type = GridCellType.Date, Width = 140 },
        new() { Key = "due", Title = "Due", Type = GridCellType.Date, Width = 140 },
        new() { Key = "health", Title = "Health", Type = GridCellType.Rating, Width = 160, Min = 0, Max = 5 },
        new() { Key = "billable", Title = "Billable", Type = GridCellType.Switch, Width = 110 },
        new() { Key = "notes", Title = "Notes", Type = GridCellType.Text, Width = 260 },
    ];

    public static List<GridRecord> SeedRows()
    {
        (string Name, string Notes)[] projects =
        [
            ("Customer portal redesign", "New navigation and self-service billing."),
            ("Mobile app 3.0", "Offline mode and push notifications."),
            ("Data warehouse migration", "Move reporting to the new warehouse."),
            ("Design system v2", "Tokens, dark mode and component audit."),
            ("Kubernetes upgrade", "Upgrade clusters to the current release."),
            ("Checkout A/B test", "Single-page checkout experiment."),
            ("Partner API", "Public REST API with OAuth for partners."),
            ("Support chatbot", "Deflect common questions from the help desk."),
            ("Analytics dashboards", "Self-service dashboards for sales."),
            ("SOC 2 readiness", "Controls, evidence collection and audit prep."),
            ("Search relevance", "Tune ranking and add synonyms."),
            ("Onboarding emails", "Lifecycle emails for the first 30 days."),
        ];
        var rng = new Random(7);
        var start = new DateOnly(2026, 1, 12);
        return projects.Select((p, i) =>
        {
            var budget = 20_000 + rng.Next(0, 32) * 5_000;
            var progress = i % 5 == 4 ? 100 : rng.Next(5, 95);
            var status = progress == 100 ? "done" : Statuses[rng.Next(0, 4)].Value;
            var begin = start.AddDays(i * 11 + rng.Next(0, 9));
            var teams = Teams.Where((_, t) => (i + t) % 3 == 0 || t == i % Teams.Length).Select(t => t.Value).Distinct();
            return new GridRecord($"p{i + 1}", new Dictionary<string, string?>
            {
                ["project"] = p.Name,
                ["owner"] = Owners[i % Owners.Length].Value,
                ["status"] = status,
                ["priority"] = Priorities[rng.Next(0, Priorities.Length)].Value,
                ["teams"] = GridSchema.FromMany(teams),
                ["budget"] = GridSchema.FromNumber(budget),
                ["spent"] = GridSchema.FromNumber(Math.Round(budget * progress / 100m * (0.8m + (decimal)rng.NextDouble() * 0.4m) / 100m) * 100m),
                ["progress"] = GridSchema.FromNumber(progress),
                ["start"] = GridSchema.FromDate(begin),
                ["due"] = GridSchema.FromDate(begin.AddDays(60 + rng.Next(0, 90))),
                ["health"] = GridSchema.FromNumber(status == "risk" ? 2 : rng.Next(3, 6)),
                ["billable"] = GridSchema.FromFlag(i % 3 != 1),
                ["notes"] = p.Notes,
            });
        }).ToList();
    }

    /// <summary>Budget used, in percent, for the expanded row and the summary.</summary>
    public static decimal? BurnRate(GridRecord row) =>
        GridSchema.Number(row["budget"]) is { } budget && budget > 0 && GridSchema.Number(row["spent"]) is { } spent
            ? Math.Round(spent / budget * 100, 0)
            : null;

    /// <summary>Totals shown under the grid.</summary>
    public static (int Count, decimal Budget, decimal Spent) Totals(IEnumerable<GridRecord> rows)
    {
        var list = rows.ToList();
        return (list.Count, list.Sum(r => GridSchema.Number(r["budget"]) ?? 0), list.Sum(r => GridSchema.Number(r["spent"]) ?? 0));
    }
}
