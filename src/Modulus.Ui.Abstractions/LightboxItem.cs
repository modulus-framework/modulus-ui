using System.Text.Json;

namespace Modulus.Ui.Abstractions;

/// <summary>
/// One image of a lightbox, shared by the Blazor <c>AppLightbox</c>, the MVC <c>&lt;app-lightbox&gt;</c> and the
/// preview of <c>&lt;app-image&gt;</c> / <c>&lt;app-image-group&gt;</c>. The preview itself runs in modulus-shell.js.
/// </summary>
/// <param name="Src">Full-size image URL.</param>
/// <param name="Title">Name of the image: its alt text, and the first line of the caption.</param>
/// <param name="Caption">Optional second line under the title.</param>
/// <param name="Thumb">Smaller image for the grid and the thumbnail strip; defaults to <paramref name="Src"/>.</param>
public sealed record LightboxItem(string Src, string Title = "", string? Caption = null, string? Thumb = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The <c>data-items</c> JSON the lightbox script reads (src, title, caption, thumb).</summary>
    public static string Json(IEnumerable<LightboxItem> items) =>
        JsonSerializer.Serialize(items.Select(i => new { src = i.Src, title = i.Title, caption = i.Caption, thumb = i.Thumb ?? i.Src }), JsonOptions);
}
