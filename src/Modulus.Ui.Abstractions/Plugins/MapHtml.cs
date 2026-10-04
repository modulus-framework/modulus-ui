using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Modulus.Ui.Abstractions.Plugins;

/// <summary>A marker on a tile map. <paramref name="Tone"/>: primary, success, warning, error or purple.</summary>
public sealed record MapMarker(double Lat, double Lng, string Title, string? Text = null, string Tone = "primary");

/// <summary>A class of a choropleth legend: regions whose value is at least <paramref name="From"/> get <paramref name="Color"/>.</summary>
public sealed record MapClass(double From, string Color, string Label);

/// <summary>A choropleth: a value per region code (ISO 3166-1 alpha-2 for the world map), bucketed into classes.</summary>
public sealed record Choropleth(IReadOnlyDictionary<string, double> Values, IReadOnlyList<MapClass> Classes, Func<double, string> Format)
{
    /// <summary>The class of a value: the last one whose <see cref="MapClass.From"/> it reaches.</summary>
    public MapClass ClassOf(double value) => Classes.LastOrDefault(c => value >= c.From) ?? Classes[0];

    /// <summary>
    /// Classes with equal steps between the smallest and largest value, coloured from <paramref name="colors"/> (light to dark),
    /// labelled with <paramref name="format"/>.
    /// </summary>
    public static Choropleth Build(IReadOnlyDictionary<string, double> values, IReadOnlyList<string> colors, Func<double, string> format)
    {
        if (colors.Count == 0) throw new ArgumentException("At least one colour is needed.", nameof(colors));
        var (min, max) = values.Count == 0 ? (0d, 0d) : (values.Values.Min(), values.Values.Max());
        var step = colors.Count == 1 || max <= min ? 0 : (max - min) / colors.Count;
        var classes = colors.Select((c, i) =>
        {
            var from = min + step * i;
            var to = i == colors.Count - 1 ? max : min + step * (i + 1);
            return new MapClass(i == 0 ? double.MinValue : from, c, i == colors.Count - 1 ? $"{format(from)}+" : $"{format(from)}–{format(to)}");
        }).ToList();
        return new Choropleth(values, classes, format);
    }
}

/// <summary>
/// Markup of the maps (<c>AppMap</c> / <c>AppVectorMap</c> in Blazor, <c>&lt;app-map&gt;</c> / <c>&lt;app-vector-map&gt;</c> in MVC).
/// modulus-plugins.js starts MapLibre GL JS on <c>[data-m-map]</c> and jsVectorMap on <c>[data-m-vector-map]</c> when they appear.
/// </summary>
public static class MapHtml
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>A tile map. Without <paramref name="center"/> it fits the markers.</summary>
    /// <param name="id">Element id; <c>[data-m-map-go]</c> buttons name it in <c>data-m-map-target</c>.</param>
    /// <param name="height">CSS height, e.g. "360px" or "100%".</param>
    public static string Tile(string id, IReadOnlyList<MapMarker> markers, (double Lat, double Lng)? center = null, int zoom = 4,
        bool scrollZoom = false, string height = "360px", string? label = null, string? cssClass = null)
    {
        var sb = new StringBuilder($"""<div class="m-map{(cssClass is { Length: > 0 } c ? " " + E(c) : "")}" id="{E(id)}" data-m-map role="region" aria-label="{E(label ?? "Map")}" """);
        sb.Append($"""data-markers="{E(JsonSerializer.Serialize(markers, Json))}" data-zoom="{zoom.ToString(Inv)}" """);
        if (center is { } ctr) sb.Append($"""data-center="{ctr.Lat.ToString(Inv)},{ctr.Lng.ToString(Inv)}" """);
        if (scrollZoom) sb.Append("data-scroll-zoom ");
        sb.Append($"""style="height: {E(height)}"><div class="m-map-loading">Loading map…</div></div>""");
        return sb.ToString();
    }

    /// <summary>A button that flies the map to marker <paramref name="index"/> and opens its popup.</summary>
    public static string GoAttributes(string mapId, int index, int zoom = 12) =>
        $""" data-m-map-go="{index.ToString(Inv)}" data-m-map-target="{E(mapId)}" data-zoom="{zoom.ToString(Inv)}" """;

    /// <summary>A world choropleth with its legend; hovering a country shows its value, clicking selects it.</summary>
    public static string Vector(string id, Choropleth data, string? label = null, string height = "420px", string map = "world")
    {
        var fills = data.Values.ToDictionary(kv => kv.Key, kv => data.ClassOf(kv.Value).Color);
        var labels = data.Values.ToDictionary(kv => kv.Key, kv => data.Format(kv.Value));
        var sb = new StringBuilder("""<div class="m-vector-map-wrap">""");
        sb.Append($"""<div class="m-vector-map" id="{E(id)}" data-m-vector-map data-map="{E(map)}" role="img" aria-label="{E(label ?? "World map")}" """)
          .Append($"""data-fills="{E(JsonSerializer.Serialize(fills, Json))}" data-labels="{E(JsonSerializer.Serialize(labels, Json))}" style="height: {E(height)}"></div>""");
        sb.Append("""<div class="m-map-legend" aria-label="Legend">""");
        foreach (var c in data.Classes)
            sb.Append($"""<span class="m-map-legend-item"><span class="m-map-legend-swatch" style="background: {E(c.Color)}"></span>{E(c.Label)}</span>""");
        sb.Append("""<span class="m-map-legend-item"><span class="m-map-legend-swatch m-map-legend-none"></span>No data</span></div>""");
        sb.Append("""<p class="m-map-selected" data-m-vector-selected data-empty="Click a country to see its value." aria-live="polite">Click a country to see its value.</p></div>""");
        return sb.ToString();
    }
}
