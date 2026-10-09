using System.Text.Json;
using System.Text.RegularExpressions;
using Modulus.Ui.Abstractions.State;

namespace Modulus.Ui.Abstractions.Dashboard;

/// <summary>A tile a dashboard can show: its id, its title, and its size when nobody has changed it (columns of 12 and rows).</summary>
public sealed record DashboardTileSpec(string Id, string Title, int Width = 4, int Height = 2);

/// <summary>A tile's place in the layout: its id and its size. The order of the list is the order on the page.</summary>
public sealed record DashboardPlacement(string Id, int Width, int Height);

/// <summary>A tile ready to draw: its id, title and size, and whether it is first or last (the move buttons are off there).</summary>
public sealed record DashboardTileView(string Id, string Title, int Width, int Height, bool First, bool Last);

/// <summary>A dashboard ready to draw: its id and its tiles in order.</summary>
public sealed record DashboardView(string Id, IReadOnlyList<DashboardTileView> Tiles);

/// <summary>
/// The dashboard layout's model, shared by the Blazor <c>DashboardGrid</c> and the MVC <c>app-dashboard</c>: the tiles in order,
/// each with a size between 1 and 12 columns and 1 and <see cref="MaxHeight"/> rows; a stored layout is checked on the way in
/// (unknown tiles are dropped, new tiles are added at their default size); and the moves and sizes a button asks for.
/// The layout is kept as JSON in the <see cref="IUiStateStore"/> under <c>dashboard:{id}</c>.
/// </summary>
public static class DashboardModel
{
    /// <summary>Columns of the grid a tile spans.</summary>
    public const int Columns = 12;

    /// <summary>The most rows a tile may span.</summary>
    public const int MaxHeight = 6;

    /// <summary>The most tiles one dashboard keeps.</summary>
    public const int MaxTiles = 50;

    private static readonly Regex IdPattern = new("^[A-Za-z0-9_-]{1,40}$", RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The store key of a dashboard's layout.</summary>
    public static string Key(string dashboardId) => "dashboard:" + dashboardId;

    /// <summary>Whether an id names a tile or a dashboard (letters, digits, <c>-</c> and <c>_</c>, up to 40 characters).</summary>
    public static bool IsValidId(string? id) => id is not null && IdPattern.IsMatch(id);

    /// <summary>The layout every tile gets when nobody has changed it: the specs in their order, at their default sizes.</summary>
    public static IReadOnlyList<DashboardPlacement> Default(IEnumerable<DashboardTileSpec> specs) =>
        specs.Select(s => new DashboardPlacement(s.Id, Clamp(s.Width, 1, Columns), Clamp(s.Height, 1, MaxHeight))).ToList();

    /// <summary>
    /// The layout to draw: the saved order and sizes for the tiles that still exist (sizes clamped to the grid), then any tile
    /// the save does not name, at its default size. Tiles the specs do not list are dropped.
    /// </summary>
    public static IReadOnlyList<DashboardPlacement> Resolve(IEnumerable<DashboardTileSpec> specs, IEnumerable<DashboardPlacement>? saved)
    {
        var known = specs.ToList();
        var byId = known.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var result = new List<DashboardPlacement>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in saved ?? [])
        {
            if (!byId.ContainsKey(p.Id) || !seen.Add(p.Id)) continue;
            result.Add(new DashboardPlacement(p.Id, Clamp(p.Width, 1, Columns), Clamp(p.Height, 1, MaxHeight)));
        }
        foreach (var s in known.Where(s => !seen.Contains(s.Id)))
            result.Add(new DashboardPlacement(s.Id, Clamp(s.Width, 1, Columns), Clamp(s.Height, 1, MaxHeight)));
        return result.Take(MaxTiles).ToList();
    }

    /// <summary>
    /// Applies one button's change to a tile: <c>earlier</c> and <c>later</c> swap it with its neighbour, <c>wider</c> and
    /// <c>narrower</c> change its columns by one, <c>taller</c> and <c>shorter</c> its rows by one, each within the grid. Any
    /// other op, or a tile that is not in the layout, leaves the layout as it is.
    /// </summary>
    public static IReadOnlyList<DashboardPlacement> Apply(IReadOnlyList<DashboardPlacement> layout, string op, string tile)
    {
        var list = layout.ToList();
        var index = list.FindIndex(p => p.Id == tile);
        if (index < 0) return list;
        var p = list[index];
        switch (op)
        {
            case "earlier" when index > 0:
                (list[index - 1], list[index]) = (list[index], list[index - 1]);
                break;
            case "later" when index < list.Count - 1:
                (list[index + 1], list[index]) = (list[index], list[index + 1]);
                break;
            case "wider" when p.Width < Columns:
                list[index] = p with { Width = p.Width + 1 };
                break;
            case "narrower" when p.Width > 1:
                list[index] = p with { Width = p.Width - 1 };
                break;
            case "taller" when p.Height < MaxHeight:
                list[index] = p with { Height = p.Height + 1 };
                break;
            case "shorter" when p.Height > 1:
                list[index] = p with { Height = p.Height - 1 };
                break;
        }
        return list;
    }

    /// <summary>The dashboard with its tiles in order, each with its title (from the specs) and its position at the ends.</summary>
    public static DashboardView View(string dashboardId, IEnumerable<DashboardTileSpec> specs, IReadOnlyList<DashboardPlacement> layout)
    {
        var titles = specs.ToDictionary(s => s.Id, s => s.Title, StringComparer.Ordinal);
        var tiles = layout.Select((p, i) => new DashboardTileView(p.Id, titles.GetValueOrDefault(p.Id, p.Id), p.Width, p.Height, i == 0, i == layout.Count - 1)).ToList();
        return new DashboardView(dashboardId, tiles);
    }

    /// <summary>The layout as JSON: <c>{"tiles":[{"id":"…","width":4,"height":2},…]}</c>.</summary>
    public static string ToJson(IEnumerable<DashboardPlacement> layout) =>
        JsonSerializer.Serialize(new { tiles = layout.Select(p => new { id = p.Id, width = p.Width, height = p.Height }) }, Json);

    /// <summary>
    /// A layout read from JSON, or null when the text is not one. Entries with a bad id or size are dropped, and at most
    /// <see cref="MaxTiles"/> are kept; sizes are clamped to the grid.
    /// </summary>
    public static IReadOnlyList<DashboardPlacement>? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("tiles", out var tiles) || tiles.ValueKind != JsonValueKind.Array) return null;
            var result = new List<DashboardPlacement>();
            foreach (var t in tiles.EnumerateArray())
            {
                if (result.Count >= MaxTiles) break;
                if (t.ValueKind != JsonValueKind.Object) continue;
                if (!t.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || !IsValidId(id.GetString())) continue;
                if (!t.TryGetProperty("width", out var w) || !IsInt(w, out var width)) continue;
                if (!t.TryGetProperty("height", out var h) || !IsInt(h, out var height)) continue;
                result.Add(new DashboardPlacement(id.GetString()!, Clamp(width, 1, Columns), Clamp(height, 1, MaxHeight)));
            }
            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The layout a user saved, resolved against the specs; the default layout when there is none or the user is anonymous.</summary>
    public static async Task<IReadOnlyList<DashboardPlacement>> LoadAsync(IUiStateStore store, string user, string dashboardId, IEnumerable<DashboardTileSpec> specs, CancellationToken cancellationToken = default)
    {
        var spec = specs.ToList();
        var saved = user.Length == 0 ? null : ParseJson(await store.GetAsync(user, Key(dashboardId), cancellationToken));
        return Resolve(spec, saved);
    }

    /// <summary>Saves a user's layout. An anonymous user keeps nothing, so this returns false for them.</summary>
    public static async Task<bool> SaveAsync(IUiStateStore store, string user, string dashboardId, IEnumerable<DashboardPlacement> layout, CancellationToken cancellationToken = default)
    {
        if (user.Length == 0) return false;
        await store.SetAsync(user, Key(dashboardId), ToJson(layout), cancellationToken);
        return true;
    }

    /// <summary>Forgets a user's layout, so the default comes back on the next visit (the reset button).</summary>
    public static async Task ClearAsync(IUiStateStore store, string user, string dashboardId, CancellationToken cancellationToken = default)
    {
        if (user.Length > 0) await store.SetAsync(user, Key(dashboardId), null, cancellationToken);
    }

    private static bool IsInt(JsonElement element, out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }

    private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
}
