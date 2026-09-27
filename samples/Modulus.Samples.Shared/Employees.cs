using System.Globalization;
using System.Text;
using Modulus.Ui.Abstractions;

namespace Modulus.Samples.Shared;

public enum EmployeeStatus { Active, Remote, OnLeave, Probation }

public sealed record Employee(int Id, string Name, string Email, string Position, string Department, string Office, DateTime StartDate, decimal Salary, EmployeeStatus Status)
{
    public int Tint => Fmt.Tint(Name);
}

/// <summary>Filters for the data table page (/tables/advanced).</summary>
public sealed record EmployeeQuery(string? Keyword = null, string? Department = null, string? Office = null, EmployeeStatus? Status = null, string? Label = null);

/// <summary>A read-only employee directory for the data table page: search, filter, sort, page, pick columns, export.</summary>
public static class EmployeeDirectory
{
    public static readonly string[] Departments = ["Engineering", "Design", "Marketing", "Sales", "Support", "Finance", "People"];
    public static readonly string[] Offices = ["Berlin", "London", "New York", "Singapore", "Tokyo", "Remote"];

    /// <summary>Optional columns (key, title); Employee and Status are always shown.</summary>
    public static readonly TableColumn[] OptionalColumns =
        [new("position", "Position"), new("department", "Department"), new("office", "Office"), new("start", "Start date"), new("salary", "Salary")];

    private static readonly Dictionary<string, string[]> Positions = new()
    {
        ["Engineering"] = ["Software Engineer", "Senior Engineer", "Staff Engineer", "Engineering Manager", "QA Engineer"],
        ["Design"] = ["Product Designer", "UX Researcher", "Design Lead"],
        ["Marketing"] = ["Content Strategist", "Growth Marketer", "Marketing Manager"],
        ["Sales"] = ["Account Executive", "Sales Engineer", "Sales Manager"],
        ["Support"] = ["Support Specialist", "Support Lead"],
        ["Finance"] = ["Accountant", "Financial Analyst", "Controller"],
        ["People"] = ["Recruiter", "People Partner"],
    };

    public static IReadOnlyList<Employee> All { get; } = Seed();

    public static string StatusLabel(EmployeeStatus s) => s == EmployeeStatus.OnLeave ? "On leave" : s.ToString();

    public static string StatusBadge(EmployeeStatus s) => s switch
    {
        EmployeeStatus.Active => "m-badge-green-lt",
        EmployeeStatus.Remote => "m-badge-blue-lt",
        EmployeeStatus.OnLeave => "m-badge-orange-lt",
        _ => "m-badge-purple-lt",
    };

    /// <summary>Parses the "hide" query value (comma-separated column keys).</summary>
    public static HashSet<string> ParseHidden(string? hide) => TableColumns.ParseHidden(hide, OptionalColumns);

    /// <summary>The "hide" value after toggling one column (null when every column is shown).</summary>
    public static string? ToggleHidden(IReadOnlySet<string> hidden, string key) => TableColumns.ToggleHidden(hidden, key, OptionalColumns);

    public static IEnumerable<Employee> Filter(EmployeeQuery query)
    {
        IEnumerable<Employee> rows = All;
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var k = query.Keyword.Trim();
            rows = rows.Where(e => e.Name.Contains(k, StringComparison.OrdinalIgnoreCase) || e.Email.Contains(k, StringComparison.OrdinalIgnoreCase) || e.Position.Contains(k, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrEmpty(query.Department)) rows = rows.Where(e => e.Department == query.Department);
        if (!string.IsNullOrEmpty(query.Office)) rows = rows.Where(e => e.Office == query.Office);
        if (query.Status is { } s) rows = rows.Where(e => e.Status == s);
        if (!string.IsNullOrEmpty(query.Label)) rows = rows.Where(e => EmployeeLabels.Of(e.Id).Contains(query.Label));
        return rows;
    }

    public static ProTableResponse<Employee> Query(EmployeeQuery query, ProTableRequest request)
    {
        var rows = Filter(query);
        rows = request.SortField is null
            ? rows.OrderBy(e => e.Id)
            : rows.SortBy(request, nameof(Employee.Name), e => e.Name)
                  .SortBy(request, nameof(Employee.Position), e => e.Position)
                  .SortBy(request, nameof(Employee.Department), e => e.Department)
                  .SortBy(request, nameof(Employee.Office), e => e.Office)
                  .SortBy(request, nameof(Employee.StartDate), e => e.StartDate)
                  .SortBy(request, nameof(Employee.Salary), e => e.Salary);
        return rows.ToPage(request);
    }

    /// <summary>Summary for the toolbar: count and average salary of the filtered rows.</summary>
    public static (int Count, decimal AverageSalary) Summary(EmployeeQuery query)
    {
        var rows = Filter(query).ToList();
        return (rows.Count, rows.Count == 0 ? 0 : Math.Round(rows.Average(e => e.Salary)));
    }

    /// <summary>CSV of the given rows (RFC 4180 quoting).</summary>
    public static string Csv(IEnumerable<Employee> rows)
    {
        static string Q(string v) => v.IndexOfAny([',', '"', '\n']) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
        var sb = new StringBuilder("Id,Name,Email,Position,Department,Office,Start date,Salary,Status\n");
        foreach (var e in rows)
            sb.Append(string.Join(',', e.Id.ToString(CultureInfo.InvariantCulture), Q(e.Name), Q(e.Email), Q(e.Position), Q(e.Department), Q(e.Office),
                e.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), e.Salary.ToString(CultureInfo.InvariantCulture), StatusLabel(e.Status))).Append('\n');
        return sb.ToString();
    }

    /// <summary>Rows for an export: the selected ids, or every row matching the filters.</summary>
    public static IEnumerable<Employee> ForExport(EmployeeQuery query, IReadOnlyCollection<int> ids) =>
        ids.Count > 0 ? All.Where(e => ids.Contains(e.Id)) : Filter(query);

    private static List<Employee> Seed()
    {
        var rng = new Random(20260925);
        var list = new List<Employee>();
        var used = new HashSet<string>();
        for (var id = 1; list.Count < 86; id++)
        {
            var first = SampleStore.FirstNames[rng.Next(SampleStore.FirstNames.Length)];
            var last = SampleStore.LastNames[rng.Next(SampleStore.LastNames.Length)];
            var name = $"{first} {last}";
            if (!used.Add(name)) continue;
            var dept = Departments[rng.Next(Departments.Length)];
            var roles = Positions[dept];
            var level = rng.Next(roles.Length);
            var email = $"{Ascii(first)}.{Ascii(last)}@modulus.dev".ToLowerInvariant();
            var start = new DateTime(2018, 1, 1).AddDays(rng.Next(0, 3100));
            var salary = Math.Round((decimal)(52_000 + level * 18_000 + rng.Next(0, 24_000)) / 500) * 500;
            var status = rng.Next(20) switch { < 12 => EmployeeStatus.Active, < 16 => EmployeeStatus.Remote, < 18 => EmployeeStatus.OnLeave, _ => EmployeeStatus.Probation };
            list.Add(new Employee(list.Count + 1, name, email, roles[level], dept, Offices[rng.Next(Offices.Length)], start, salary, status));
        }
        return list;
    }

    private static string Ascii(string s) =>
        new(s.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
}

/// <summary>
/// Colour-tagged labels on employees (/tables/advanced). People add labels from the filter or the batch bar, so
/// the list lives in memory for the whole app, like the other sample stores.
/// </summary>
public static class EmployeeLabels
{
    private static readonly Lock Gate = new();
    private static IReadOnlyList<GridOption> _options =
        [new("mentor", "Mentor", "green"), new("onCall", "On call", "red"), new("newHire", "New hire", "blue"), new("leadership", "Leadership", "purple")];
    private static readonly Dictionary<int, List<string>> Assigned = Seed();

    public static IReadOnlyList<GridOption> Options { get { lock (Gate) return _options; } }

    public static GridOption? Find(string? value) => Options.FirstOrDefault(o => o.Value == value);

    /// <summary>Label values of an employee, in the order they were added.</summary>
    public static IReadOnlyList<string> Of(int employeeId)
    {
        lock (Gate) return Assigned.TryGetValue(employeeId, out var list) ? [.. list] : [];
    }

    /// <summary>Adds a label (see <see cref="GridSchema.AddOption"/>): the new option, or an error.</summary>
    public static (GridOption? Added, string? Error) Add(string? label, string? color)
    {
        lock (Gate)
        {
            var (options, added, error) = GridSchema.AddOption(_options, label, color);
            _options = options;
            return (added, error);
        }
    }

    /// <summary>Gives the label to each employee; returns how many didn't have it yet.</summary>
    public static int Apply(IEnumerable<int> employeeIds, string value)
    {
        lock (Gate)
        {
            if (!_options.Any(o => o.Value == value)) return 0;
            var count = 0;
            foreach (var id in employeeIds.Distinct().Where(id => EmployeeDirectory.All.Any(e => e.Id == id)))
            {
                if (!Assigned.TryGetValue(id, out var list)) Assigned[id] = list = [];
                if (list.Contains(value)) continue;
                list.Add(value);
                count++;
            }
            return count;
        }
    }

    private static Dictionary<int, List<string>> Seed()
    {
        var rng = new Random(20260927);
        var map = new Dictionary<int, List<string>>();
        foreach (var e in EmployeeDirectory.All)
        {
            var labels = new List<string>();
            if (rng.Next(5) == 0) labels.Add("mentor");
            if (rng.Next(8) == 0) labels.Add("onCall");
            if (e.StartDate >= new DateTime(2025, 7, 1)) labels.Add("newHire");
            if (e.Position.Contains("Manager") || e.Position.Contains("Lead")) labels.Add("leadership");
            if (labels.Count > 0) map[e.Id] = labels;
        }
        return map;
    }
}
