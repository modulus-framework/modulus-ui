using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Pivot;

/// <summary>A field the pivot can group by, for the header dropdowns.</summary>
/// <param name="Key">The group-by key (<c>customer</c>), or the field of a date (<c>date</c>, grouped as <c>date:month</c>).</param>
public sealed record PivotFieldInfo(string Key, string Title, bool IsDate);
