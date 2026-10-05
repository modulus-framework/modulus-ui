using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Modulus.Ui.Abstractions.Data;

namespace Modulus.Ui.Abstractions;

/// <summary>
/// One column <see cref="DataGridAuto"/> discovers on a CLR type: its key, title and kind, how it formats a cell,
/// and how it registers with a <see cref="TableQuery{T}"/> so the database sorts and searches it.
/// </summary>
public abstract class DataGridAutoColumn<T>
{
    private protected DataGridAutoColumn(string key, string title, ColumnKind kind, GridCellType cellType, int? width, IReadOnlyList<GridOption> options)
    {
        Key = key;
        Title = title;
        Kind = kind;
        CellType = cellType;
        Width = width;
        Options = options;
    }

    /// <summary>The column's key: what the header sorts and searches by (a camelCase path, "customerName").</summary>
    public string Key { get; }

    public string Title { get; }

    public ColumnKind Kind { get; }

    /// <summary>The column as a <see cref="GridCellType"/> of the schema-driven grids.</summary>
    public GridCellType CellType { get; }

    /// <summary>A good width for the column, in pixels, or null to let the table size it.</summary>
    public int? Width { get; }

    /// <summary>Choices of a <see cref="GridCellType.Select"/> column (an enum); empty for the rest.</summary>
    public IReadOnlyList<GridOption> Options { get; }

    public bool AlignRight => Kind == ColumnKind.Number;

    /// <summary>The cell as text in <paramref name="culture"/> (the current culture by default).</summary>
    public abstract string? Text(T row, CultureInfo? culture = null);

    /// <summary>The cell as an invariant string, as a <see cref="GridRecord"/> value stores it.</summary>
    public abstract string? Invariant(T row);

    /// <summary>The cell's raw value, boxed (null when the property, or its parent object, is null).</summary>
    public abstract object? Value(T row);

    /// <summary>The column as <c>x =&gt; (object?)x.Prop</c>, for a boxed property column.</summary>
    public abstract Expression<Func<T, object?>> Boxed { get; }

    /// <summary>Registers the column's sort and its column search with <paramref name="query"/>.</summary>
    /// <param name="ignoreCase">Compare text case-insensitively (an in-memory source; the database collation decides otherwise).</param>
    internal abstract void Register(TableQuery<T> query, bool ignoreCase);

    /// <summary>The cell of an enum value: its option's label, or the name.</summary>
    private protected string EnumText(string name) => Options.FirstOrDefault(o => o.Value == name)?.Label ?? name;
}

/// <summary>
/// The columns, row key and query a data grid derives from a row type's public readable properties:
/// simple properties become columns, a complex object's simple properties are flattened one level (<c>Customer.Name</c>
/// becomes a "Customer name" column that stays null-safe), and the row key is the <c>Id</c>, <c>{T}Id</c> or
/// <c>Key</c> property (the first simple property when there is none). Discovered once per type, then cached.
/// <para>
/// The same discovery feeds the schema-driven grids: <see cref="SchemaColumns{T}"/> maps the columns to
/// <see cref="GridColumn"/>s and <see cref="Records{T}"/> the rows to <see cref="GridRecord"/>s, so a nested level
/// is zero-config too:
/// <code>
/// new NestedGridLevel { Title = "Lines", Columns = DataGridAuto.SchemaColumns&lt;PurchaseLine&gt;() }
/// </code>
/// </para>
/// </summary>
public static class DataGridAuto
{
    /// <summary>Columns beyond this are ignored: a runaway type must not build an unusable table.</summary>
    public const int MaxColumns = 40;

    /// <summary>The columns in display order (the row key property is not among them when it is Id-like).</summary>
    public static IReadOnlyList<DataGridAutoColumn<T>> Columns<T>() => Cache<T>.Model.Columns;

    /// <summary>The key of the row key property: "Id", "{T}Id", "Key", or the first simple property's name.</summary>
    public static string IdKey<T>() => Cache<T>.Model.IdKey;

    /// <summary>The row key of a row, boxed (an int Id becomes the boxed int).</summary>
    public static Func<T, object> RowKeyOf<T>() => Cache<T>.Model.RowKey;

    /// <summary>
    /// A fresh <see cref="TableQuery{T}"/> over <typeparamref name="T"/> with every discovered column sorted and
    /// searched (the <see cref="ColumnSearch"/> grammar: contains, =exact, !exclude, and &gt;100, &lt;=5, 10..20 for
    /// numbers and dates), and <paramref name="defaultSort"/> as the default when it is one of them.
    /// </summary>
    /// <param name="ignoreCase">Compare text case-insensitively (an in-memory source; the database collation decides otherwise).</param>
    public static TableQuery<T> Query<T>(string? defaultSort = null, bool descending = false, bool ignoreCase = false)
    {
        var query = Cache<T>.Model.Start();
        foreach (var column in Cache<T>.Model.Columns) column.Register(query, ignoreCase);
        if (defaultSort is { Length: > 0 } field && query.CanSort(field)) query.DefaultSort(field, descending);
        return query;
    }

    /// <summary>The columns as <see cref="GridColumn"/>s (new instances, safe to change), for the schema-driven grids.</summary>
    public static List<GridColumn> SchemaColumns<T>() =>
        [.. Columns<T>().Select(c => new GridColumn { Key = c.Key, Title = c.Title, Type = c.CellType, Width = c.Width ?? GridSchema.DefaultWidth(c.CellType), Options = c.Options })];

    /// <summary>The rows as <see cref="GridRecord"/>s (invariant values, keyed by column), for the schema-driven grids.</summary>
    public static List<GridRecord> Records<T>(IEnumerable<T> rows)
    {
        var key = RowKeyOf<T>();
        var columns = Columns<T>();
        return [.. rows.Select(row =>
        {
            var record = new GridRecord(Convert.ToString(key(row), CultureInfo.InvariantCulture) ?? "");
            foreach (var c in columns) record[c.Key] = c.Invariant(row);
            return record;
        })];
    }

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

    // ---- Discovery, once per closed type -----------------------------------------------------------------------------

    private static class Cache<T>
    {
        public static readonly Model<T> Model = Discover<T>();
    }

    private sealed class Model<T>(string idKey, Func<TableQuery<T>> start, Func<T, object> rowKey, List<DataGridAutoColumn<T>> columns)
    {
        public string IdKey { get; } = idKey;
        public Func<TableQuery<T>> Start { get; } = start;
        public Func<T, object> RowKey { get; } = rowKey;
        public List<DataGridAutoColumn<T>> Columns { get; } = columns;
    }

    private static Model<T> Discover<T>()
    {
        var type = typeof(T);
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !Ignored(p)).ToList();
        var id = IdProperty(props, type);
        PropertyInfo? fallback = null;
        List<DataGridAutoColumn<T>> columns = [];
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in props)
        {
            if (columns.Count >= MaxColumns) break;
            if (p == id) continue; // the Id-like property keys the rows; it is not a column
            if (Simple(p.PropertyType))
            {
                fallback ??= p; // no Id-like property: the first simple property keys the rows, and stays a column
                Add(Column<T>(p, null));
            }
            else if (Complex<T>(p.PropertyType))
            {
                foreach (var leaf in Leaves(p)) Add(Column<T>(leaf, p));
            }
        }

        id ??= fallback;
        if (id is null)
            throw new InvalidOperationException($"{type.Name} has no Id, {type.Name}Id, Key or simple property a data grid could key its rows by.");
        return new Model<T>(id.Name, Start<T>(id), Key<T>(id), columns);

        void Add(DataGridAutoColumn<T>? column)
        {
            if (column is not null && keys.Add(column.Key)) columns.Add(column);
        }
    }

    private static PropertyInfo? IdProperty(List<PropertyInfo> props, Type type) =>
        props.FirstOrDefault(p => p.Name == "Id" && Simple(p.PropertyType))
        ?? props.FirstOrDefault(p => p.Name == type.Name + "Id" && Simple(p.PropertyType))
        ?? props.FirstOrDefault(p => p.Name == "Key" && Simple(p.PropertyType));

    private static IEnumerable<PropertyInfo> Leaves(PropertyInfo parent) =>
        parent.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !Ignored(p) && Simple(p.PropertyType));

    private static DataGridAutoColumn<T>? Column<T>(PropertyInfo leaf, PropertyInfo? parent)
    {
        var raw = Nullable.GetUnderlyingType(leaf.PropertyType) ?? leaf.PropertyType;
        var kind = IsNumber(raw) ? ColumnKind.Number : IsDate(raw) ? ColumnKind.Date : ColumnKind.Text;
        int? width = kind switch { ColumnKind.Number => 120, ColumnKind.Date => 130, _ => raw == typeof(bool) ? 100 : raw.IsEnum ? 150 : null };
        IReadOnlyList<GridOption> options = raw.IsEnum
            ? [.. Enum.GetValues(raw).Cast<object>().Select((e, i) => new GridOption(e.ToString()!, EnumName(raw, e), GridSchema.NextColor(i)))]
            : [];
        var key = Camel(parent is null ? leaf.Name : parent.Name + leaf.Name);
        var title = parent is null ? Title(leaf) : $"{Title(parent)} {LowerFirst(Title(leaf))}";
        var access = Access<T>(leaf, parent);
        var closed = typeof(Auto<,>).MakeGenericType(typeof(T), access.Type.GetGenericArguments()[1]);
        return (DataGridAutoColumn<T>)Activator.CreateInstance(closed, key, title, kind, CellType(raw), width, options, access)!;
    }

    /// <summary>The property as <c>x =&gt; x.Leaf</c>, or null-safe through its parent: <c>x =&gt; x.Parent == null ? null : x.Parent.Leaf</c>.</summary>
    private static LambdaExpression Access<T>(PropertyInfo leaf, PropertyInfo? parent)
    {
        var x = Expression.Parameter(typeof(T), "x");
        if (parent is null) return Expression.Lambda(Expression.GetFuncType(typeof(T), leaf.PropertyType), Expression.Property(x, leaf), x);
        var value = leaf.PropertyType.IsValueType
            ? typeof(Nullable<>).MakeGenericType(Nullable.GetUnderlyingType(leaf.PropertyType) ?? leaf.PropertyType)
            : leaf.PropertyType;
        var body = Expression.Condition(
            Expression.Equal(Expression.Property(x, parent), Expression.Constant(null, parent.PropertyType)),
            Expression.Default(value),
            Expression.Convert(Expression.Property(Expression.Property(x, parent), leaf), value));
        return Expression.Lambda(Expression.GetFuncType(typeof(T), value), body, x);
    }

    private static Func<TableQuery<T>> Start<T>(PropertyInfo id)
    {
        var x = Expression.Parameter(typeof(T), "x");
        var lambda = Expression.Lambda(Expression.GetFuncType(typeof(T), id.PropertyType), Expression.Property(x, id), x);
        return (Func<TableQuery<T>>)typeof(DataGridAuto).GetMethod(nameof(StartOf), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(T), id.PropertyType).Invoke(null, [lambda])!;
    }

    private static Func<TableQuery<T>> StartOf<T, TId>(Expression<Func<T, TId>> id) => () => TableQuery<T>.By(id);

    private static Func<T, object> Key<T>(PropertyInfo id)
    {
        var x = Expression.Parameter(typeof(T), "x");
        return Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(x, id), typeof(object)), x).Compile();
    }

    private static GridCellType CellType(Type raw) => raw switch
    {
        _ when raw == typeof(bool) => GridCellType.Switch,
        _ when raw.IsEnum => GridCellType.Select,
        _ when IsNumber(raw) => GridCellType.Number,
        _ when IsDate(raw) => GridCellType.Date,
        _ => GridCellType.Text,
    };

    private static bool Simple(Type type)
    {
        var raw = Nullable.GetUnderlyingType(type) ?? type;
        return raw == typeof(string) || raw.IsEnum || raw == typeof(bool) || raw == typeof(Guid) || raw == typeof(char)
            || IsNumber(raw) || IsDate(raw) || raw == typeof(TimeOnly);
    }

    /// <summary>Whether a property is flattened: a class of our own (not the row itself, not a collection, not a system type).</summary>
    private static bool Complex<T>(Type type) =>
        type.IsClass && type != typeof(T)
        && type != typeof(object) && type.Assembly != typeof(object).Assembly
        && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type);

    private static bool IsNumber(Type raw) =>
        raw == typeof(byte) || raw == typeof(sbyte) || raw == typeof(short) || raw == typeof(ushort)
        || raw == typeof(int) || raw == typeof(uint) || raw == typeof(long) || raw == typeof(ulong)
        || raw == typeof(float) || raw == typeof(double) || raw == typeof(decimal);

    private static bool IsDate(Type raw) =>
        raw == typeof(DateTime) || raw == typeof(DateTimeOffset) || raw == typeof(DateOnly);

    private static bool Ignored(PropertyInfo p) =>
        p.GetCustomAttribute<ScaffoldColumnAttribute>()?.Scaffold == false
        || p.GetCustomAttribute<BrowsableAttribute>()?.Browsable == false;

    private static string Title(PropertyInfo p) =>
        p.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? p.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? Sentence(p.Name);

    /// <summary>A property name as a column title: "StartDate" → "Start date", "PurchaseOrderNumber" → "Purchase order number".</summary>
    private static string Sentence(string name)
    {
        var words = SplitWords(name);
        return words.Count == 0 ? name : string.Join(' ', words.Select((w, i) => i == 0 ? w : LowerFirst(w)));
    }

    private static List<string> SplitWords(string name)
    {
        var words = new List<string>();
        var start = 0;
        for (var i = 1; i < name.Length; i++)
        {
            var upper = char.IsUpper(name[i]);
            var prevUpper = char.IsUpper(name[i - 1]);
            if (upper && (!prevUpper || i + 1 < name.Length && char.IsLower(name[i + 1])))
            {
                words.Add(name[start..i]);
                start = i;
            }
        }
        words.Add(name[start..]);
        return words;
    }

    private static string Camel(string path) => char.ToLowerInvariant(path[0]) + path[1..];

    private static string LowerFirst(string text) => char.ToLowerInvariant(text[0]) + text[1..];

    private static string EnumName(Type enumType, object value) =>
        enumType.GetField(value.ToString() ?? "")?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString() ?? "";

    private static readonly MethodInfo Contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
    private static readonly MethodInfo ToLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

    /// <summary>Whether a query means yes or no ("yes", "true", "no", "false").</summary>
    private static bool TryFlag(string text, out bool flag)
    {
        switch (text.ToLowerInvariant())
        {
            case "yes" or "true": flag = true; return true;
            case "no" or "false": flag = false; return true;
            default: flag = false; return false;
        }
    }

    /// <summary>
    /// A comparison query as its operator and operand, or a range as its bounds: (">=", "100", ""), ("..", "10", "20"),
    /// ("..", "", "20"), ("..", "10", ""). Null when the query is no comparison (then it matches as text, or equals).
    /// </summary>
    private static (string Op, string From, string To)? Grammar(string query)
    {
        var range = query.IndexOf("..", StringComparison.Ordinal);
        if (range >= 0) return ("..", query[..range].Trim(), query[(range + 2)..].Trim());
        if (query.StartsWith(">=", StringComparison.Ordinal)) return (">=", query[2..].Trim(), "");
        if (query.StartsWith("<=", StringComparison.Ordinal)) return ("<=", query[2..].Trim(), "");
        if (query.StartsWith('>') || query.StartsWith('<') || query.StartsWith('=')) return (query[..1], query[1..].Trim(), "");
        return null;
    }
}
