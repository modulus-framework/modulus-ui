using System.Net;
using System.Text;
using System.Text.Json;

namespace Modulus.Ui.Abstractions.Plugins;

/// <summary>A crop in the picture's natural pixels, from its top left corner.</summary>
public readonly record struct CropBox(int X, int Y, int Width, int Height);

/// <summary>
/// The image cropper's markup, shared by the Blazor <c>AppImageCropper</c> and the MVC <c>app-image-cropper</c>. Cropper.js 2
/// (modulus-plugins.js) draws the crop box over the picture; the aspect ratio buttons set its shape. The crop goes to the
/// hidden input as JSON <c>{ x, y, width, height }</c> in the picture's natural pixels, with a change event, so a form posts it
/// and Blazor binds it. Blazor renders the root and the hidden input itself (so its change event binds) and uses
/// <see cref="Parts"/> for the rest.
/// </summary>
public static class CropperHtml
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The shapes the buttons offer, by the key the markup uses (<c>free</c> is no fixed shape).</summary>
    public static IReadOnlyList<(string Key, string Label)> Ratios { get; } =
    [
        ("free", "Free"),
        ("1", "1:1"),
        ("4/3", "4:3"),
        ("16/9", "16:9"),
    ];

    /// <summary>The root element's opening tag; <paramref name="height"/> is the picture area's CSS height.</summary>
    public static string RootAttributes(string id, string? label, string ratio, string height, string? cssClass = null)
    {
        CheckRatio(ratio);
        return $"""<div class="m-cropper{(cssClass is { Length: > 0 } c ? " " + E(c) : "")}" id="{E(id)}" data-m-cropper data-ratio="{E(ratio)}" role="group" aria-label="{E(label ?? "Crop the picture")}" style="--m-cropper-height: {E(height)}">""";
    }

    /// <summary>The whole cropper for MVC: the root, the parts and a hidden input named <paramref name="name"/> that holds the crop.</summary>
    public static string Editor(string id, string src, string alt, string name, string? label = null, string ratio = "free", string height = "360px",
        string? cssClass = null, CropBox? crop = null)
    {
        var hidden = $"""<input type="hidden" name="{E(name)}" id="{E(id)}-value" value="{E(Format(crop))}" data-m-cropper-value />""";
        return RootAttributes(id, label, ratio, height, cssClass) + Parts(src, alt, ratio) + hidden + "</div>";
    }

    /// <summary>The toolbar, the picture and the readout with the preview: everything inside the root except the hidden input.</summary>
    public static string Parts(string src, string alt, string ratio = "free")
    {
        CheckRatio(ratio);
        return Toolbar(ratio) + Stage(src, alt) + Footer();
    }

    /// <summary>The aspect ratio buttons; the pressed one is the root's starting shape.</summary>
    public static string Toolbar(string ratio = "free")
    {
        CheckRatio(ratio);
        var sb = new StringBuilder("""<div class="m-cropper-toolbar" role="toolbar" aria-label="Crop shape">""");
        foreach (var (key, label) in Ratios)
        {
            var pressed = key == ratio ? "true" : "false";
            sb.Append($"""<button type="button" class="ant-btn ant-btn-default" data-m-cropper-ratio="{E(key)}" aria-pressed="{pressed}">{E(label)}</button>""");
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>The picture the crop box sits over. Cropper.js takes the image over when it starts.</summary>
    public static string Stage(string src, string alt)
    {
        var url = Source(src);
        return $"""<div class="m-cropper-stage" data-m-cropper-stage><img class="m-cropper-image" data-m-cropper-image src="{E(url)}" alt="{E(alt)}" /></div>""";
    }

    /// <summary>The crop's size and place, and a preview of the crop.</summary>
    public static string Footer() =>
        """<div class="m-cropper-footer"><output class="m-cropper-box" data-m-cropper-box aria-live="polite">Drag the picture to choose the crop.</output><div class="m-cropper-preview" data-m-cropper-preview></div></div>""";

    /// <summary>
    /// The script's JSON as a value: a crop in the picture's natural pixels, from its top left corner.
    /// </summary>
    public static CropBox? ParseBox(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<CropBox>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The value the hidden input starts with (empty when there is no crop yet).</summary>
    public static string Format(CropBox? box) => box is null ? "" : JsonSerializer.Serialize(box, Json);

    private static void CheckRatio(string ratio)
    {
        if (!Ratios.Any(r => r.Key == ratio)) throw new ArgumentException($"A crop shape is one of: {string.Join(", ", Ratios.Select(r => r.Key))}.", nameof(ratio));
    }

    // An image data URI is allowed: an <img> never runs script, and the gallery's pictures are data URIs.
    private static string Source(string url)
    {
        if (url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal)) return url;
        if (url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) return url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) return url;
        throw new ArgumentException("A picture is a same-site path, an http(s) address or an image data URI.", nameof(url));
    }
}
