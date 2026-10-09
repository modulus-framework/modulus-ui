using System.Globalization;
using System.Net;
using System.Text;

namespace Modulus.Ui.Abstractions.Forms;

/// <summary>
/// Markup of a dynamic form (<see cref="FormSchema"/>). <c>&lt;app-schema-form&gt;</c> in MVC writes <see cref="Fields"/>. The Blazor
/// <c>AppSchemaForm</c> writes each item's wrapper in Razor (its controls are Blazor elements, which must nest inside the wrapper),
/// with the classes from <see cref="ItemClass"/> and the messages from <see cref="Messages"/>, so both flavours match.
/// </summary>
public static class FormSchemaHtml
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>
    /// The whole body of the form: one item per field, with its control filled from <paramref name="values"/> and the messages
    /// from <paramref name="problems"/> for its key. <paramref name="idPrefix"/> makes the control ids unique on the page.
    /// </summary>
    public static string Fields(FormSchema schema, IReadOnlyDictionary<string, string?> values, IReadOnlyList<FormProblem> problems, string idPrefix)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(problems);
        if (string.IsNullOrWhiteSpace(idPrefix)) throw new ArgumentException("A form needs an id prefix.", nameof(idPrefix));

        var sb = new StringBuilder();
        foreach (var field in schema.Fields)
        {
            var id = idPrefix + "-" + field.Key;
            var errors = Messages(problems, field.Key);
            values.TryGetValue(field.Key, out var value);
            sb.Append(ItemOpen(field, id, errors)).Append(Control(field, id, value, errors.Count > 0)).Append(ItemClose(field, errors));
        }
        return sb.ToString();
    }

    /// <summary>The messages for one field, in the order they were found.</summary>
    public static IReadOnlyList<string> Messages(IReadOnlyList<FormProblem> problems, string key) =>
        problems.Where(p => p.Key == key).Select(p => p.Message).ToList();

    /// <summary>
    /// Opens a form item: the wrapper, the label (required when the field is) and the control's box. The control goes after it,
    /// then <see cref="ItemClose"/>.
    /// </summary>
    public static string ItemOpen(FormField field, string id, IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(field);
        var required = field.Required ? " class=\"ant-form-item-required\"" : "";
        return "<div class=\"" + ItemClass(field, errors) + "\" data-key=\"" + E(field.Key) + "\">"
            + "<div class=\"ant-row ant-form-item-row ant-form-item-row-vertical\">"
            + "<div class=\"ant-form-item-label\"><label for=\"" + E(id) + "\"" + required + ">" + E(field.Label) + "</label></div>"
            + "<div class=\"ant-col ant-form-item-control\"><div class=\"ant-form-item-control-input\"><div class=\"ant-form-item-control-input-content\">";
    }

    /// <summary>The classes of a form item: the error state and whether it has help or errors to show.</summary>
    public static string ItemClass(FormField field, IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(field);
        var error = errors.Count > 0;
        return "ant-form-item" + (error ? " ant-form-item-has-error" : "") + (error || field.Help is not null ? " ant-form-item-with-help" : "");
    }

    /// <summary>Closes a form item opened by <see cref="ItemOpen"/>: the error messages, or else the help text.</summary>
    public static string ItemClose(FormField field, IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(field);
        var explain = errors.Count > 0
            ? "<div class=\"ant-form-item-explain ant-form-item-explain-connected\" role=\"alert\">"
              + string.Concat(errors.Select(e => "<div class=\"ant-form-item-explain-error\">" + E(e) + "</div>")) + "</div>"
            : field.Help is null ? "" : "<div class=\"ant-form-item-explain\"><div>" + E(field.Help) + "</div></div>";
        return "</div></div>" + explain + "</div></div></div>";
    }

    /// <summary>The control of one field, with its name, id, current value and invalid state.</summary>
    public static string Control(FormField field, string id, string? value, bool invalid)
    {
        ArgumentNullException.ThrowIfNull(field);
        var name = " name=\"" + E(field.Key) + "\" id=\"" + E(id) + "\"";
        var aria = invalid ? " aria-invalid=\"true\"" : "";
        var placeholder = Attr("placeholder", field.Placeholder);

        switch (field.Kind)
        {
            case FormFieldKind.Multiline:
                return "<textarea class=\"ant-input\"" + name + " rows=\"3\"" + Attr("maxlength", field.MaxLength?.ToString(CultureInfo.InvariantCulture))
                    + placeholder + aria + ">" + E(value) + "</textarea>";

            case FormFieldKind.Choice:
                var options = (field.Placeholder is null ? "" : "<option value=\"\">" + E(field.Placeholder) + "</option>")
                    + string.Concat(field.Options.Select(o => "<option value=\"" + E(o.Value) + "\"" + (o.Value == value ? " selected" : "") + ">" + E(o.Label) + "</option>"));
                return "<select class=\"m-select\"" + name + aria + ">" + options + "</select>";

            case FormFieldKind.Switch:
                return "<label class=\"ant-checkbox-wrapper\"><span class=\"ant-checkbox\"><input type=\"checkbox\" class=\"ant-checkbox-input\"" + name
                    + " value=\"true\"" + (FormSchema.IsOn(value) ? " checked" : "") + aria + "><span class=\"ant-checkbox-inner\"></span></span><span>"
                    + E(field.Placeholder ?? "Yes") + "</span></label>";

            case FormFieldKind.Money:
                return "<span class=\"ant-input-wrapper ant-input-group\"><span class=\"ant-input-group-addon\">" + E(field.Currency)
                    + "</span>" + NumberInput(field, name, value, aria, placeholder) + "</span>";

            case FormFieldKind.Number:
                return NumberInput(field, name, value, aria, placeholder);

            case FormFieldKind.Date:
                return "<input type=\"date\" class=\"ant-input\"" + name + Attr("value", value) + aria + ">";

            default:
                return "<input type=\"text\" class=\"ant-input\"" + name + Attr("value", value)
                    + Attr("maxlength", field.MaxLength?.ToString(CultureInfo.InvariantCulture)) + placeholder + aria + ">";
        }
    }

    private static string NumberInput(FormField field, string name, string? value, string aria, string placeholder) =>
        "<input type=\"number\" inputmode=\"decimal\" step=\"any\" class=\"ant-input\"" + name + Attr("value", value)
        + Attr("min", field.Min?.ToString(CultureInfo.InvariantCulture)) + Attr("max", field.Max?.ToString(CultureInfo.InvariantCulture))
        + placeholder + aria + ">";

    private static string Attr(string name, string? value) => value is null ? "" : " " + name + "=\"" + E(value) + "\"";
}
