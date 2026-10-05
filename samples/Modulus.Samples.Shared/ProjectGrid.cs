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

    /// <summary>A copy of the saved schema and rows, safe to edit, with each row's state (<see cref="Mark"/>).</summary>
    public (List<GridColumn> Columns, List<GridRecord> Rows) Load()
    {
        lock (_gate) return (Copy(_columns), Mark(Copy(_rows)));
    }

    /// <summary>The saved rows with their states: what a post is held to (<see cref="GridSchema.Protect"/>).</summary>
    public List<GridRecord> StoredRows()
    {
        lock (_gate) return Mark(Copy(_rows));
    }

    /// <summary>
    /// Row states, as an ERP derives them from the saved record: a done project is closed (locked), and the
    /// partner API project is read-only here (its team edits it elsewhere). A row saved as done locks on the next load.
    /// </summary>
    public static List<GridRecord> Mark(List<GridRecord> rows)
    {
        foreach (var row in rows)
        {
            (row.State, row.StateReason) = row["status"] == "done" ? (GridRowState.Locked, "Closed: done projects can't change")
                : row["project"] == "Partner API" ? (GridRowState.ReadOnly, "Managed by the platform team")
                : (GridRowState.Editable, null);
        }
        return rows;
    }

    /// <summary>The check across cells, run on Save: a row error.</summary>
    public static string? CheckRow(GridRecord row) =>
        GridSchema.Date(row["start"]) is { } start && GridSchema.Date(row["due"]) is { } due && due < start
            ? "Due is before the start."
            : null;

    public void Save(IEnumerable<GridColumn> columns, IEnumerable<GridRecord> rows)
    {
        lock (_gate)
        {
            // The approval changes only through Approve and Reject (ApplyBatch), not with an edited post.
            var approvals = _rows.ToDictionary(r => r.Id, r => r["approval"]);
            _columns = Copy(columns);
            _rows = Copy(rows);
            foreach (var row in _rows) row["approval"] = approvals.GetValueOrDefault(row.Id) ?? "pending";
        }
    }

    /// <summary>Whether a posted grid differs from the saved one (MVC: "Unsaved changes" after a batch post).</summary>
    public bool Differs(IReadOnlyList<GridColumn> columns, IReadOnlyList<GridRecord> rows)
    {
        lock (_gate)
            return GridSchema.Serialize(columns) != GridSchema.Serialize(_columns) || rows.Count != _rows.Count ||
                   rows.Zip(_rows).Any(p => p.First.Id != p.Second.Id || columns.Any(c => p.First[c.Key] != p.Second[c.Key]));
    }

    /// <summary>The batch bar's actions: approve or reject the pending projects, and print the selection.</summary>
    public static readonly GridBatchAction[] BatchActions =
    [
        new() { Key = "approve", Label = "Approve", Icon = "check", Primary = true, Confirm = true, When = r => r["approval"] == "pending" },
        new() { Key = "reject", Label = "Reject", Icon = "close", Danger = true, Confirm = true, When = r => r["approval"] == "pending" },
        GridBatchAction.Printing(),
    ];

    /// <summary>
    /// Runs an Approve or Reject: saves the new approval of those rows straight away (as an ERP posts a document;
    /// the grid's other unsaved edits stay unsaved) and sets it on the grid's rows. Returns the message to show.
    /// </summary>
    public string ApplyBatch(GridBatchRequest request)
    {
        var value = request.Action.Key switch { "approve" => "approved", "reject" => "rejected", _ => null };
        if (value is null || request.Rows.Count == 0) return "Nothing to do.";
        var ids = request.Rows.Select(r => r.Id).ToHashSet();
        lock (_gate)
            foreach (var row in _rows.Where(r => ids.Contains(r.Id) && r["approval"] == "pending")) row["approval"] = value;
        foreach (var row in request.Rows) row["approval"] = value;
        var count = request.Rows.Count;
        return $"{count} {(count == 1 ? "project" : "projects")} {value}.";
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

    public static readonly GridOption[] Approvals =
        [new("pending", "Pending", "default", "clock-circle"), new("approved", "Approved", "success", "check-circle"), new("rejected", "Rejected", "error", "close-circle")];

    public static readonly GridOption[] Priorities =
        [new("low", "Low", null, "arrow-down"), new("medium", "Medium", null, "minus"), new("high", "High", null, "arrow-up"), new("urgent", "Urgent", null, "fire")];

    public static readonly GridOption[] Teams =
    [
        new("web", "Web", "blue"), new("mobile", "Mobile", "purple"), new("data", "Data", "cyan"),
        new("design", "Design", "magenta"), new("infra", "Infrastructure", "gold"),
    ];

    /// <summary>Header bands over the money and the dates (a multi-level header).</summary>
    public static readonly HeaderGroup[] HeaderBands =
        [new("Money", "budget", "spent", "progress"), new("Schedule", "start", "due")];

    public static List<GridColumn> SeedColumns() =>
    [
        new() { Key = "project", Title = "Project", Type = GridCellType.Text, Width = 220, Fixed = GridFixed.Left, Required = true },
        new() { Key = "owner", Title = "Owner", Type = GridCellType.Select, Width = 190, Options = Owners, Required = true, Avatar = true, Filterable = true },
        new() { Key = "status", Title = "Status", Type = GridCellType.Tag, Width = 140, Options = Statuses, Required = true, Filterable = true },
        // Set by the batch bar's Approve and Reject, not by editing.
        new() { Key = "approval", Title = "Approval", Type = GridCellType.Tag, Width = 140, Options = Approvals, Editable = false, Filterable = true },
        new() { Key = "priority", Title = "Priority", Type = GridCellType.Select, Width = 130, Options = Priorities, Filterable = true },
        new() { Key = "teams", Title = "Teams", Type = GridCellType.MultiSelect, Width = 240, Options = Teams, Filterable = true },
        new() { Key = "budget", Title = "Budget", Type = GridCellType.Currency, Width = 140, Min = 0, Required = true },
        new() { Key = "spent", Title = "Spent", Type = GridCellType.Currency, Width = 140, Min = 0 },
        new() { Key = "progress", Title = "Progress", Type = GridCellType.Progress, Width = 180, Min = 0, Max = 100 },
        new() { Key = "start", Title = "Start", Type = GridCellType.Date, Width = 140 },
        new() { Key = "due", Title = "Due", Type = GridCellType.Date, Width = 140 },
        new() { Key = "health", Title = "Health", Type = GridCellType.Rating, Width = 160, Min = 0, Max = 5 },
        new() { Key = "billable", Title = "Billable", Type = GridCellType.Switch, Width = 110, Filterable = true },
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
                ["approval"] = status == "done" || i % 4 == 2 ? "approved" : i % 5 == 3 ? "rejected" : "pending",
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
