using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using Fluid;
using Fluid.Ast;
using Fluid.Values;
using Modulus.Ui.Abstractions.Barcode;
using Modulus.Ui.Abstractions.Shell;
using Modulus.Ui.Abstractions.Workflow;

namespace Modulus.AspNetCore.Fluid;

/// <summary>
/// The Modulus tags and filter of a Liquid template. Each one writes the markup the matching Modulus component draws, so a document
/// or page built from templates looks like the rest of the UI. Arguments are the template's own values; a tag refuses what it cannot
/// draw (an unknown icon name, a value the barcode kind cannot encode) by throwing, the same as the other Modulus renderers.
/// </summary>
internal static class ModulusLiquidTags
{
    /// <summary>{% modulus_head %}: the stylesheets and the theme and shell scripts a Modulus page needs, under the asset prefixes.</summary>
    internal static void Register(FluidParser parser, ModulusLiquidOptions options)
    {
        parser.RegisterEmptyTag("modulus_head", (writer, encoder, context) =>
        {
            writer.Write(Head(options));
            return new ValueTask<Completion>(Completion.Normal);
        });

        // {% icon "name" %}: an Ant Design outline icon, drawn inline.
        parser.RegisterExpressionTag("icon", async (expression, writer, encoder, context) =>
        {
            var name = (await expression.EvaluateAsync(context)).ToStringValue();
            var svg = AntIconSvg.Svg(name) ?? throw new ArgumentException($"There is no Ant Design icon called \"{name}\".", nameof(name));
            writer.Write(svg);
            return Completion.Normal;
        });

        // {% barcode "value" %}: a Code 128 symbol as SVG (the MVC app-barcode and the Blazor AppBarcode draw the same).
        parser.RegisterExpressionTag("barcode", async (expression, writer, encoder, context) =>
        {
            var value = (await expression.EvaluateAsync(context)).ToStringValue();
            writer.Write(BarcodeSvg.Render(BarcodeKind.Code128, value));
            return Completion.Normal;
        });
    }

    /// <summary>
    /// | money: the amount with a thousands separator and two decimals (<c>| money: 0</c> for whole units, up to 6 places), in the
    /// invariant culture, so a template prints the same figures on every server.
    /// </summary>
    internal static void RegisterMoney(ModulusLiquidOptions options)
    {
        options.Template.Filters.AddFilter("money", (input, arguments, context) =>
        {
            var places = arguments.Count > 0 ? Math.Clamp((int)arguments.At(0).ToNumberValue(), 0, 6) : 2;
            var text = input.ToNumberValue().ToString("N" + places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            return new ValueTask<FluidValue>(new StringValue(text, true));
        });
    }

    /// <summary>| status_badge: "tone": the record's status as a tinted pill. The text is escaped; the tone is checked.</summary>
    internal static void RegisterFilters(ModulusLiquidOptions options)
    {
        RegisterMoney(options);
        options.Template.Filters.AddFilter("status_badge", (input, arguments, context) =>
        {
            var text = WebUtility.HtmlEncode(input.ToStringValue());
            var tone = arguments.At(0).ToStringValue();
            return new ValueTask<FluidValue>(new StringValue(StatusBadgeHtml.Render(text, tone), false));
        });
    }

    private static string Head(ModulusLiquidOptions options)
    {
        var a = options.AssetPrefix;
        var ant = options.AntDesignPrefix;
        var sb = new StringBuilder();
        sb.Append($"<link rel=\"stylesheet\" href=\"{ant}css/ant-design-blazor.variable.min.css\" data-theme-css=\"light\" />\n");
        sb.Append($"<link rel=\"stylesheet\" href=\"{ant}css/ant-design-blazor.dark.min.css\" data-theme-css=\"dark\" media=\"not all\" />\n");
        sb.Append($"<link rel=\"stylesheet\" href=\"{a}css/antd-dark-brand.css\" data-theme-css=\"dark\" media=\"not all\" />\n");
        sb.Append($"<link rel=\"stylesheet\" href=\"{a}css/modulus.css\" />\n");
        sb.Append($"<link rel=\"stylesheet\" href=\"{a}css/modulus-shell.css\" />\n");
        sb.Append($"<link rel=\"stylesheet\" href=\"{a}css/modulus-pages.css\" />\n");
        sb.Append($"<script src=\"{a}js/modulus-theme.js\"></script>\n");
        sb.Append($"<script src=\"{a}js/modulus-shell.js\" defer></script>\n");
        sb.Append($"<script src=\"{a}js/modulus-plugins.js\" defer></script>\n");
        return sb.ToString();
    }
}
