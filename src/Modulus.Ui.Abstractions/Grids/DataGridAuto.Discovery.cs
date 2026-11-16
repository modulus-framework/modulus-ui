using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Modulus.Ui.Abstractions.Data;

namespace Modulus.Ui.Abstractions;

public static partial class DataGridAuto
{
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

}
