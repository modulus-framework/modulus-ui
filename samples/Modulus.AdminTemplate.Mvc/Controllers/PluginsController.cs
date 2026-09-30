using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Plugins pages: charts advanced, colour picker, date picker, dropzone, lightbox, tour. Query-driven ones read the query in the shared view.</summary>
[Route("plugins")]
public class PluginsController : SampleController
{
    [HttpGet("charts-advanced")]
    public IActionResult ChartsAdvanced() => Page("Charts advanced", "Plugins/ChartsAdvanced");

    [HttpGet("color-picker")]
    public IActionResult ColorPicker() => Page("Color picker", "Plugins/ColorPicker");

    [HttpGet("date-picker")]
    public IActionResult DatePicker() => Page("Date picker", "Plugins/DatePicker");

    [HttpGet("dropzone")]
    public IActionResult Dropzone() => Page("Dropzone", "Plugins/Dropzone");

    // The dropzone's upload target: checks each file and throws it away (the demo stores nothing).
    [HttpPost("dropzone/upload")]
    [RequestSizeLimit(PluginsContent.MaxUploadMb * 1024 * 1024 + 64 * 1024)]
    public IActionResult Upload(IFormFile? file) =>
        PluginsContent.CheckUpload(file?.FileName, file?.Length ?? 0) is { } error ? BadRequest(error) : Ok();

    [HttpGet("lightbox")]
    public IActionResult Lightbox() => Page("Lightbox", "Plugins/Lightbox");

    [HttpGet("tour")]
    public IActionResult Tour() => Page("Tour", "Plugins/Tour");

    [HttpGet("player")]
    public IActionResult Player() => Page("Inline player", "Plugins/Player");

    // The player's audio track, made on the server (the Blazor and Razor Pages samples serve the same URL).
    [HttpGet("player/track.wav")]
    public IActionResult Track() => File(PluginsContent.DemoTrack(), "audio/wav", enableRangeProcessing: true);

    // The editor posts its HTML back to the page, which shows it cleaned.
    [HttpGet("editor"), HttpPost("editor")]
    public IActionResult Editor() => Page("WYSIWYG editor", "Plugins/Editor");

    [HttpGet("sortable")]
    public IActionResult Sortable() => Page("Sortable", "Plugins/Sortable");

    [HttpGet("map")]
    public IActionResult Map() => Page("Map", "Plugins/Map");

    [HttpGet("map-fullsize")]
    public IActionResult MapFullsize() => Page("Map fullsize", "Plugins/MapFullsize");

    [HttpGet("map-vector")]
    public IActionResult MapVector() => Page("Map vector", "Plugins/MapVector");
}
