using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
/// <summary>
/// An option of a Select, MultiSelect or Tag column. <paramref name="Color"/> is an Ant Design tag colour;
/// <paramref name="Icon"/> an Ant Design icon shown before the label in the cell ("arrow-up", "fire").
/// </summary>
public sealed record GridOption(string Value, string Label, string? Color = null, string? Icon = null);

