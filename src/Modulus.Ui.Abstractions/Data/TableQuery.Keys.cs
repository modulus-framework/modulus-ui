using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using Modulus.Ui.Abstractions.Query;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.Ui.Abstractions.Data;

public sealed partial class TableQuery<T>
{
    // Keys -------------------------------------------------------------------------------------------------------------

    private static readonly HashSet<Type> KeysetTypes =
    [
        typeof(int), typeof(long), typeof(short), typeof(byte), typeof(decimal), typeof(double), typeof(float),
        typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly), typeof(string),
    ];

    private sealed class Key
    {
        public required LambdaExpression Lambda { get; init; }
        public required Type Type { get; init; }
        public required bool Keyset { get; init; }
        public required Func<T, object?> Read { get; init; }

        public static Key Create<TKey>(Expression<Func<T, TKey>> lambda, bool keyset)
        {
            var type = typeof(TKey);
            var read = Expression.Lambda<Func<T, object?>>(Expression.Convert(lambda.Body, typeof(object)), lambda.Parameters).Compile();
            return new Key
            {
                Lambda = lambda,
                Type = type,
                Keyset = keyset && Nullable.GetUnderlyingType(type) is null && (type.IsEnum || KeysetTypes.Contains(type)),
                Read = row => read(row),
            };
        }

        /// <summary>The key's body over another parameter.</summary>
        public Expression Body(ParameterExpression parameter) => new Rebind(Lambda.Parameters[0], parameter).Visit(Lambda.Body);

        public string? Text(object? value) => value switch
        {
            null => null,
            string s => s,
            DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString("O", CultureInfo.InvariantCulture),
            Enum e => Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };

        /// <summary>The cursor's text as a value of the key's type, or null when it doesn't parse (a tampered cursor).</summary>
        public object? Parse(string text)
        {
            var c = CultureInfo.InvariantCulture;
            try
            {
                return Type switch
                {
                    _ when Type == typeof(string) => text,
                    _ when Type.IsEnum => Enum.ToObject(Type, long.Parse(text, c)),
                    _ when Type == typeof(DateTime) => DateTime.Parse(text, c, DateTimeStyles.RoundtripKind),
                    _ when Type == typeof(DateTimeOffset) => DateTimeOffset.Parse(text, c, DateTimeStyles.RoundtripKind),
                    _ when Type == typeof(DateOnly) => DateOnly.Parse(text, c),
                    _ when Type == typeof(TimeOnly) => TimeOnly.Parse(text, c),
                    _ => Convert.ChangeType(text, Type, c),
                };
            }
            catch (Exception e) when (e is FormatException or OverflowException or InvalidCastException or ArgumentException)
            {
                return null;
            }
        }
    }

    private sealed class Rebind(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
