using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Pivot;

/// <summary>
/// A pivot table over a queryable source, in the manner of <see cref="Data.TableQuery{T}"/>: only registered fields
/// group and only registered measures aggregate, so nothing from the query string reaches the query unchecked.
/// <code>
/// static readonly PivotQuery&lt;Shipment&gt; Pivot = PivotQuery&lt;Shipment&gt;.Create()
///     .Dimension("customer", "Customer", s => s.Customer)
///     .Dimension("status", "Status", s => s.Status)
///     .Date("date", "Date", s => s.Date)
///     .Measure("amount", "Amount", s => s.Amount)
///     .Default(rows: ["customer"], columns: ["date:year"], measures: ["count", "amount"]);
///
/// var state = Pivot.State(query);                          // from the query string, checked
/// var result = Pivot.Run(db.Shipments.Where(…), state);    // GROUP BYs in the database
/// </code>
/// <para>
/// As in Odoo, the pivot reads only what it shows. <see cref="Run"/> sends one <c>GroupBy</c> per pair of depths (row
/// depth × column depth) that has open headers, each filtered to the records under those open headers. Every query
/// returns a record count and one sum (or min, or max) per measure for each of its groups, so every total comes
/// straight from the database; averages are sums divided by counts. A closed header costs one group, however many
/// records or group-bys lie under it.
/// </para>
/// </summary>
public sealed partial class PivotQuery<T>
{
    /// <summary>Group-by levels of both axes together.</summary>
    public const int MaxLevels = 8;

    /// <summary>Measures shown at once (<c>count</c> included).</summary>
    public const int MaxMeasures = 8;

    /// <summary>The record count measure, always available.</summary>
    public const string Count = "count";

    private readonly List<Field> _fields = [];
    private readonly List<MeasureDef> _measures = [];
    private readonly List<RatioDef> _ratios = [];
    private PivotState _default = new() { Measures = [Count] };
    private int _maxGroups = 10_000;

    private PivotQuery() { }

    public static PivotQuery<T> Create() => new();

    /// <summary>The fields to group by, in the order the header dropdown lists them.</summary>
    public IReadOnlyList<PivotFieldInfo> Fields => _fields.Select(f => new PivotFieldInfo(f.Key, f.Title, f.IsDate)).ToArray();

    /// <summary>The measures, in the order the Measures menu lists them: the sums, then the ratios, <c>count</c> last (as in Odoo).</summary>
    public IReadOnlyList<PivotMeasureInfo> Measures =>
        _measures.Select(m => new PivotMeasureInfo(m.Key, m.Title, m.Decimals))
            .Concat(_ratios.Select(r => new PivotMeasureInfo(r.Key, r.Title, r.Decimals, r.Percent)))
            .Append(new PivotMeasureInfo(Count, "Count", 0)).ToArray();

    /// <summary>The state of a page that names none.</summary>
    public PivotState DefaultState => _default;

    /// <summary>Most groups one table may read (about the cells it shows); beyond it the result is an error, not a huge table.</summary>
    public int MaxGroups => _maxGroups;

    /// <summary>Groups by a text value; a null groups as "None".</summary>
    public PivotQuery<T> Dimension(string key, string title, Expression<Func<T, string?>> value) =>
        Add(new Field(key, title, false, _ => (value, true), (_, k) => k.Length == 0 ? "None" : k));

    /// <summary>Groups by an enum value, labelled with <paramref name="label"/> or the member's name. The database groups the number.</summary>
    public PivotQuery<T> Dimension<TEnum>(string key, string title, Expression<Func<T, TEnum>> value, Func<TEnum, string>? label = null)
        where TEnum : struct, Enum
    {
        var number = Expression.Lambda(Expression.Convert(Expression.Convert(value.Body, typeof(long)), typeof(long?)), value.Parameters);
        return Add(new Field(key, title, false, _ => (number, false), (_, k) =>
        {
            if (!long.TryParse(k, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)) return "None";
            var member = (TEnum)Enum.ToObject(typeof(TEnum), n);
            return label?.Invoke(member) ?? member.ToString();
        }));
    }

    /// <summary>Groups by a date, by year, quarter, month, week (from Monday) or day (<c>{key}:month</c>).</summary>
    public PivotQuery<T> Date(string key, string title, Expression<Func<T, DateOnly>> value)
    {
        var d = value.Body;
        Expression P(string name) => Expression.Convert(Expression.Property(d, name), typeof(long));
        Expression C(long v) => Expression.Constant(v);
        LambdaExpression Keyed(Expression body) => Expression.Lambda(Expression.Convert(body, typeof(long?)), value.Parameters);
        var year = P(nameof(DateOnly.Year));
        var month = P(nameof(DateOnly.Month));
        var keys = new Dictionary<PivotInterval, LambdaExpression>
        {
            [PivotInterval.Year] = Keyed(year),
            // 20243 is the third quarter of 2024; 202407 is July 2024; 20240715 the 15th of July. All sort as numbers.
            [PivotInterval.Quarter] = Keyed(Expression.Add(Expression.Multiply(year, C(10)), Expression.Divide(Expression.Add(month, C(2)), C(3)))),
            [PivotInterval.Month] = Keyed(Expression.Add(Expression.Multiply(year, C(100)), month)),
            // Days since 0001-01-01, a Monday, in whole weeks: the week's number, from Monday.
            [PivotInterval.Week] = Keyed(Expression.Divide(P(nameof(DateOnly.DayNumber)), C(7))),
            [PivotInterval.Day] = Keyed(Expression.Add(Expression.Multiply(year, C(10_000)), Expression.Add(Expression.Multiply(month, C(100)), P(nameof(DateOnly.Day))))),
        };
        return Add(new Field(key, title, true, interval => (keys[interval], false), DateLabel));
    }

    /// <summary>A measure: the <paramref name="aggregate"/> of a number per group, shown with <paramref name="decimals"/> decimals.</summary>
    public PivotQuery<T> Measure(string key, string title, Expression<Func<T, decimal>> value, PivotAggregate aggregate = PivotAggregate.Sum, int decimals = 2)
    {
        if (key == Count || _measures.Any(m => m.Key == key)) throw new ArgumentException($"The measure '{key}' is already registered.", nameof(key));
        _measures.Add(new MeasureDef(key, title, value, aggregate, decimals));
        return this;
    }

    /// <summary>
    /// A derived measure: the <paramref name="numerator"/> measure divided by the <paramref name="denominator"/> measure,
    /// worked out for every cell and total from their sums, so a total is the ratio of the totals (margin = profit ÷
    /// sales), not the sum of the cells' ratios. A cell with no denominator shows nothing. Both must be registered
    /// measures or <see cref="Count"/> (amount per shipment = amount ÷ count), and a ratio can't be a part of another.
    /// </summary>
    public PivotQuery<T> Ratio(string key, string title, string numerator, string denominator, int decimals = 2, bool percent = false)
    {
        if (key == Count || Measures.Any(m => m.Key == key)) throw new ArgumentException($"The measure '{key}' is already registered.", nameof(key));
        if (!IsBase(numerator)) throw new ArgumentException($"'{numerator}' is not a measure registered before the ratio '{key}'.", nameof(numerator));
        if (!IsBase(denominator)) throw new ArgumentException($"'{denominator}' is not a measure registered before the ratio '{key}'.", nameof(denominator));
        _ratios.Add(new RatioDef(key, title, numerator, denominator, decimals, percent));
        return this;
    }

    /// <summary>A whole-number measure (quantities).</summary>
    public PivotQuery<T> Measure(string key, string title, Expression<Func<T, int>> value, PivotAggregate aggregate = PivotAggregate.Sum, int decimals = 0) =>
        Measure(key, title, Expression.Lambda<Func<T, decimal>>(Expression.Convert(value.Body, typeof(decimal)), value.Parameters), aggregate, decimals);

    /// <summary>What a page shows when its URL names no state: these group-bys, with both "Total" headers open.</summary>
    public PivotQuery<T> Default(IReadOnlyList<string> rows, IReadOnlyList<string> columns, IReadOnlyList<string> measures)
    {
        var state = new PivotState { Rows = rows, Columns = columns, Measures = measures };
        state = state with
        {
            RowPaths = rows.Count > 0 ? [PivotPath.Root] : [],
            ColumnPaths = columns.Count > 0 ? [PivotPath.Root] : [],
        };
        _default = Normalize(state);
        return this;
    }

    /// <summary>Most groups one table may read (default 10,000).</summary>
    public PivotQuery<T> Limit(int maxGroups)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxGroups, 1);
        _maxGroups = maxGroups;
        return this;
    }

    /// <summary>
    /// A copy without the group-bys and measures <paramref name="allow"/> refuses (<see cref="State.FieldAccess.For"/>;
    /// <see cref="Count"/> always stays): they leave the menus, and a state naming one drops it like an unknown key, so
    /// the database never groups or adds up a field the user may not see. Run each user's pivot on their copy.
    /// </summary>
    public PivotQuery<T> Restrict(Func<string, bool> allow)
    {
        var copy = new PivotQuery<T> { _maxGroups = _maxGroups };
        copy._fields.AddRange(_fields.Where(f => allow(f.Key)));
        copy._measures.AddRange(_measures.Where(m => allow(m.Key)));
        copy._ratios.AddRange(_ratios.Where(r => allow(r.Key) && Allowed(r.Numerator) && Allowed(r.Denominator)));
        bool Allowed(string key) => key == Count || allow(key);
        var state = copy.Normalize(_default);
        // The default's open "Total" headers belong to group-bys that may have gone.
        copy._default = state with
        {
            RowPaths = state.Rows.Count > 0 ? state.RowPaths : [],
            ColumnPaths = state.Columns.Count > 0 ? state.ColumnPaths : [],
        };
        return copy;
    }
}
