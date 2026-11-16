using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Pivot;

/// <summary>How a measure combines the values of a group (Odoo's <c>aggregator</c>); the record count is the <c>count</c> measure.</summary>
public enum PivotAggregate
{
    Sum,
    Average,
    Min,
    Max,
}
