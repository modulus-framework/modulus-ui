using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Forms;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-uploader, app-image-cropper and app-code-editor: the same markup as the Blazor components.</summary>
public class EditingTagHelperTests
{
    private static async Task<string> RenderAsync(TagHelper helper, string tag)
    {
        var output = new TagHelperOutput(tag, [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext(tag, [], new Dictionary<object, object>(), "p1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task AppUploader_RendersTheDropAreaWithItsLimitsInBytes()
    {
        var html = await RenderAsync(new UploaderTagHelper { Id = "docs", Action = "/files/upload", Accept = ".pdf", MaxSize = 5242880, MaxCount = 6 }, "app-uploader");

        Assert.StartsWith("<div class=\"m-uploader\" id=\"docs\" data-m-uploader", html);
        Assert.Contains("data-action=\"/files/upload\"", html);
        Assert.Contains("data-max-size=\"5242880\"", html);
        Assert.Contains("data-max-count=\"6\"", html);
        Assert.DoesNotContain("<app-uploader", html);
    }

    [Fact]
    public async Task AppImageCropper_PostsItsStartingCropInTheFieldAndStartsOnTheRatio()
    {
        var html = await RenderAsync(new ImageCropperTagHelper
        {
            Id = "photo", Src = "/a.jpg", Alt = "A", Name = "crop", Ratio = "4/3", Crop = "{\"x\":1,\"y\":2,\"width\":3,\"height\":4}",
        }, "app-image-cropper");

        Assert.Contains("data-ratio=\"4/3\"", html);
        Assert.Contains("data-m-cropper-ratio=\"4/3\" aria-pressed=\"true\"", html);
        Assert.Contains("name=\"crop\" id=\"photo-value\" value=\"{&quot;x&quot;:1,&quot;y&quot;:2,&quot;width&quot;:3,&quot;height&quot;:4}\"", html);
        Assert.DoesNotContain("<app-image-cropper", html);
    }

    [Fact]
    public async Task AppCodeEditor_ShowsTheValueInItsLanguage_AndCanBeReadOnly()
    {
        var html = await RenderAsync(new CodeEditorTagHelper { Language = "json", ReadOnly = true, Label = "Rule" }, "app-code-editor");

        Assert.Contains("data-language=\"json\"", html);
        Assert.Contains("data-readonly", html);
        Assert.Contains("data-m-code-value", html);
        Assert.DoesNotContain("<app-code-editor", html);
    }

    [Fact]
    public async Task AppSchemaForm_DrawsItsFieldsWithTheValuesAndMessagesItWasGiven()
    {
        var schema = FormSchema.Parse("""{ "name": "S", "fields": [ { "key": "employee", "label": "Employee", "kind": "Text", "required": true }, { "key": "receipt", "label": "Receipt", "kind": "Switch" } ] }""");
        var problems = schema.Validate(new Dictionary<string, string?> { ["receipt"] = "true" });

        var html = await RenderAsync(new SchemaFormTagHelper
        {
            Schema = schema, Values = new Dictionary<string, string?> { ["receipt"] = "true" }, Problems = problems, IdPrefix = "claim",
        }, "app-schema-form");

        Assert.StartsWith("<div class=\"m-schema-form\" id=\"claim\">", html);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "data-key=").Count);
        Assert.Contains("<div class=\"ant-form-item-explain-error\">Employee is required.</div>", html);
        Assert.Contains("name=\"receipt\" id=\"claim-receipt\" value=\"true\" checked", html);
        Assert.Contains("<label for=\"claim-employee\" class=\"ant-form-item-required\">Employee</label>", html);
        Assert.DoesNotContain("<app-schema-form", html);
    }

    [Fact]
    public async Task AppSchemaForm_NeedsASchema()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => RenderAsync(new SchemaFormTagHelper(), "app-schema-form"));
    }
}
