using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
/// <summary>The editor and display format of a <see cref="GridColumn"/>.</summary>
public enum GridCellType
{
    Text,
    Number,
    Currency,
    Percent,
    Select,
    MultiSelect,
    Date,
    Switch,
    Tag,
    Progress,
    Rating,
}

