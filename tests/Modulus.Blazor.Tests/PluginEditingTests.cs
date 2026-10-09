using Bunit;
using Modulus.Blazor.Components.Extras;
using Modulus.Ui.Abstractions.Plugins;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The uploader, cropper and code editor: their markup, the addresses and values they refuse, and their Blazor components.</summary>
public class PluginEditingTests : BunitContext
{
    // Uploader --------------------------------------------------------------------------------------------------

    [Fact]
    public void TheUploader_CarriesItsTargetLimitsAndTheHooksUppyDrives()
    {
        var html = UploaderHtml.Dashboard("drop", "/plugins/dropzone/upload", "file", ".pdf,image/*", 5 * 1024 * 1024, 6, "Five MB each");

        Assert.Contains("id=\"drop\"", html);
        Assert.Contains("data-m-uploader", html);
        Assert.Contains("data-action=\"/plugins/dropzone/upload\"", html);
        Assert.Contains("data-name=\"file\"", html);
        Assert.Contains("data-accept=\".pdf,image/*\"", html);
        Assert.Contains("data-max-size=\"5242880\"", html);
        Assert.Contains("data-max-count=\"6\"", html);
        Assert.Contains("data-note=\"Five MB each\"", html);
        foreach (var hook in new[] { "data-m-uploader-host", "data-m-uploader-fallback", "data-m-uploader-result" })
            Assert.Contains(hook, html);
    }

    [Fact]
    public void TheUploader_LeavesOutLimitsItWasNotGiven()
    {
        var html = UploaderHtml.Dashboard("drop", "/up");

        Assert.DoesNotContain("data-max-size", html);
        Assert.DoesNotContain("data-max-count", html);
        Assert.DoesNotContain("data-accept", html);
        Assert.Contains("data-name=\"file\"", html);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("//evil.example/up")]
    [InlineData("upload")]
    public void AnUploadAddressThatIsNotSameSiteOrHttp_IsRefused(string action) =>
        Assert.Throws<ArgumentException>(() => UploaderHtml.Dashboard("drop", action));

    [Fact]
    public void TheUploader_EncodesItsText_AndRefusesNegativeLimits()
    {
        var html = UploaderHtml.Dashboard("drop", "/up", accept: "\"><script>", note: "<b>Limits</b>");

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("data-note=\"&lt;b&gt;Limits&lt;/b&gt;\"", html);
        Assert.Throws<ArgumentOutOfRangeException>(() => UploaderHtml.Dashboard("drop", "/up", maxSize: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => UploaderHtml.Dashboard("drop", "/up", maxCount: -1));
    }

    [Fact]
    public void TheBlazorUploader_RendersTheDashboardMarkup()
    {
        var cut = Render<AppUploader>(p => p.Add(x => x.Id, "drop").Add(x => x.Action, "/plugins/dropzone/upload").Add(x => x.MaxCount, 3));

        Assert.NotNull(cut.Find("[data-m-uploader]"));
        Assert.Contains("data-max-count=\"3\"", cut.Markup);
    }

    // Cropper ---------------------------------------------------------------------------------------------------

    [Fact]
    public void TheCropper_ShowsItsShapeButtons_WithTheStartingShapePressed()
    {
        var html = CropperHtml.Editor("photo", "/a.jpg", "A photo", "crop", ratio: "1");

        Assert.Contains("data-m-cropper data-ratio=\"1\"", html);
        Assert.Contains("data-m-cropper-ratio=\"1\" aria-pressed=\"true\"", html);
        Assert.Contains("data-m-cropper-ratio=\"free\" aria-pressed=\"false\"", html);
        Assert.Contains("data-m-cropper-ratio=\"16/9\" aria-pressed=\"false\"", html);
        foreach (var hook in new[] { "data-m-cropper-stage", "data-m-cropper-image", "data-m-cropper-box", "data-m-cropper-preview", "data-m-cropper-value" })
            Assert.Contains(hook, html);
    }

    [Fact]
    public void TheCropper_PostsTheCropAsItsFieldValue()
    {
        var html = CropperHtml.Editor("photo", "/a.jpg", "A photo", "crop", crop: new CropBox(10, 20, 300, 200));

        Assert.Contains("<input type=\"hidden\" name=\"crop\" id=\"photo-value\" value=\"{&quot;x&quot;:10,&quot;y&quot;:20,&quot;width&quot;:300,&quot;height&quot;:200}\" data-m-cropper-value />", html);
    }

    [Fact]
    public void TheCropper_ReadsTheCropTheScriptPosts_AndIgnoresWhatItCannotRead()
    {
        Assert.Equal(new CropBox(5, 6, 70, 80), CropperHtml.ParseBox("{\"x\":5,\"y\":6,\"width\":70,\"height\":80}"));
        Assert.Null(CropperHtml.ParseBox(""));
        Assert.Null(CropperHtml.ParseBox("not json"));
        Assert.Equal("", CropperHtml.Format(null));
        Assert.Equal(new CropBox(1, 2, 3, 4), CropperHtml.ParseBox(CropperHtml.Format(new CropBox(1, 2, 3, 4))));
    }

    [Fact]
    public void TheCropper_RefusesAnUnknownShape()
    {
        Assert.Throws<ArgumentException>(() => CropperHtml.Editor("photo", "/a.jpg", "A", "crop", ratio: "2:1"));
    }

    [Theory]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("https://example.com/a.jpg")]
    [InlineData("/a.jpg")]
    public void APictureThatIsAnImageOrSameSiteOrHttp_IsAccepted(string src) =>
        Assert.Contains($"src=\"{System.Net.WebUtility.HtmlEncode(src)}\"", CropperHtml.Stage(src, "A"));

    [Theory]
    [InlineData("data:text/html;base64,AAAA")]
    [InlineData("javascript:alert(1)")]
    [InlineData("a.jpg")]
    public void APictureThatIsNotAnImageOrSameSiteOrHttp_IsRefused(string src) =>
        Assert.Throws<ArgumentException>(() => CropperHtml.Stage(src, "A"));

    [Fact]
    public async Task TheBlazorCropper_ReportsEachCropBackToItsParameter()
    {
        CropBox? reported = null;
        var cut = Render<AppImageCropper>(p => p
            .Add(x => x.Id, "photo")
            .Add(x => x.Src, "/a.jpg")
            .Add(x => x.CropChanged, (CropBox? box) => reported = box));

        Assert.NotNull(cut.Find("[data-m-cropper]"));
        await cut.Find("input[data-m-cropper-value]").ChangeAsync("{\"x\":1,\"y\":2,\"width\":3,\"height\":4}");

        Assert.Equal(new CropBox(1, 2, 3, 4), reported);
    }

    // Code editor -----------------------------------------------------------------------------------------------

    [Fact]
    public void TheCodeEditor_ShowsTheCodeUntilItsEditorStarts_EscapedAndWithItsLanguage()
    {
        var html = CodeEditorHtml.Editor("rule", "rule", "{\"a\": \"<b>\"}", "json", "Rule");

        Assert.Contains("data-m-code data-language=\"json\"", html);
        Assert.Contains("data-label=\"Rule\"", html);
        Assert.Contains("<pre class=\"m-code-fallback\">{&quot;a&quot;: &quot;&lt;b&gt;&quot;}</pre>", html);
        Assert.Contains("name=\"rule\" id=\"rule-value\" value=\"{&quot;a&quot;: &quot;&lt;b&gt;&quot;}\" data-m-code-value", html);
        Assert.DoesNotContain("data-readonly", html);
    }

    [Fact]
    public void TheCodeEditor_KeepsNewLinesInItsValue_AndCanBeReadOnly()
    {
        var html = CodeEditorHtml.Editor("total", "total", "a\nb", "javascript", readOnly: true);

        Assert.Contains("value=\"a\nb\"", html);
        Assert.Contains("data-readonly", html);
    }

    [Fact]
    public void TheCodeEditor_EscapesTheCodeSoItCannotCloseItsHost()
    {
        var html = CodeEditorHtml.Host("</pre><script>alert(1)</script>");

        Assert.DoesNotContain("<script>", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "</pre>"));
    }

    [Fact]
    public void TheCodeEditor_RefusesAnUnknownLanguage()
    {
        Assert.Throws<ArgumentException>(() => CodeEditorHtml.Editor("x", "x", "", language: "cobol"));
        Assert.Equal(["", "javascript", "typescript", "json", "css", "html"], CodeEditorHtml.Languages);
    }

    [Fact]
    public async Task TheBlazorCodeEditor_ReportsEditsBackToItsValue()
    {
        string? text = null;
        var cut = Render<AppCodeEditor>(p => p
            .Add(x => x.Id, "rule")
            .Add(x => x.Name, "rule")
            .Add(x => x.Value, "{}")
            .Add(x => x.Language, "json")
            .Add(x => x.ValueChanged, (string? t) => text = t));

        Assert.Contains("data-language=\"json\"", cut.Markup);
        await cut.Find("input[data-m-code-value]").ChangeAsync("{\"a\":1}");

        Assert.Equal("{\"a\":1}", text);
    }

    [Fact]
    public void TheBlazorCodeEditor_RefusesAnUnknownLanguage()
    {
        Assert.Throws<ArgumentException>(() => Render<AppCodeEditor>(p => p.Add(x => x.Id, "x").Add(x => x.Language, "cobol")));
    }
}
