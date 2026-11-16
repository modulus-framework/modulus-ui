using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Pivot;

public sealed partial class PivotQuery<T>
{
    // Expressions ----------------------------------------------------------------------------------------------

    private static readonly MethodInfo CountMethod = new Func<IEnumerable<T>, int>(Enumerable.Count).Method;
    private static readonly MethodInfo SumMethod = new Func<IEnumerable<T>, Func<T, decimal>, decimal>(Enumerable.Sum).Method;
    private static readonly MethodInfo MinMethod = new Func<IEnumerable<T>, Func<T, decimal>, decimal>(Enumerable.Min).Method;
    private static readonly MethodInfo MaxMethod = new Func<IEnumerable<T>, Func<T, decimal>, decimal>(Enumerable.Max).Method;

    private static Expression Rebind(LambdaExpression lambda, ParameterExpression to) =>
        new Replace(lambda.Parameters[0], to).Visit(lambda.Body);

    private sealed class Replace(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
