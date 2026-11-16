using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using Modulus.Ui.Abstractions.Query;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.Ui.Abstractions.Data;

/// <summary>
/// A value as a member of a closure object, which LINQ providers send as a query parameter (not a literal in the SQL
/// text), so every value shares one cached plan.
/// </summary>
internal static class Box
{
    public static Expression Of(object? value, Type type)
    {
        var holder = Activator.CreateInstance(typeof(Holder<>).MakeGenericType(type), value)!;
        return Expression.Field(Expression.Constant(holder), holder.GetType().GetField(nameof(Holder<int>.Value))!);
    }

    private sealed class Holder<TValue>(TValue value)
    {
        public readonly TValue Value = value;
    }
}
