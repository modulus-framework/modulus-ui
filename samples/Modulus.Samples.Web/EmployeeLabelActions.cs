using Modulus.Samples.Shared;

namespace Modulus.Samples.Web;

/// <summary>
/// The label POSTs behind /tables/advanced (MVC <c>TablesController</c> and the Razor Pages <c>Tables/Advanced</c>
/// page): adding a label from a dropdown's "Add item" row, and giving a label to the selected employees.
/// </summary>
public static class EmployeeLabelActions
{
    /// <summary>The reply <c>app-creatable-select add-href</c> expects: the new option, or <c>{ error }</c>.</summary>
    public static object Add(string? label, string? color)
    {
        var (added, error) = EmployeeLabels.Add(label, color);
        return added is null ? new { error } : new { value = added.Value, label = added.Label, color = added.Color };
    }

    /// <summary>Gives the label to the employees; returns the toast text, or null when nothing was asked.</summary>
    public static string? Apply(IReadOnlyCollection<int> ids, string? value)
    {
        if (ids.Count == 0 || EmployeeLabels.Find(value) is not { } label) return null;
        var count = EmployeeLabels.Apply(ids, label.Value);
        return count == 0
            ? $"Everyone selected already has “{label.Label}”"
            : $"Added “{label.Label}” to {count} {(count == 1 ? "employee" : "employees")}";
    }
}
