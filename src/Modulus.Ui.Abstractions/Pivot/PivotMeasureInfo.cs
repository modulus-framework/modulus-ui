using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Pivot;

/// <summary>A measure the pivot can show.</summary>
/// <param name="Percent">A ratio shown as a percentage (the value is the fraction, shown ×100 with a % sign).</param>
public sealed record PivotMeasureInfo(string Key, string Title, int Decimals, bool Percent = false);
