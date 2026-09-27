using Microsoft.AspNetCore.Components;

namespace Modulus.Blazor.Components.Fields;

/// <summary>
/// Base of the business fields that edit a typed value through text (money, quantity, duration, number range):
/// the text shows <see cref="Format"/> of the value, and a change parses it back. Text that does not parse
/// puts the last valid value back. The MVC tag helpers render the same DOM and format through the same
/// Modulus.Ui.Abstractions.Forms rules.
/// </summary>
public abstract class FieldBase<TValue> : ComponentBase
{
    [Parameter] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue?> ValueChanged { get; set; }

    [Parameter] public string? Placeholder { get; set; }

    /// <summary>"small", "middle" (default) or "large".</summary>
    [Parameter] public string? Size { get; set; }

    /// <summary>"error" or "warning".</summary>
    [Parameter] public string? Status { get; set; }

    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string? Id { get; set; }
    [Parameter] public string? AriaLabel { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? Style { get; set; }

    protected string Text { get; set; } = "";

    private TValue? _last;
    private string? _lastKey;

    /// <summary>The value as the field shows it; "" for none.</summary>
    protected abstract string Format(TValue? value);

    /// <summary>Reads non-empty text; false keeps the previous value.</summary>
    protected abstract bool TryParse(string text, out TValue? value);

    /// <summary>Clamps or rounds a parsed value before it is set.</summary>
    protected virtual TValue? Normalize(TValue? value) => value;

    /// <summary>Anything besides the value that changes the text (decimals of the currency or unit).</summary>
    protected virtual string FormatKey => "";

    protected override void OnParametersSet()
    {
        if (EqualityComparer<TValue?>.Default.Equals(Value, _last) && FormatKey == _lastKey) return;
        _last = Value;
        _lastKey = FormatKey;
        Text = Format(Value);
    }

    protected async Task OnTextChanged(ChangeEventArgs e)
    {
        var typed = e.Value as string ?? "";
        TValue? next;
        if (string.IsNullOrWhiteSpace(typed)) next = default;
        else if (TryParse(typed, out var parsed)) next = Normalize(parsed);
        else
        {
            await ShowAsync(typed, Format(Value)); // not a value: put the last one back
            return;
        }
        _last = next;
        await ShowAsync(typed, Format(next));
        await ValueChanged.InvokeAsync(next);
    }

    /// <summary>
    /// Replaces what the user typed with <paramref name="formatted"/>. Blazor only writes the input when the
    /// rendered text changes, so the typed text is rendered first when the two could match.
    /// </summary>
    protected async Task ShowAsync(string typed, string formatted)
    {
        if (Text == formatted && typed != formatted)
        {
            Text = typed;
            StateHasChanged();
            await Task.Yield();
        }
        Text = formatted;
    }

    protected string? SizeClass(string prefix) => Size switch { "small" => prefix + "-sm", "large" => prefix + "-lg", _ => null };

    protected string? StatusClass(string prefix) => Status is { Length: > 0 } s ? $"{prefix}-status-{s}" : null;

    protected static string Cx(params string?[] classes) => string.Join(' ', classes.Where(c => !string.IsNullOrWhiteSpace(c)));
}
