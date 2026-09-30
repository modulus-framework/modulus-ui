using System.Net;
using System.Text;
using System.Text.Json;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.Ui.Abstractions.Plugins;

/// <summary>A caption shown over the media from <paramref name="Start"/> to <paramref name="End"/> seconds.</summary>
public sealed record MediaCaption(double Start, double End, string Text);

/// <summary>What <see cref="MediaPlayerHtml.Render"/> draws.</summary>
public sealed record MediaPlayerOptions
{
    /// <summary>The media URL.</summary>
    public required string Src { get; init; }

    /// <summary>True for a &lt;video&gt;, false for an &lt;audio&gt; bar.</summary>
    public bool Video { get; init; } = true;

    /// <summary>Picture shown before the video plays; for audio, the cover beside the title.</summary>
    public string? Poster { get; init; }

    /// <summary>Title of an audio track (and the accessible name of the player).</summary>
    public string? Title { get; init; }

    /// <summary>Second line of an audio track (artist, album).</summary>
    public string? Subtitle { get; init; }

    /// <summary>Captions, turned on with the CC button (or <c>c</c>).</summary>
    public IReadOnlyList<MediaCaption> Captions { get; init; } = [];

    /// <summary>Captions start switched on.</summary>
    public bool CaptionsOn { get; init; }

    public bool Loop { get; init; }

    /// <summary>Width / height of the video box, e.g. "16 / 9".</summary>
    public string AspectRatio { get; init; } = "16 / 9";

    /// <summary>Extra classes on the player.</summary>
    public string? CssClass { get; init; }
}

/// <summary>
/// Markup of the inline media player (<c>AppMediaPlayer</c> in Blazor, <c>&lt;app-media-player&gt;</c> in MVC): a native
/// &lt;video&gt; or &lt;audio&gt; with custom controls (Ant Design icons). modulus-plugins.js ([data-m-player]) runs it, so both
/// flavours share one engine and Blazor needs no interop.
/// </summary>
public static class MediaPlayerHtml
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string Ai(string name) => AntIconSvg.Svg(name) ?? "";

    private static string AiFill(string name) => AntIconSvg.Svg(name, AntIconSvg.Fill) ?? "";

    private static string Button(string cmd, string label, string icons, string? extra = null) =>
        $"""<button type="button" class="m-media-btn" data-m-player-cmd="{cmd}" aria-label="{E(label)}" title="{E(label)}"{extra}>{icons}</button>""";

    public static string Render(MediaPlayerOptions o)
    {
        var sb = new StringBuilder();
        var captions = o.Captions.Count > 0;
        var cls = "m-media " + (o.Video ? "m-media-video" : "m-media-audio") + (captions && o.CaptionsOn ? " m-media-captions-on" : "") + (o.CssClass is { Length: > 0 } c ? " " + c : "");
        sb.Append($"""<div class="{E(cls)}" data-m-player role="group" aria-label="{E(o.Title ?? (o.Video ? "Video player" : "Audio player"))}" tabindex="-1" """)
          .Append(captions ? $"""data-captions="{E(JsonSerializer.Serialize(o.Captions, Json))}" """ : "")
          .Append(o.Video ? $"""style="--m-media-ratio: {E(o.AspectRatio)}">""" : ">");

        if (o.Video)
        {
            sb.Append($"""<div class="m-media-stage"><video class="m-media-media" src="{E(o.Src)}" preload="metadata" playsinline{(o.Poster is null ? "" : $" poster=\"{E(o.Poster)}\"")}{(o.Loop ? " loop" : "")}></video>""")
              .Append("""<div class="m-media-caption" data-m-player-caption hidden></div>""")
              .Append($"""<button type="button" class="m-media-big" data-m-player-cmd="play" aria-label="Play" tabindex="-1">{AiFill("play-circle")}</button>""")
              .Append($"""<div class="m-media-spinner" aria-hidden="true">{Ai("loading")}</div>""")
              .Append($"""<div class="m-media-fail" role="alert">{Ai("warning")}<span>This video can't be played.</span></div></div>""");
        }
        else
        {
            sb.Append("""<div class="m-media-track">""");
            if (o.Poster is not null) sb.Append($"""<img class="m-media-cover" src="{E(o.Poster)}" alt="" />""");
            else sb.Append($"""<span class="m-media-cover m-media-cover-icon" aria-hidden="true">{Ai("customer-service")}</span>""");
            sb.Append($"""<div class="m-media-track-text"><div class="m-media-title">{E(o.Title)}</div>""")
              .Append(o.Subtitle is null ? "" : $"""<div class="m-media-subtitle">{E(o.Subtitle)}</div>""")
              .Append("</div>")
              .Append($"""<div class="m-media-fail" role="alert">{Ai("warning")}<span>This track can't be played.</span></div></div>""")
              .Append($"""<audio class="m-media-media" src="{E(o.Src)}" preload="metadata"{(o.Loop ? " loop" : "")}></audio>""")
              .Append("""<div class="m-media-caption" data-m-player-caption hidden></div>""");
        }

        sb.Append("""<div class="m-media-bar">""")
          .Append(Button("play", "Play (k)", $"""<span class="m-media-show-paused">{AiFill("caret-right")}</span><span class="m-media-show-playing">{Ai("pause")}</span>"""))
          .Append(o.Video ? "" : Button("back", "Back 10 seconds (j)", Ai("undo")))
          .Append(o.Video ? "" : Button("forward", "Forward 10 seconds (l)", Ai("redo")))
          .Append("""<span class="m-media-time"><span data-m-player-time>0:00</span><span class="m-media-time-sep">/</span><span data-m-player-duration>0:00</span></span>""")
          .Append("""<span class="m-media-seek-wrap"><span class="m-media-buffered" data-m-player-buffered></span>""")
          .Append("""<input type="range" class="m-media-range m-media-seek" data-m-player-seek min="0" max="1000" step="1" value="0" aria-label="Seek" /></span>""")
          .Append(Button("mute", "Mute (m)", $"""<span class="m-media-show-sound">{Ai("sound")}</span><span class="m-media-show-muted">{Ai("muted")}</span>"""))
          .Append("""<input type="range" class="m-media-range m-media-volume" data-m-player-volume min="0" max="1" step="0.05" value="1" aria-label="Volume" />""")
          .Append("""<button type="button" class="m-media-btn m-media-speed" data-m-player-cmd="speed" aria-label="Playback speed" title="Playback speed">1×</button>""");
        if (captions)
            sb.Append(Button("captions", "Captions (c)", """<span class="m-media-cc">CC</span>""", $""" aria-pressed="{(o.CaptionsOn ? "true" : "false")}" """));
        if (o.Video)
            sb.Append(Button("fullscreen", "Full screen (f)", $"""<span class="m-media-show-window">{Ai("fullscreen")}</span><span class="m-media-show-full">{Ai("fullscreen-exit")}</span>"""));
        sb.Append("</div></div>");
        return sb.ToString();
    }
}
