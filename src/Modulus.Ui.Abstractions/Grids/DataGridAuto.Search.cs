using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Modulus.Ui.Abstractions.Data;

namespace Modulus.Ui.Abstractions;


public static partial class DataGridAuto
{
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
