using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>The form elements page's controls in MVC: the HTML add-ons, and the stacked radio and checkbox groups.</summary>
public class FormElementsTagHelperTests
{
    private static async Task<string> RenderAsync(TagHelper helper, string tag)
    {
        var output = new TagHelperOutput(tag, [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext(tag, [], new Dictionary<object, object>(), "id"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task AnAddonAfterHtml_IsWrittenAsItIs()
    {
        var html = await RenderAsync(new InputTagHelper { Name = "forgot", AddonAfterHtml = "<a href='/x'>Forgot?</a>" }, "app-input");

        Assert.Contains("<span class=\"ant-input-group-addon\"><a href='/x'>Forgot?</a></span>", html);
    }

    [Fact]
    public async Task AnAddonBeforeText_IsStillEscaped()
    {
        var html = await RenderAsync(new InputTagHelper { Name = "u", AddonBefore = "<b>https://</b>" }, "app-input");

        Assert.Contains("&lt;b&gt;https://&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>https://</b>", html);
    }

    [Fact]
    public async Task AVerticalCheckboxGroup_StacksItsBoxes()
    {
        var html = await RenderAsync(new CheckboxGroupTagHelper { Name = "channels", Items = [new("Email", "email")], Vertical = true }, "app-checkbox-group");

        Assert.Contains("ant-checkbox-group", html);
        Assert.Contains("m-choice-vertical", html);
    }

    [Fact]
    public async Task AHorizontalCheckboxGroup_IsTheDefault()
    {
        var html = await RenderAsync(new CheckboxGroupTagHelper { Name = "channels", Items = [new("Email", "email")] }, "app-checkbox-group");

        Assert.DoesNotContain("m-choice-vertical", html);
    }

    [Fact]
    public async Task AMultipleSelect_OffersSeveralOptionsAndMarksTheChosenOnes()
    {
        var items = new[] { new SelectListItem("C#", "C#", true), new SelectListItem("Go", "Go", true), new SelectListItem("Rust", "Rust") };
        var html = await RenderAsync(new SelectTagHelper { Name = "languages", Multiple = true, Items = items, Placeholder = "Any" }, "app-select");

        Assert.Contains("multiple=\"multiple\"", html);
        Assert.Contains("<option value=\"C#\" selected>C#</option>", html);
        Assert.Contains("<option value=\"Rust\">Rust</option>", html);
        Assert.DoesNotContain("<option value=\"\">Any</option>", html);
    }
}
