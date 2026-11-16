using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Modulus.Ui.Abstractions.Data;

namespace Modulus.Ui.Abstractions;

public static partial class DataGridAuto
{
    /// <summary>A discovered column over a property (or a parent's property): reads, formats, sorts and searches it.</summary>
    private sealed class Auto<TItem, TValue>(string key, string title, ColumnKind kind, GridCellType cellType, int? width, IReadOnlyList<GridOption> options,
        Expression<Func<TItem, TValue>> access)
        : DataGridAutoColumn<TItem>(key, title, kind, cellType, width, options)
    {
        private readonly Expression<Func<TItem, TValue>> _access = access;
        private readonly Func<TItem, TValue> _read = access.Compile();
        private readonly Type _raw = Nullable.GetUnderlyingType(access.ReturnType) ?? access.ReturnType;

        public override Expression<Func<TItem, object?>> Boxed { get; } =
            Expression.Lambda<Func<TItem, object?>>(Expression.Convert(access.Body, typeof(object)), access.Parameters);

        public override object? Value(TItem row) => _read(row);

        public override string? Text(TItem row, CultureInfo? culture = null)
        {
            var value = _read(row);
            culture ??= CultureInfo.CurrentCulture;
            return value switch
            {
                null => null,
                Enum e => EnumText(e.ToString() ?? ""),
                bool b => b ? "Yes" : "No",
                DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTimeOffset d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeOnly t => t.ToString("HH:mm", CultureInfo.InvariantCulture),
                decimal m => m.ToString("0.##", culture),
                double d => d.ToString("0.##", culture),
                float f => f.ToString("0.##", culture),
                IFormattable n => n.ToString("N0", culture),
                _ => value.ToString(),
            };
        }

        public override string? Invariant(TItem row)
        {
            var value = _read(row);
            return value switch
            {
                null => null,
                string s => s,
                bool b => GridSchema.FromFlag(b),
                Enum e => e.ToString(),
                DateTime d => GridSchema.FromDate(d),
                DateTimeOffset d => GridSchema.FromDate(d.DateTime),
                DateOnly d => GridSchema.FromDate(d),
                TimeOnly t => t.ToString("HH:mm", CultureInfo.InvariantCulture),
                IFormattable n => GridSchema.FromNumber(Convert.ToDecimal(n, CultureInfo.InvariantCulture)),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture),
            };
        }

        internal override void Register(TableQuery<TItem> query, bool ignoreCase)
        {
            query.Sort(Key, _access);
            query.Search(Key, text => Search(text, ignoreCase));
        }

        // The ColumnSearch grammar (>100, <=5, =12, 10..20, =exact, !exclude, contains) as expression trees, so the
        // database runs it. A query that doesn't parse matches everything (the search is ignored, not the rows lost).
        private Expression<Func<TItem, bool>> Search(string query, bool ignoreCase) => _raw switch
        {
            _ when ColumnSearch.ValueList(query.Trim()) is { } list => Listed(list.Exclude, list.Values, ignoreCase),
            _ when _raw == typeof(string) => Text(query.Trim(), ignoreCase),
            _ when _raw == typeof(bool) => Flag(query.Trim()),
            _ when _raw.IsEnum => Enum(query.Trim()),
            _ when _raw == typeof(Guid) => GuidOf(query.Trim()),
            _ when _raw == typeof(char) => Text(query.Trim(), ignoreCase, ToString(_access.Body)),
            _ when Kind == ColumnKind.Number => Number(query.Trim()),
            _ when Kind == ColumnKind.Date => Date(query.Trim()),
            _ => _ => true,
        };

        private Expression<Func<TItem, bool>> Text(string query, bool ignoreCase, Expression? body = null)
        {
            body ??= _access.Body;
            var x = _access.Parameters[0];
            var lowered = ignoreCase;

            Expression Subject() => lowered ? Expression.Call(body, ToLower) : body;
            Expression Value(string text) => Box.Of(lowered ? text.ToLowerInvariant() : text, typeof(string));
            // The body is always text (a string column, or a char as text): a null cell contains nothing.
            Expression Keep(Expression test) => Expression.AndAlso(Expression.NotEqual(body, Expression.Constant(null, body.Type)), test);

            Expression test;
            if (query.StartsWith('!') && query.Length > 1)
                test = Expression.Not(Keep(Expression.Call(Subject(), Contains, Value(query[1..].Trim()))));
            else if (query.StartsWith('=') && query.Length > 1)
                test = Keep(Expression.Equal(Subject(), Value(query[1..].Trim())));
            else
                test = Keep(Expression.Call(Subject(), Contains, Value(query)));
            return Expression.Lambda<Func<TItem, bool>>(test, x);
        }

        // A checkbox list (=a|b, !=a|b) of displayed values: text as an IN, enum labels and Yes/No as the values they
        // show. Other columns (numbers, dates) list nothing here, so the search is ignored.
        private Expression<Func<TItem, bool>> Listed(bool exclude, IReadOnlySet<string> values, bool ignoreCase)
        {
            var x = _access.Parameters[0];
            if (_raw == typeof(string) || _raw == typeof(char))
            {
                var body = _raw == typeof(string) ? _access.Body : ToString(_access.Body);
                return Expression.Lambda<Func<TItem, bool>>(ValueListSearch.Test(exclude, values, body, ignoreCase), x);
            }
            object[] picked;
            if (_raw.IsEnum)
                picked = [.. System.Enum.GetValues(_raw).Cast<object>().Where(v => values.Contains(EnumText(v.ToString() ?? "")))];
            else if (_raw == typeof(bool))
                picked = [.. new object[] { true, false }.Where(v => values.Contains((bool)v ? "Yes" : "No"))];
            else
                return Always();
            Expression test = Expression.Constant(false);
            foreach (var value in picked) test = Expression.OrElse(test, Expression.Equal(_access.Body, Constant(value, _access.Body.Type)));
            // A blank is a null cell (a nullable enum or bool).
            if (values.Contains("") && _access.Body.Type != _raw) test = Expression.OrElse(test, Expression.Equal(_access.Body, Expression.Constant(null, _access.Body.Type)));
            return Expression.Lambda<Func<TItem, bool>>(exclude ? Expression.Not(test) : test, x);
        }

        private Expression<Func<TItem, bool>> Flag(string query)
        {
            var q = query.StartsWith('=') ? query[1..].Trim() : query;
            return TryFlag(q, out var flag)
                ? Expression.Lambda<Func<TItem, bool>>(Expression.Equal(_access.Body, Constant(flag, _access.Body.Type)), _access.Parameters[0])
                : Always();
        }

        private Expression<Func<TItem, bool>> Enum(string query)
        {
            var negated = query.StartsWith('!');
            var q = negated || query.StartsWith('=') ? query[1..].Trim() : query;
            if (!System.Enum.TryParse(_raw, q, ignoreCase: true, out var parsed)) return Always();
            var equal = Expression.Equal(_access.Body, Constant(parsed!, _access.Body.Type));
            return Expression.Lambda<Func<TItem, bool>>(negated ? Expression.Not(equal) : equal, _access.Parameters[0]);
        }

        private Expression<Func<TItem, bool>> GuidOf(string query)
        {
            var q = query.StartsWith('=') ? query[1..].Trim() : query;
            return System.Guid.TryParse(q, out var guid)
                ? Expression.Lambda<Func<TItem, bool>>(Expression.Equal(_access.Body, Constant(guid, _access.Body.Type)), _access.Parameters[0])
                : Always();
        }

        private Expression<Func<TItem, bool>> Number(string query)
        {
            var (op, from, to) = Grammar(query) ?? ("=", query, ""); // no operator: the operand itself, as equality
            if (op == "..")
            {
                Expression? test = null;
                if (from.Length > 0 && ParseNumber(from) is { } low)
                    test = Expression.GreaterThanOrEqual(_access.Body, Constant(low, _access.Body.Type));
                if (to.Length > 0 && ParseNumber(to) is { } high)
                {
                    var upper = Expression.LessThanOrEqual(_access.Body, Constant(high, _access.Body.Type));
                    test = test is null ? upper : Expression.AndAlso(test, upper);
                }
                return test is null ? Always() : Expression.Lambda<Func<TItem, bool>>(test, _access.Parameters[0]);
            }
            if (ParseNumber(from) is not { } operand) return Always();
            var body = _access.Body;
            var compare = op switch
            {
                ">" => Expression.GreaterThan(body, Constant(operand, body.Type)),
                ">=" => Expression.GreaterThanOrEqual(body, Constant(operand, body.Type)),
                "<" => Expression.LessThan(body, Constant(operand, body.Type)),
                "<=" => Expression.LessThanOrEqual(body, Constant(operand, body.Type)),
                _ => Expression.Equal(body, Constant(operand, body.Type)),
            };
            return Expression.Lambda<Func<TItem, bool>>(compare, _access.Parameters[0]);
        }

        private Expression<Func<TItem, bool>> Date(string query)
        {
            var body = _access.Body;
            var raw = Nullable.GetUnderlyingType(body.Type) ?? body.Type;
            if (raw == typeof(DateTime) || raw == typeof(DateTimeOffset)) body = Expression.Property(body, raw.GetProperty("Date")!); // the day
            var type = Nullable.GetUnderlyingType(body.Type) ?? body.Type;

            object Operand(DateOnly d) => type == typeof(DateTime) ? new DateTime(d.Year, d.Month, d.Day) : d;

            var (op, from, to) = Grammar(query) ?? ("=", query, ""); // no operator: that day
            if (op == "..")
            {
                Expression? test = null;
                if (from.Length > 0 && ParseDate(from) is { } low)
                    test = Expression.GreaterThanOrEqual(body, Constant(Operand(low), body.Type));
                if (to.Length > 0 && ParseDate(to) is { } high)
                {
                    var upper = Expression.LessThanOrEqual(body, Constant(Operand(high), body.Type));
                    test = test is null ? upper : Expression.AndAlso(test, upper);
                }
                return test is null ? Always() : Expression.Lambda<Func<TItem, bool>>(test, _access.Parameters[0]);
            }
            if (ParseDate(from) is not { } operand) return Always();
            var compare = op switch
            {
                ">" => Expression.GreaterThan(body, Constant(Operand(operand), body.Type)),
                ">=" => Expression.GreaterThanOrEqual(body, Constant(Operand(operand), body.Type)),
                "<" => Expression.LessThan(body, Constant(Operand(operand), body.Type)),
                "<=" => Expression.LessThanOrEqual(body, Constant(Operand(operand), body.Type)),
                _ => Expression.Equal(body, Constant(Operand(operand), body.Type)),
            };
            return Expression.Lambda<Func<TItem, bool>>(compare, _access.Parameters[0]);
        }

        private object? ParseNumber(string text) =>
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? Convert.ChangeType(d, _raw, CultureInfo.InvariantCulture) : null;

        private static DateOnly? ParseDate(string text) =>
            DateOnly.TryParseExact(text, GridSchema.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        private static Expression<Func<TItem, bool>> Always() => _ => true;

        /// <summary>A comparison value as a closure member, so LINQ providers send it as a query parameter (as <see cref="TableQuery{T}"/> does), not SQL text.</summary>
        private static Expression Constant(object? value, Type type)
        {
            var raw = Nullable.GetUnderlyingType(type) ?? type;
            var converted = value is null || value.GetType() == raw ? value : Convert.ChangeType(value, raw, CultureInfo.InvariantCulture);
            return Box.Of(converted, type);
        }

        private static Expression ToString(Expression body) => Expression.Call(body, body.Type.GetMethod(nameof(object.ToString), Type.EmptyTypes, [])!);
    }
}
