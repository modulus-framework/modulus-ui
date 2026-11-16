using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Pivot;

public sealed partial class PivotQuery<T>
{
    // Fields ---------------------------------------------------------------------------------------------------

    private sealed record Field(string Key, string Title, bool IsDate, Func<PivotInterval, (LambdaExpression Key, bool Text)> KeyFor,
        Func<PivotInterval, string, string> Label);

    private sealed record RatioDef(string Key, string Title, string Numerator, string Denominator, int Decimals, bool Percent);

    private sealed record MeasureDef(string Key, string Title, Expression<Func<T, decimal>> Value, PivotAggregate Aggregate, int Decimals);

    private PivotQuery<T> Add(Field field)
    {
        if (field.Key.Length == 0 || field.Key.Contains(',') || field.Key.Contains(':'))
            throw new ArgumentException("A field key is not empty and has no ',' or ':'.", nameof(field));
        if (_fields.Any(f => f.Key == field.Key)) throw new ArgumentException($"The field '{field.Key}' is already registered.", nameof(field));
        _fields.Add(field);
        return this;
    }

    private static (string Name, string? Interval) Split(string groupBy)
    {
        var colon = groupBy.IndexOf(':');
        return colon < 0 ? (groupBy, null) : (groupBy[..colon], groupBy[(colon + 1)..]);
    }

    private (LambdaExpression Key, bool Text) KeyOf(string groupBy)
    {
        var (name, interval) = Split(groupBy);
        var field = _fields.First(f => f.Key == name);
        return field.KeyFor(interval is null ? PivotInterval.Month : Enum.Parse<PivotInterval>(interval, true));
    }

    /// <summary>A group's label (<c>March 2025</c>, <c>None</c>).</summary>
    private string Label(string groupBy, string key)
    {
        var (name, interval) = Split(groupBy);
        var field = _fields.First(f => f.Key == name);
        return field.Label(interval is null ? PivotInterval.Month : Enum.Parse<PivotInterval>(interval, true), key);
    }

    /// <summary>How the groups of a level sort: dates and enums by their number, text by the culture; "None" last.</summary>
    private int Order(string groupBy, string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return (a.Length == 0).CompareTo(b.Length == 0);
        if (KeyOf(groupBy).Text) return string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
        return long.Parse(a, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
            .CompareTo(long.Parse(b, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
    }

    private static string DateLabel(PivotInterval interval, string key)
    {
        if (!long.TryParse(key, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)) return "None";
        var culture = CultureInfo.CurrentCulture;
        return interval switch
        {
            PivotInterval.Year => n.ToString(CultureInfo.InvariantCulture),
            PivotInterval.Quarter => $"Q{n % 10} {n / 10}",
            PivotInterval.Month => new DateTime((int)(n / 100), (int)(n % 100), 1).ToString("MMMM yyyy", culture),
            PivotInterval.Week => WeekLabel(DateOnly.FromDayNumber((int)(n * 7))),
            _ => new DateTime((int)(n / 10_000), (int)(n / 100 % 100), (int)(n % 100)).ToString("dd MMM yyyy", culture),
        };

        static string WeekLabel(DateOnly monday)
        {
            var date = monday.ToDateTime(TimeOnly.MinValue);
            return $"W{ISOWeek.GetWeekOfYear(date).ToString("00", CultureInfo.InvariantCulture)} {ISOWeek.GetYear(date).ToString(CultureInfo.InvariantCulture)}";
        }
    }
}
