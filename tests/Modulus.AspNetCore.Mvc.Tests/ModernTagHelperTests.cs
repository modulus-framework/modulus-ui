using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Qr;
using Modulus.Ui.Abstractions.Theming;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>QRCode, ColorPicker, FloatButton (group, back-top) and Tour.</summary>
public class ModernTagHelperTests
{
    private sealed record Node(TagHelper Helper, string Tag, string Content = "", Node[]? Children = null, TagHelperAttributeList? Attributes = null);

    private static async Task<string> RenderAsync(Node node, IDictionary<object, object>? items = null)
    {
        items ??= new Dictionary<object, object>();
        var output = new TagHelperOutput(node.Tag, node.Attributes ?? [], async (_, _) =>
        {
            var html = node.Content;
            foreach (var child in node.Children ?? []) html += await RenderAsync(child, new Dictionary<object, object>(items));
            return new DefaultTagHelperContent().SetHtmlContent(html);
        });
        await node.Helper.ProcessAsync(new TagHelperContext(node.Tag, [], items, "u1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private static Task<string> RenderAsync(TagHelper helper, string tag, TagHelperAttributeList? attributes = null) =>
        RenderAsync(new Node(helper, tag, Attributes: attributes));

    [Fact]
    public async Task QRCode_DrawsTheSharedSvgInsideTheBorderedBox()
    {
        var html = await RenderAsync(new QRCodeTagHelper { Value = "https://modulus.dev", Size = 160, AriaLabel = "Site" }, "app-qr-code",
            [new TagHelperAttribute("class", "mine")]);

        Assert.StartsWith("<div class=\"ant-qrcode m-qrcode mine\" style=\"width:160px;height:160px;background-color:transparent\">", html);
        Assert.Contains("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"134\" height=\"134\"", html); // 160 - 12px padding - 1px border, both sides
        Assert.Contains("aria-label=\"Site\"", html);
        Assert.Contains(QRCodeMarkup.Svg("https://modulus.dev", 160, "#000", "transparent", QrErrorLevel.M, null, 40, true, "Site"), html);
        Assert.DoesNotContain("ant-qrcode-mask", html);
    }

    [Fact]
    public async Task QRCode_ExpiredShowsARefreshLink_BorderlessUsesTheFullSize()
    {
        var html = await RenderAsync(new QRCodeTagHelper { Value = "x", Size = 120, Bordered = false, Status = QRCodeStatus.Expired, RefreshHref = "/qr?new=1" }, "app-qr-code");

        Assert.Contains("ant-qrcode-borderless", html);
        Assert.Contains("width=\"120\"", html);
        Assert.Contains("<p class=\"ant-qrcode-expired\">QR code expired</p>", html);
        Assert.Contains("<a class=\"ant-btn ant-btn-link\" href=\"/qr?new=1\">", html);
    }

    [Fact]
    public async Task QRCode_ExpiredWithoutHrefRefreshesInTheBrowser()
    {
        var html = await RenderAsync(new QRCodeTagHelper { Value = "https://modulus.dev/login", Status = QRCodeStatus.Expired }, "app-qr-code");

        Assert.Contains("x-data=\"{ expired: true }\"", html);
        Assert.Contains("<div class=\"ant-qrcode-mask\" x-show=\"expired\">", html);
        Assert.Contains("<button type=\"button\" class=\"ant-btn ant-btn-link\" @click=\"expired = false; $dispatch('modulus-qr-refresh', &quot;https://modulus.dev/login&quot;)\">", html);
    }

    [Fact]
    public async Task QRCode_TooLongDrawsAMessageInsteadOfThrowing()
    {
        var html = await RenderAsync(new QRCodeTagHelper { Value = new string('x', 3000) }, "app-qr-code");
        Assert.Contains("m-qrcode-error", html);
        Assert.DoesNotContain("<svg", html);
    }

    [Fact]
    public async Task ColorPicker_PostsTheColourThroughAHiddenInput_WithPresetsAndAlpha()
    {
        var html = await RenderAsync(new ColorPickerTagHelper
        {
            Name = "Brand",
            Value = "#1677FF",
            ShowText = true,
            AllowClear = true,
            Size = "small",
            Presets = [new ColorPreset("Brand", ["#066fd1", "#ffffff"])],
        }, "app-color-picker");

        Assert.StartsWith("<span class=\"m-color-picker\" x-data=\"modulusColorPicker(", html);
        Assert.Contains("&quot;value&quot;:&quot;#1677ff&quot;,&quot;format&quot;:&quot;hex&quot;,&quot;alpha&quot;:true", html);
        Assert.Contains("<input type=\"hidden\" x-ref=\"input\" name=\"Brand\" value=\"#1677ff\" :value=\"value\" />", html);
        Assert.Contains("class=\"ant-color-picker-trigger ant-color-picker-sm\" id=\"Brand\"", html);
        Assert.Contains("style=\"background:rgb(22, 119, 255)\"", html);
        Assert.Contains("<div class=\"ant-color-picker-trigger-text\" x-text=\"cleared ? '' : text\">#1677ff</div>", html);
        Assert.Contains("ant-color-picker-slider-alpha", html);
        Assert.Contains("m-color-picker-clear-btn", html);
        Assert.Contains("<div class=\"ant-color-picker-presets-label\">Brand</div>", html);
        Assert.Contains("@click=\"pick('#066fd1')\"", html);
        Assert.Contains("ant-color-picker-presets-color ant-color-picker-presets-color-bright", html); // white gets a dark check
        Assert.DoesNotContain("Recommended", html);
    }

    [Fact]
    public async Task ColorPicker_EmptyValueIsCleared_DisabledHasNoPopup_RgbFormat()
    {
        var cleared = await RenderAsync(new ColorPickerTagHelper { Name = "C", Value = "", DisabledAlpha = true }, "app-color-picker");
        Assert.Contains("name=\"C\" value=\"\"", cleared);
        Assert.Contains("<div class=\"ant-color-picker-clear\" x-show=\"cleared\"></div>", cleared);
        Assert.DoesNotContain("ant-color-picker-slider-alpha", cleared);
        Assert.Contains("Recommended", cleared);

        var disabled = await RenderAsync(new ColorPickerTagHelper { Name = "D", Value = "rgba(0,0,0,0.5)", Format = "rgb", Disabled = true }, "app-color-picker");
        Assert.Contains("value=\"rgba(0, 0, 0, 0.5)\"", disabled);
        Assert.Contains("ant-color-picker-trigger-disabled", disabled);
        Assert.DoesNotContain("m-color-picker-popup", disabled);
    }

    [Fact]
    public async Task FloatButton_RendersButtonOrLink_WithBadgeTooltipAndDescription()
    {
        var button = await RenderAsync(new FloatButtonTagHelper { Icon = "question-circle", Tooltip = "Help", BadgeCount = 120, Type = "primary" }, "app-float-button",
            [new TagHelperAttribute("hx-get", "/help")]);
        Assert.StartsWith("<button hx-get=\"/help\" type=\"button\" class=\"ant-float-btn ant-float-btn-primary ant-float-btn-circle\" aria-label=\"Help\">", button);
        Assert.Contains("<div class=\"ant-float-btn-icon\">", button);
        Assert.Contains(">99+</sup>", button);
        Assert.Contains("<span class=\"m-float-btn-tip\" aria-hidden=\"true\">Help</span>", button);

        var link = await RenderAsync(new FloatButtonTagHelper { Href = "/docs", Shape = "square", Description = "Docs" }, "app-float-button");
        Assert.StartsWith("<a href=\"/docs\" class=\"ant-float-btn ant-float-btn-default ant-float-btn-square\" aria-label=\"Docs\">", link);
        Assert.Contains("<div class=\"ant-float-btn-description\">Docs</div>", link);
        Assert.DoesNotContain("ant-float-btn-icon", link); // description only
    }

    [Fact]
    public async Task FloatButtonGroup_TriggerFoldsChildrenBehindAToggle()
    {
        var child = new Node(new FloatButtonTagHelper { Icon = "edit" }, "app-float-button");
        var folded = await RenderAsync(new Node(new FloatButtonGroupTagHelper { Trigger = "hover", Type = "primary" }, "app-float-button-group", Children: [child]));
        Assert.StartsWith("<div class=\"ant-float-btn-group ant-float-btn-group-circle\" x-data=\"{ open: false }\"", folded);
        Assert.Contains("@mouseenter=\"open = true\"", folded);
        Assert.Contains("<div class=\"ant-float-btn-group-wrap\" x-show=\"open\" x-cloak><button type=\"button\" class=\"ant-float-btn", folded);
        Assert.Contains("class=\"ant-float-btn ant-float-btn-primary ant-float-btn-circle\" aria-label=\"Actions\" :aria-expanded=\"open\"", folded);

        var square = await RenderAsync(new Node(new FloatButtonGroupTagHelper { Shape = "square" }, "app-float-button-group", Children: [child]));
        Assert.StartsWith("<div class=\"ant-float-btn-group ant-float-btn-group-square ant-float-btn-group-square-shadow\"><button", square);
    }

    [Fact]
    public async Task FloatBackTop_CarriesTheVisibilityHeightForTheShellScript()
    {
        var html = await RenderAsync(new FloatBackTopTagHelper { VisibilityHeight = 200, Target = "#scroller" }, "app-float-back-top");
        Assert.StartsWith("<button type=\"button\" class=\"ant-float-btn ant-float-btn-default ant-float-btn-circle m-float-back-top\" data-m-back-top=\"200\" data-m-target=\"#scroller\" aria-label=\"Back to top\">", html);
    }

    private static readonly GridOption[] Colors = [new("red", "Red", "red"), new("blue", "Blue", "blue")];

    [Fact]
    public async Task CreatableSelect_PostsAHiddenInput_AndPaintsTheSelection()
    {
        var html = await RenderAsync(new CreatableSelectTagHelper { Name = "label", Value = "blue", Options = Colors, AllowClear = true, AddHref = "/labels", AriaLabel = "Label" },
            "app-creatable-select", [new TagHelperAttribute("style", "width: 180px")]);

        Assert.StartsWith("<div ", html);
        Assert.Contains("class=\"ant-select ant-select-single ant-select-show-arrow m-cselect ant-select-allow-clear\"", html);
        Assert.Contains("style=\"width: 180px\"", html);
        Assert.Contains("x-data=\"modulusCreatableSelect(", html);
        Assert.Contains("&quot;name&quot;:&quot;label&quot;,&quot;multiple&quot;:false,&quot;value&quot;:&quot;blue&quot;", html);
        Assert.Contains("{&quot;value&quot;:&quot;red&quot;,&quot;label&quot;:&quot;Red&quot;,&quot;color&quot;:&quot;red&quot;}", html);
        Assert.Contains("&quot;addHref&quot;:&quot;/labels&quot;", html);
        Assert.Contains("<span class=\"m-cselect-inputs\" x-html=\"inputs()\"><input type=\"hidden\" name=\"label\" value=\"blue\" /></span>", html);
        Assert.Contains("role=\"combobox\" tabindex=\"0\" aria-haspopup=\"listbox\" aria-expanded=\"false\" aria-controls=\"m-cselect-u1\" aria-label=\"Label\"", html);
        Assert.Contains("<span class=\"ant-select-selection-item\" title=\"Blue\"><span class=\"m-cselect-dot ant-tag-blue\" aria-hidden=\"true\"></span>Blue</span>", html);
        Assert.Contains("class=\"ant-select-clear\" role=\"button\" aria-label=\"Clear\" x-show=\"has()\" @click.stop=\"clear()\"", html);
        Assert.Contains("placeholder=\"New item\"", html);
        Assert.Contains("@change.stop @keydown.enter.stop.prevent=\"add()\"", html);
        Assert.Contains("class=\"m-cselect-colors\" role=\"radiogroup\"", html);
    }

    [Fact]
    public async Task CreatableSelect_MultipleRendersTagsAndOneInputPerValue_DisabledHasNoDropdown()
    {
        var html = await RenderAsync(new CreatableSelectTagHelper { Name = "tags", Multiple = true, Values = ["red", "gone"], Options = Colors, AllowAdd = false, ShowColors = false }, "app-creatable-select");
        Assert.Contains("ant-select ant-select-multiple", html);
        Assert.Contains("<input type=\"hidden\" name=\"tags\" value=\"red\" /><input type=\"hidden\" name=\"tags\" value=\"gone\" />", html);
        Assert.Contains("<span class=\"ant-select-selection-item m-cselect-tag ant-tag-red\" title=\"Red\">", html);
        Assert.Contains("data-remove=\"gone\"", html); // unknown values keep their raw text
        Assert.Contains("aria-multiselectable=\"true\"", html);
        Assert.DoesNotContain("m-cselect-footer", html);

        var disabled = await RenderAsync(new CreatableSelectTagHelper { Name = "c", Options = Colors, Placeholder = "Colour", Disabled = true }, "app-creatable-select");
        Assert.Contains("ant-select-disabled", disabled);
        Assert.Contains("aria-disabled=\"true\"><span class=\"ant-select-selection-placeholder\">Colour</span>", disabled);
        Assert.Contains("<input type=\"hidden\" name=\"c\" value=\"\" />", disabled);
        Assert.DoesNotContain("m-cselect-dropdown", disabled);
    }

    [Fact]
    public async Task CreatableSelect_AddModalOpensAModalAndListensForTheSavedOption()
    {
        var html = await RenderAsync(new CreatableSelectTagHelper { Name = "Customer", Options = Colors, AddModal = "customers", AddNewText = "Add new customer" }, "app-creatable-select");

        Assert.Contains("@modulus:option-added.window=\"added($event.detail)\"", html);
        Assert.Contains("&quot;addModal&quot;:&quot;customers&quot;", html);
        Assert.Contains("class=\"ant-btn ant-btn-text ant-btn-block m-cselect-add-new\" @click=\"addNew()\">", html);
        Assert.Contains("<span>Add new customer</span>", html);
        Assert.DoesNotContain("m-cselect-add\"", html); // no inline row
    }

    [Fact]
    public void HxOptionAdded_RaisesTheEventWithTheListAndOption()
    {
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        Modulus.AspNetCore.Mvc.Htmx.HtmxExtensions.HxOptionAdded(http.Response, "customers", new GridOption("87", "Ada", null));
        Assert.Equal("""{"modulus:option-added":{"list":"customers","value":"87","label":"Ada","color":null}}""", http.Response.Headers["HX-Trigger"].ToString());
    }

    [Fact]
    public async Task Tour_CollectsStepsIntoTheAlpineConfig()
    {
        var html = await RenderAsync(new Node(new TourTagHelper { Name = "intro", Type = "primary", FinishText = "Done" }, "app-tour", Children:
        [
            new(new TourStepTagHelper { Title = "Upload", Description = "Drop files", Target = "#up" }, "tour-step"),
            new(new TourStepTagHelper { Title = "Save", Target = "#save", Placement = "top" }, "tour-step"),
        ]));

        Assert.Contains("x-data=\"modulusTour(", html);
        Assert.Contains("&quot;name&quot;:&quot;intro&quot;", html);
        Assert.Contains("{&quot;title&quot;:&quot;Upload&quot;,&quot;description&quot;:&quot;Drop files&quot;,&quot;target&quot;:&quot;#up&quot;,&quot;cover&quot;:null,&quot;placement&quot;:&quot;bottom&quot;}", html);
        Assert.Contains("&quot;placement&quot;:&quot;top&quot;", html);
        Assert.Contains("@modulus-tour-open.window=\"start($event.detail)\"", html);
        Assert.Contains("<svg class=\"ant-tour-mask\"", html);
        Assert.Contains("mask=\"url(#m-tour-u1-mask)\"", html);
        Assert.Contains("class=\"ant-tour ant-tour-primary\" role=\"dialog\" aria-modal=\"true\" aria-labelledby=\"m-tour-u1-title\"", html);
        Assert.Contains("x-text=\"last ? 'Done' : 'Next'\"", html);
        Assert.DoesNotContain("<tour-step", html);
    }
}
