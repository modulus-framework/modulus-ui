using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Forms;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// A dynamic form drawn from a <see cref="FormSchema"/>: one Ant Design form item per field, with the values and the messages
/// from <see cref="FormSchema.Validate"/>. Posted fields are named by their key, so the server reads them as a dictionary.
/// The markup is the Blazor <c>AppSchemaForm</c>'s (both use <see cref="FormSchemaHtml"/>).
/// <code>&lt;app-schema-form schema="@Model.Schema" values="@Model.Values" problems="@Model.Problems" id-prefix="expense" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-schema-form", TagStructure = TagStructure.WithoutEndTag)]
public class SchemaFormTagHelper : TagHelper
{
    /// <summary>The form's fields and rules.</summary>
    public FormSchema? Schema { get; set; }

    /// <summary>The values by field key (what was posted, or empty for a new form).</summary>
    public IReadOnlyDictionary<string, string?>? Values { get; set; }

    /// <summary>The messages to show, from <see cref="FormSchema.Validate"/>.</summary>
    public IReadOnlyList<FormProblem>? Problems { get; set; }

    /// <summary>Prefix of the control ids (each field's id is "<c>id-prefix</c>-<c>key</c>").</summary>
    public string IdPrefix { get; set; } = "schema";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Schema is null) throw new InvalidOperationException("app-schema-form needs a schema.");

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-schema-form");
        output.Attributes.SetAttribute("id", IdPrefix);
        output.Content.SetHtmlContent(FormSchemaHtml.Fields(Schema, Values ?? new Dictionary<string, string?>(), Problems ?? [], IdPrefix));
    }
}
