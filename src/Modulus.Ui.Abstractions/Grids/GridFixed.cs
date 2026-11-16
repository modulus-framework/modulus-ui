using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
/// <summary>Where a column is pinned while the table scrolls horizontally (Ant Design's <c>fixed</c>).</summary>
public enum GridFixed
{
    None,
    Left,
    Right,
}

