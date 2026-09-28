using AntDesign;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Extras;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Qr;
using Modulus.Ui.Abstractions.Theming;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>QRCode, ColorPicker, FloatButton and Tour (Ant Design 5 components built in Modulus).</summary>
public class ExtrasTests : BunitContext
{
    public ExtrasTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddAntDesign();
    }

    [Fact]
    public void QRCode_RendersTheSameSvgAsTheMvcTagHelper()
    {
        var cut = Render<AppQRCode>(p => p.Add(q => q.Value, "https://modulus.dev").Add(q => q.Size, 160).Add(q => q.Class, "mine"));

        var box = cut.Find("div.ant-qrcode");
        Assert.Equal("ant-qrcode m-qrcode mine", box.GetAttribute("class"));
        Assert.Equal("width:160px;height:160px;background-color:transparent", box.GetAttribute("style"));
        Assert.Equal("134", cut.Find("svg").GetAttribute("width"));
        Assert.Empty(cut.FindAll(".ant-qrcode-mask"));
    }

    [Fact]
    public void QRCode_ExpiredRefreshRaisesOnRefresh()
    {
        var refreshed = false;
        var cut = Render<AppQRCode>(p => p
            .Add(q => q.Value, "x")
            .Add(q => q.Status, QRCodeStatus.Expired)
            .Add(q => q.OnRefresh, () => refreshed = true));

        Assert.Equal("QR code expired", cut.Find(".ant-qrcode-expired").TextContent);
        cut.Find(".ant-qrcode-mask button").Click();
        Assert.True(refreshed);
    }

    [Fact]
    public void ColorPicker_OpensPicksPresetAndBindsValue()
    {
        string? value = "#1677ff";
        var cut = Render<AppColorPicker>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, v => value = v)
            .Add(c => c.ShowText, true));

        Assert.Equal("#1677ff", cut.Find(".ant-color-picker-trigger-text").TextContent);
        Assert.Empty(cut.FindAll(".m-color-picker-popup"));

        cut.Find(".ant-color-picker-trigger").Click();
        Assert.Single(cut.FindAll(".m-color-picker-popup"));
        Assert.Contains("ant-color-picker-trigger-active", cut.Find(".ant-color-picker-trigger").ClassList);
        Assert.Equal(16, cut.FindAll(".ant-color-picker-presets-color").Count); // Recommended by default
        Assert.Single(cut.FindAll(".ant-color-picker-presets-color-checked"));

        cut.Find(".ant-color-picker-presets-color[title='#52c41a']").Click();
        Assert.Equal("#52c41a", value);

        cut.Find(".ant-color-picker-input input").Change("rgb(255, 0, 0)");
        Assert.Equal("#ff0000", value);

        cut.Find(".ant-color-picker-alpha-input input").Change("50");
        Assert.Equal("#ff000080", value);

        cut.Find(".m-color-picker-popup").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(cut.FindAll(".m-color-picker-popup"));
    }

    [Fact]
    public void ColorPicker_SlidersFollowThePointerAndKeys()
    {
        string? value = "#ff0000";
        var cut = Render<AppColorPicker>(p => p.Add(c => c.Value, value).Add(c => c.ValueChanged, v => value = v).Add(c => c.Format, "rgb"));
        cut.Find(".ant-color-picker-trigger").Click();

        cut.Find(".ant-color-picker-slider-hue").PointerDown(new PointerEventArgs { OffsetX = 66, PointerId = 1 }); // 66 / 198 × 360 = 120°
        Assert.Equal("rgb(0, 255, 0)", value);

        cut.Find(".ant-color-picker-select").PointerDown(new PointerEventArgs { OffsetX = 0, OffsetY = 80, PointerId = 1 }); // no saturation, half value
        Assert.Equal("rgb(128, 128, 128)", value);

        cut.Find(".ant-color-picker-select").KeyDown(new KeyboardEventArgs { Key = "ArrowUp", ShiftKey = true });
        Assert.Equal("rgb(153, 153, 153)", value); // value 0.5 → 0.6

        Assert.Equal("120", cut.Find(".ant-color-picker-slider-hue").GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void ColorPicker_ClearDisabledAndPresets()
    {
        string? value = "#1677ff";
        var cut = Render<AppColorPicker>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, v => value = v)
            .Add(c => c.AllowClear, true)
            .Add(c => c.DisabledAlpha, true)
            .Add(c => c.Presets, [new ColorPreset("Brand", ["#066fd1", "#4263eb"])]));
        cut.Find(".ant-color-picker-trigger").Click();

        Assert.Empty(cut.FindAll(".ant-color-picker-slider-alpha"));
        Assert.Equal("Brand", cut.Find(".ant-color-picker-presets-label").TextContent);
        Assert.Equal(2, cut.FindAll(".ant-color-picker-presets-color").Count);

        cut.Find(".m-color-picker-clear-btn").Click();
        Assert.Null(value);
        Assert.Single(cut.FindAll(".ant-color-picker-trigger .ant-color-picker-clear"));

        var disabled = Render<AppColorPicker>(p => p.Add(c => c.Value, "#000").Add(c => c.Disabled, true));
        disabled.Find(".ant-color-picker-trigger").Click();
        Assert.Empty(disabled.FindAll(".m-color-picker-popup"));
        Assert.Contains("ant-color-picker-trigger-disabled", disabled.Find(".ant-color-picker-trigger").ClassList);
    }

    [Fact]
    public void FloatButton_RendersBadgeTooltipAndLink()
    {
        var clicks = 0;
        var cut = Render<AppFloatButton>(p => p
            .Add(b => b.Icon, "question-circle")
            .Add(b => b.Tooltip, "Help")
            .Add(b => b.Type, "primary")
            .Add(b => b.BadgeCount, 5)
            .Add(b => b.OnClick, () => clicks++));

        var button = cut.Find("button");
        Assert.Equal("ant-float-btn ant-float-btn-primary ant-float-btn-circle", button.GetAttribute("class"));
        Assert.Equal("Help", button.GetAttribute("aria-label"));
        Assert.Equal("5", cut.Find(".m-float-btn-badge").TextContent);
        button.Click();
        Assert.Equal(1, clicks);

        var link = Render<AppFloatButton>(p => p.Add(b => b.Href, "/docs").Add(b => b.Shape, "square").Add(b => b.Description, "Docs"));
        Assert.Equal("/docs", link.Find("a.ant-float-btn-square").GetAttribute("href"));
        Assert.Empty(link.FindAll(".ant-float-btn-icon"));
    }

    [Fact]
    public void FloatButtonGroup_ClickTriggerTogglesChildren()
    {
        var cut = Render<AppFloatButtonGroup>(p => p
            .Add(g => g.Trigger, "click")
            .AddChildContent<AppFloatButton>(b => b.Add(x => x.Icon, "edit")));

        Assert.Empty(cut.FindAll(".ant-float-btn-group-wrap"));
        cut.Find(".ant-float-btn-group > button").Click();
        Assert.Single(cut.FindAll(".ant-float-btn-group-wrap .ant-float-btn"));
        Assert.Contains("ant-float-btn-group-open", cut.Find(".ant-float-btn-group").ClassList);
        Assert.Equal("true", cut.Find(".ant-float-btn-group > button").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void FloatBackTop_CarriesTheVisibilityHeight()
    {
        var cut = Render<AppFloatBackTop>(p => p.Add(b => b.VisibilityHeight, 200));
        var button = cut.Find("button.m-float-back-top");
        Assert.Equal("200", button.GetAttribute("data-m-back-top"));
        Assert.False(button.HasAttribute("data-m-target"));
        Assert.Equal("Back to top", button.GetAttribute("aria-label"));
    }

    [Fact]
    public void Tour_StepsThroughAndFinishes()
    {
        TourStep[] steps = [new("Upload", "Drop files", "#up"), new("Save", Target: "#save", Placement: "top"), new("Done")];
        var open = true;
        var finished = false;
        var cut = Render<AppTour>(p => p
            .Add(t => t.Steps, steps)
            .Add(t => t.Open, open)
            .Add(t => t.OpenChanged, v => open = v)
            .Add(t => t.OnFinish, () => finished = true));

        var card = cut.Find(".ant-tour");
        Assert.Equal("#up", card.GetAttribute("data-target"));
        Assert.Equal("Upload", cut.Find(".ant-tour-title").TextContent);
        Assert.Empty(cut.FindAll(".ant-tour-prev-btn"));
        Assert.Equal(3, cut.FindAll(".ant-tour-indicator").Count);
        Assert.Single(cut.FindAll(".ant-tour-mask"));
        JSInterop.VerifyInvoke("modulusShell.tour");

        cut.Find(".ant-tour-next-btn").Click();
        Assert.Equal("Save", cut.Find(".ant-tour-title").TextContent);
        Assert.Contains("ant-tour-placement-top", cut.Find(".ant-tour").ClassList);

        cut.Find(".m-tour").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("Finish", cut.Find(".ant-tour-next-btn").TextContent);
        cut.Find(".ant-tour-next-btn").Click();

        Assert.True(finished);
        Assert.False(open);
        Assert.Empty(cut.FindAll(".ant-tour"));
    }

    [Fact]
    public void Tour_EscapeCloses()
    {
        var closed = false;
        var cut = Render<AppTour>(p => p
            .Add(t => t.Steps, [new TourStep("Only")])
            .Add(t => t.Open, true)
            .Add(t => t.Mask, false)
            .Add(t => t.OnClose, () => closed = true));

        Assert.Empty(cut.FindAll(".ant-tour-mask"));
        cut.Find(".m-tour").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.True(closed);
        Assert.Empty(cut.FindAll(".ant-tour"));
    }

    [Fact]
    public void Lightbox_RendersTheGridAndTheOverlayTheScriptClones()
    {
        var cut = Render<AppLightbox>(p => p.Add(l => l.Items,
            [new LightboxItem("a.png", "Lake", "Morning light"), new LightboxItem("b.png", "Dunes", Thumb: "b-small.png")]));

        var root = cut.Find("[data-m-lightbox]");
        Assert.Equal(LightboxItem.Json([new("a.png", "Lake", "Morning light"), new("b.png", "Dunes", Thumb: "b-small.png")]), root.GetAttribute("data-items"));
        Assert.Equal("--m-lightbox-min:180px;--m-lightbox-aspect:4 / 3", root.GetAttribute("style"));
        Assert.Equal("Morning light", cut.Find(".m-lightbox-item-caption").TextContent);
        Assert.Equal("b-small.png", cut.FindAll(".m-lightbox-tile img")[1].GetAttribute("src"));
        Assert.Equal("1", cut.FindAll("[data-lightbox-index]")[1].GetAttribute("data-lightbox-index"));
        Assert.True(cut.Find(".m-lightbox-source").HasAttribute("hidden"));
        foreach (var part in new[] { "progress", "close", "zoom-in", "zoom-out", "rotate-right", "rotate-left", "download", "pan", "img", "prev", "next", "spin", "footer", "text", "title", "caption", "thumbs" })
            Assert.Single(cut.FindAll($"[data-lb='{part}']"));
    }
}
