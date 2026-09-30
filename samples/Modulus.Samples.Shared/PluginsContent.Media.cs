using System.Globalization;
using Modulus.Ui.Abstractions.Forms;
using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.Samples.Shared;

/// <summary>Content of the second Plugins batch: inline player, WYSIWYG editor, sortable lists and maps.</summary>
public static partial class PluginsContent
{
    // Inline player -------------------------------------------------------------------------------------------

    /// <summary>A short CC0 clip from MDN's examples (needs the internet; the player shows its error state offline).</summary>
    public const string VideoSrc = "https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4";

    public static string VideoPoster => ExtraContent.Photos[6].Src;

    public static readonly IReadOnlyList<MediaCaption> VideoCaptions =
    [
        new(0, 2.5, "Captions come from time-coded cues."),
        new(2.5, 6, "Turn them on and off with CC or the C key."),
    ];

    /// <summary>The audio track: made on the server (<see cref="DemoTrack"/>), so the sample needs no media files.</summary>
    public const string TrackUrl = "/plugins/player/track.wav";

    public static string TrackCover => ExtraContent.Photos[8].Src;

    private static readonly (string Name, double[] Notes)[] Chords =
    [
        ("C major", [261.63, 329.63, 392.00, 523.25]),
        ("A minor", [220.00, 261.63, 329.63, 440.00]),
        ("F major", [174.61, 220.00, 261.63, 349.23]),
        ("G major", [196.00, 246.94, 293.66, 392.00]),
    ];

    private const double ChordSeconds = 4;

    /// <summary>One caption per chord, in step with the track.</summary>
    public static IReadOnlyList<MediaCaption> TrackCaptions { get; } =
        [.. Chords.Select((c, i) => new MediaCaption(i * ChordSeconds, (i + 1) * ChordSeconds, $"Chord {i + 1} of {Chords.Length}: {c.Name}"))];

    private static readonly Lazy<byte[]> Track = new(BuildTrack);

    /// <summary>A 16-second arpeggio over C, Am, F and G as a 16-bit mono WAV (22.05 kHz, about 700 KB), built once.</summary>
    public static byte[] DemoTrack() => Track.Value;

    private static byte[] BuildTrack()
    {
        const int rate = 22050;
        var total = (int)(rate * ChordSeconds * Chords.Length);
        var samples = new short[total];
        const double step = 0.25; // an eighth note at 120 bpm
        for (var i = 0; i < total; i++)
        {
            var t = (double)i / rate;
            var chord = Chords[Math.Min(Chords.Length - 1, (int)(t / ChordSeconds))].Notes;
            var inChord = t % ChordSeconds;
            // Arpeggio: up and down the chord, each note with a quick attack and an exponential fade.
            var n = (int)(inChord / step);
            var note = chord[n % 8 < 4 ? n % 4 : 3 - n % 4];
            var local = inChord % step;
            var pluck = Math.Sin(2 * Math.PI * note * t) * Math.Min(1, local * 200) * Math.Exp(-local * 9);
            // A soft pad of the chord's root and fifth under it, faded at the chord edges.
            var edge = Math.Min(1, Math.Min(inChord, ChordSeconds - inChord) * 4);
            var pad = (Math.Sin(2 * Math.PI * chord[0] / 2 * t) + 0.5 * Math.Sin(2 * Math.PI * chord[2] / 2 * t)) * 0.18 * edge;
            var fade = Math.Min(1, Math.Min(t, total / (double)rate - t) * 2);
            samples[i] = (short)Math.Round(Math.Clamp((pluck * 0.45 + pad) * fade, -1, 1) * 30000);
        }
        using var ms = new MemoryStream(44 + total * 2);
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8);
        w.Write(36 + total * 2);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)1);      // PCM
        w.Write((short)1);      // mono
        w.Write(rate);
        w.Write(rate * 2);      // bytes per second
        w.Write((short)2);      // block align
        w.Write((short)16);     // bits per sample
        w.Write("data"u8);
        w.Write(total * 2);
        foreach (var s in samples) w.Write(s);
        w.Flush();
        return ms.ToArray();
    }

    // WYSIWYG editor ------------------------------------------------------------------------------------------

    public const string EditorSample =
        "<h2>Quarterly update</h2>" +
        "<p>Revenue grew <strong>18%</strong> on the same quarter last year, led by the <em>online store</em> and two new retail partners.</p>" +
        "<ul><li>Online orders up 24%</li><li>Average basket $86, up from $79</li><li>Returns down to 3.1%</li></ul>" +
        "<blockquote>We will open the Berlin warehouse in November.</blockquote>" +
        "<p>Read the <a href=\"https://example.com/report\">full report</a> for the details by region.</p>";

    /// <summary>What a posted editor value becomes before it is stored: cleaned, with its plain text and word count.</summary>
    public static EditorResult Clean(string? html)
    {
        var clean = RichText.Sanitize(html, images: true);
        var text = RichText.ToPlainText(clean);
        return new EditorResult(clean, text.Length == 0 ? 0 : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, text.Length);
    }

    // Sortable ------------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<SortTask> Tasks =
    [
        new("t1", "Write the launch brief", "Marketing", "blue"),
        new("t2", "Price the spring range", "Sales", "green"),
        new("t3", "Photograph new stock", "Studio", "purple"),
        new("t4", "Update the returns policy", "Support", "orange"),
        new("t5", "Book the trade fair stand", "Events", "cyan"),
        new("t6", "Review supplier contracts", "Legal", "red"),
        new("t7", "Plan the warehouse move", "Operations", "gold"),
    ];

    /// <summary>The board's lists: (name, title) in order.</summary>
    public static readonly IReadOnlyList<(string Name, string Title)> BoardLists = [("todo", "To do"), ("doing", "In progress"), ("done", "Done")];

    private static readonly Dictionary<string, string[]> BoardStart = new()
    {
        ["todo"] = ["t1", "t2", "t3"],
        ["doing"] = ["t4", "t5"],
        ["done"] = ["t6", "t7"],
    };

    /// <summary>
    /// The board as posted (<c>?todo=t2&amp;todo=t1&amp;doing=…</c>), or as it starts. Unknown and repeated ids are dropped and
    /// any task missing from the post goes back to its starting list, so every task shows exactly once.
    /// </summary>
    public static Dictionary<string, List<SortTask>> Board(Func<string, IEnumerable<string?>>? posted = null)
    {
        var byId = Tasks.ToDictionary(t => t.Id);
        var seen = new HashSet<string>();
        var board = BoardLists.ToDictionary(l => l.Name, _ => new List<SortTask>());
        if (posted is not null)
            foreach (var (name, _) in BoardLists)
                foreach (var id in posted(name))
                    if (id is not null && byId.TryGetValue(id, out var task) && seen.Add(id)) board[name].Add(task);
        foreach (var (name, ids) in BoardStart)
            foreach (var id in ids)
                if (seen.Add(id)) board[name].Add(byId[id]);
        return board;
    }

    /// <summary>The steps of a checklist that is reordered on its own.</summary>
    public static readonly IReadOnlyList<SortTask> Checklist =
    [
        new("c1", "Freeze the release branch", "Day 1", "blue"),
        new("c2", "Run the full test suite", "Day 1", "blue"),
        new("c3", "Write the release notes", "Day 2", "purple"),
        new("c4", "Deploy to staging", "Day 2", "green"),
        new("c5", "Announce the release", "Day 3", "orange"),
    ];

    /// <summary>Tiles of the grid layout demo: (id, title, icon).</summary>
    public static readonly IReadOnlyList<SortTask> Tiles =
    [
        new("dashboard", "Dashboard", "dashboard", "blue"),
        new("orders", "Orders", "shopping-cart", "green"),
        new("customers", "Customers", "team", "purple"),
        new("reports", "Reports", "bar-chart", "orange"),
        new("inventory", "Inventory", "inbox", "cyan"),
        new("settings", "Settings", "setting", "gray"),
    ];

    // Maps ----------------------------------------------------------------------------------------------------

    public static readonly IReadOnlyList<MapMarker> Offices =
    [
        new(51.5074, -0.1278, "London", "Head office · 240 people", "primary"),
        new(52.5200, 13.4050, "Berlin", "Warehouse opening in November", "warning"),
        new(40.7128, -74.0060, "New York", "Sales and support · 85 people", "primary"),
        new(-23.5505, -46.6333, "São Paulo", "Partner office", "purple"),
        new(-1.2921, 36.8219, "Nairobi", "Engineering hub · 40 people", "success"),
        new(1.3521, 103.8198, "Singapore", "Asia-Pacific office · 60 people", "primary"),
        new(35.6762, 139.6503, "Tokyo", "Partner office", "purple"),
        new(-33.8688, 151.2093, "Sydney", "Sales · 25 people", "success"),
    ];

    /// <summary>Stores in London for the zoomed-in map.</summary>
    public static readonly IReadOnlyList<MapMarker> Stores =
    [
        new(51.5145, -0.1444, "Oxford Street", "Open 9:00–21:00", "success"),
        new(51.5033, -0.1195, "South Bank", "Open 10:00–20:00", "success"),
        new(51.5226, -0.0780, "Shoreditch", "Open 10:00–19:00", "success"),
        new(51.4613, -0.1156, "Brixton", "Closed for refit until Monday", "error"),
        new(51.5413, -0.1433, "Camden", "Open 10:00–18:00", "success"),
    ];

    /// <summary>Sales by country in thousands of US dollars (ISO 3166-1 alpha-2 codes).</summary>
    public static readonly IReadOnlyDictionary<string, double> SalesByCountry = new Dictionary<string, double>
    {
        ["US"] = 1240, ["CA"] = 310, ["MX"] = 140, ["BR"] = 260, ["AR"] = 90, ["CL"] = 70, ["CO"] = 65,
        ["GB"] = 520, ["IE"] = 85, ["DE"] = 610, ["FR"] = 430, ["ES"] = 210, ["PT"] = 60, ["IT"] = 240, ["NL"] = 180,
        ["BE"] = 95, ["CH"] = 150, ["AT"] = 80, ["SE"] = 120, ["NO"] = 100, ["DK"] = 75, ["FI"] = 55, ["PL"] = 110,
        ["CZ"] = 45, ["TR"] = 95, ["SA"] = 130, ["AE"] = 150, ["EG"] = 55, ["NG"] = 45, ["KE"] = 40, ["ZA"] = 85,
        ["IN"] = 380, ["CN"] = 720, ["JP"] = 450, ["KR"] = 160, ["ID"] = 100, ["SG"] = 140, ["MY"] = 70, ["TH"] = 65,
        ["VN"] = 50, ["PH"] = 45, ["AU"] = 290, ["NZ"] = 60,
    };

    /// <summary>The sales choropleth: six blue classes, labelled "$310k" / "$1.2M".</summary>
    public static Choropleth SalesMap { get; } = Choropleth.Build(SalesByCountry, ["#bae0ff", "#91caff", "#69b1ff", "#4096ff", "#1677ff", "#0958d9"], Money);

    public static double SalesTotal => SalesByCountry.Values.Sum();

    public static IReadOnlyList<KeyValuePair<string, double>> TopMarkets(int count) =>
        [.. SalesByCountry.OrderByDescending(kv => kv.Value).Take(count)];

    /// <summary>English name of a country code ("DE" → "Germany").</summary>
    public static string CountryName(string code)
    {
        try { return new RegionInfo(code).EnglishName; }
        catch (ArgumentException) { return code; }
    }

    /// <summary>Thousands of dollars as "$310k" or "$1.2M".</summary>
    public static string Money(double thousands) => thousands >= 1000
        ? "$" + (thousands / 1000).ToString("0.#", CultureInfo.InvariantCulture) + "M"
        : "$" + Math.Round(thousands).ToString("0", CultureInfo.InvariantCulture) + "k";
}

/// <summary>A card on the sortable demos: <paramref name="Meta"/> is a team, day or icon; <paramref name="Tone"/> an Ant Design tag colour.</summary>
public sealed record SortTask(string Id, string Title, string Meta, string Tone);

public sealed record EditorResult(string Html, int Words, int Characters);
