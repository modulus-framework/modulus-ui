using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Pivot;

/// <summary>The steps a date field groups by (<c>date:month</c>).</summary>
public enum PivotInterval
{
    Year,
    Quarter,
    Month,
    Week,
    Day,
}
