using AntDesign;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Extras;
using Modulus.Ui.Abstractions;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>GridSchema.AddOption and AppCreatableSelect (Select with an "Add item" row).</summary>
public class CreatableSelectTests : BunitContext
{
    private static readonly GridOption[] Colors = [new("red", "Red", "red"), new("blue", "Blue", "blue")];

    public CreatableSelectTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddAntDesign();
    }

    [Fact]
    public void AddOption_TrimsKeysAndTintsTheNewOption()
    {
        var (options, added, error) = GridSchema.AddOption(Colors, "  Sky blue ", "geekblue");
        Assert.Null(error);
        Assert.Equal(new GridOption("skyBlue", "Sky blue", "geekblue"), added);
        Assert.Equal(3, options.Count);
        Assert.Equal(2, Colors.Length); // the input list is not changed

        // An unknown colour takes the next one in turn; a taken key gets a number.
        var (_, next, _) = GridSchema.AddOption([.. Colors, new("green", "Leaf", "green")], "Green", "#123456");
        Assert.Equal(new GridOption("green2", "Green", GridSchema.OptionColors[3]), next);
    }

    [Theory]
    [InlineData("", "Enter a name.")]
    [InlineData("  ", "Enter a name.")]
    [InlineData("RED", "“RED” is already in the list.")]
    public void AddOption_RejectsEmptyAndDuplicateLabels(string label, string message)
    {
        var (options, added, error) = GridSchema.AddOption(Colors, label, null);
        Assert.Equal(message, error);
        Assert.Null(added);
        Assert.Same(Colors, options);
    }

    [Fact]
    public void AddOption_CapsLengthAndCount()
    {
        Assert.Equal("Use 60 characters or fewer.", GridSchema.AddOption(Colors, new string('x', 61), null).Error);
        var full = Enumerable.Range(0, GridSchema.MaxOptions).Select(i => new GridOption($"o{i}", $"O{i}")).ToList();
        Assert.Equal("A list holds up to 50 options.", GridSchema.AddOption(full, "One more", null).Error);
    }

    [Fact]
    public void Closed_ShowsTheSelectionWithItsColourDot_OrThePlaceholder()
    {
        var cut = Render<AppCreatableSelect>(p => p.Add(s => s.Options, Colors).Add(s => s.Value, "blue").Add(s => s.AllowClear, true));
        Assert.Equal("ant-select ant-select-single ant-select-show-arrow m-cselect ant-select-allow-clear", cut.Find(".ant-select").GetAttribute("class"));
        Assert.Equal("Blue", cut.Find(".ant-select-selection-item").TextContent);
        Assert.NotNull(cut.Find(".ant-select-selection-item .m-cselect-dot.ant-tag-blue"));
        Assert.Empty(cut.FindAll(".m-cselect-dropdown"));

        var empty = Render<AppCreatableSelect>(p => p.Add(s => s.Options, Colors).Add(s => s.Placeholder, "Colour"));
        Assert.Equal("Colour", empty.Find(".ant-select-selection-placeholder").TextContent);
        Assert.Empty(empty.FindAll(".ant-select-clear"));
    }

    [Fact]
    public void Open_ListsOptionsAndTheAddRow_AndPickingCloses()
    {
        string? value = null;
        var cut = Render<AppCreatableSelect>(p => p.Add(s => s.Options, Colors).Add(s => s.ValueChanged, v => value = v));

        cut.Find(".ant-select-selector").Click();
        Assert.Equal("true", cut.Find(".ant-select-selector").GetAttribute("aria-expanded"));
        Assert.Equal(["Red", "Blue"], cut.FindAll(".ant-select-item-option").Select(o => o.TextContent));
        Assert.Equal(GridSchema.OptionColors.Count, cut.FindAll(".m-cselect-swatch").Count);
        Assert.Equal("true", cut.Find(".m-cselect-swatch.ant-tag-orange").GetAttribute("aria-checked")); // third colour for a third option

        cut.FindAll(".ant-select-item-option")[1].Click();
        Assert.Equal("blue", value);
        Assert.Empty(cut.FindAll(".m-cselect-dropdown"));
    }

    [Fact]
    public void Add_RaisesOptionsChanged_SelectsTheNewOption_AndShowsErrors()
    {
        IReadOnlyList<GridOption>? options = null;
        string? value = null;
        var cut = Render<AppCreatableSelect>(p => p
            .Add(s => s.Options, Colors)
            .Add(s => s.OptionsChanged, o => options = o)
            .Add(s => s.ValueChanged, v => value = v));
        cut.Find(".ant-select-selector").Click();

        cut.Find(".m-cselect-add input").Input("red");
        cut.Find(".m-cselect-add-btn").Click();
        Assert.Equal("“red” is already in the list.", cut.Find(".m-cselect-error").TextContent);
        Assert.Null(options);

        cut.Find(".m-cselect-add input").Input("Mint");
        cut.Find(".m-cselect-swatch.ant-tag-cyan").Click();
        cut.Find(".m-cselect-add input").KeyDown("Enter");
        Assert.Equal(new GridOption("mint", "Mint", "cyan"), options![^1]);
        Assert.Equal("mint", value);
    }

    [Fact]
    public void Multiple_AddsToTheValues_TagsRemove_AndOnAddCanSaveFirst()
    {
        IReadOnlyList<string> values = ["red"];
        var saved = new List<string>();
        var cut = Render<AppCreatableSelect>(p => p
            .Add(s => s.Options, Colors)
            .Add(s => s.Multiple, true)
            .Add(s => s.Values, values)
            .Add(s => s.ValuesChanged, v => values = v)
            .Add(s => s.OnAdd, (label, color) =>
            {
                saved.Add($"{label}/{color}");
                return Task.FromResult<(GridOption?, string?)>((new GridOption("x1", label, color), null));
            }));

        Assert.Equal("ant-select-selection-item m-cselect-tag ant-tag-red", cut.Find(".ant-select-selection-item").GetAttribute("class"));
        cut.Find(".ant-select-selector").Click();
        cut.Find(".m-cselect-add input").Input("Teal");
        cut.Find(".m-cselect-add-btn").Click();

        Assert.Equal(["Teal/orange"], saved);
        Assert.Equal(["red", "x1"], values);
        Assert.NotEmpty(cut.FindAll(".m-cselect-dropdown")); // multiple stays open

        cut.Find(".ant-select-selection-item-remove").Click();
        Assert.Equal(["x1"], values);
    }
}
