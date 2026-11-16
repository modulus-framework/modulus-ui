using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// Hierarchical and composite widgets: Cascader, TreeSelect, Tree, Transfer, Calendar, Carousel and Anchor.
// Each renders Ant Design's DOM on the server (so the first paint matches the Blazor component) and an
// Alpine component in modulus-widgets.js takes over the interaction. Form widgets post plain inputs:
// hidden inputs that are disabled while unselected, so a form posts the current selection without script too.


internal static class Widgets
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>JSON for an Alpine <c>x-data</c> argument, encoded for an attribute value.</summary>
    public static string Config(object value) => Html.E(JsonSerializer.Serialize(value, Json));

    public static string Arrow() =>
        $"""<span class="ant-select-arrow" unselectable="on" aria-hidden="true" style="user-select: none;">{AntIcons.Span("down", IconThemeType.Outline, "ant-select-suffix")}</span>""";

    /// <summary>Values of a bound model: one string, or the items of a sequence.</summary>
    public static List<string> Values(object? model, string? value) => model switch
    {
        string s => [s],
        IEnumerable items => items.Cast<object?>().Where(i => i is not null).Select(i => Html.Format(i)).ToList(),
        null => value is null ? [] : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        _ => [Html.Format(model)],
    };
}
