using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Qr;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>What <see cref="CreatableSelectTagHelper"/> renders; the editable grid builds one for its option editors.</summary>
/// <param name="ListId">Id of the listbox (options get <c>{ListId}-{index}</c>).</param>
internal sealed record CreatableSelectModel(string Name, string ListId, IReadOnlyList<GridOption> Options)
{
    public string? Id { get; init; }
    public string? SizeClass { get; init; }
    public bool Multiple { get; init; }
    public string? Value { get; init; }
    public IReadOnlyList<string> Values { get; init; } = [];
    public string? Placeholder { get; init; }
    public bool AllowClear { get; init; }
    public bool Disabled { get; init; }
    public bool AllowAdd { get; init; } = true;
    public bool ShowColors { get; init; } = true;
    public string AddPlaceholder { get; init; } = "New item";
    public string AddText { get; init; } = "Add";
    public string CreateText { get; init; } = "Create new item";
    public string? AddHref { get; init; }
    public string? AddModal { get; init; }
    public string AddNewText { get; init; } = "Add new";
    public string? AriaLabel { get; init; }

    /// <summary>
    /// In an editable grid: the <c>add-option:{key}:{row}</c> operation "Add" posts (with <see cref="OptionField"/> and
    /// <see cref="ColorField"/>), so the new option joins the column's schema on the server.
    /// </summary>
    public string? GridOp { get; init; }
    public string? OptionField { get; init; }
    public string? ColorField { get; init; }
}
