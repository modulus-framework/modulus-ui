using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Forms;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// Business fields of ERP forms (Odoo, SAP, Oracle): money with its currency, quantity with its unit, durations,
// number ranges, phone numbers, masked codes, one-time codes, lookups with a "Search more" dialog, the workflow
// status bar, a signature pad and rich text. The Blazor versions live in Modulus.Blazor/Components/Fields and
// render the same DOM; the formatting rules are Modulus.Ui.Abstractions.Forms, mirrored in modulus-shell.js.
// Each field posts through a hidden input holding the invariant value ("1234.5", minutes, "+44 20 …"), so the
// model binds whatever the page shows. Money, quantity, duration, range, phone and lookup run on the Alpine
// components in modulus-widgets.js; masks, one-time codes, signatures and rich text on modulus-shell.js.


internal static class BusinessFields
{
    public static string Down() => $"""<span class="m-addon-select-arrow" aria-hidden="true">{AntIcons.Span("down")}</span>""";

    public static string Hidden(string name, string? value, string xRef, string? bind) =>
        $"""<input type="hidden"{Html.Attr("name", name.Length > 0 ? name : null)} value="{Html.E(value)}" x-ref="{xRef}"{(bind is null ? "" : $" :value=\"{bind}\"")} />""";

    public static string Invalid(string? status) => status == "error" ? " aria-invalid=\"true\"" : "";

    public static string Inv(decimal? value) => NumberText.Invariant(value);
}
