using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using Modulus.Ui.Abstractions.Query;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.Ui.Abstractions.Data;

/// <summary>Whether a page also counts every matching row.</summary>
public enum CountMode
{
    /// <summary>Count the rows (a <c>COUNT(*)</c>): the pager shows "1–50 of 100,000".</summary>
    Exact,

    /// <summary>
    /// Don't count: one row more than the page is read to know whether there is a next page. Use it for tables too large
    /// to count on every request, and for infinite and virtual scrolling after the first page.
    /// </summary>
    None,
}
