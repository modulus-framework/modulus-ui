using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using Fluid;
using Fluid.Values;

namespace Modulus.AspNetCore.Fluid;

/// <summary>
/// Liquid templates for Modulus pages and documents (Fluid). Every value a template prints is HTML-escaped, unless it is a
/// <see cref="LiquidHtml"/>. A page is a body template in a layout template: the body is rendered first and given to the layout as
/// <c>content</c>. The tags <c>{% modulus_head %}</c>, <c>{% icon "name" %}</c> and <c>{% barcode "value" %}</c>, and the filter
/// <c>| status_badge: "tone"</c>, expand to the same markup as the Modulus components.
/// </summary>
public sealed class ModulusLiquid
{
    private readonly FluidParser _parser = new();
    private readonly ConcurrentDictionary<string, (DateTime Written, IFluidTemplate Template)> _files = new(StringComparer.Ordinal);

    public ModulusLiquid() : this(new ModulusLiquidOptions()) { }

    public ModulusLiquid(ModulusLiquidOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        ModulusLiquidTags.Register(_parser, options);
        ModulusLiquidTags.RegisterFilters(options);
    }

    public ModulusLiquidOptions Options { get; }

    /// <summary>Parses a template; throws <see cref="ArgumentException"/> with the parser's message when it is not valid Liquid.</summary>
    public IFluidTemplate Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!TryParseTemplate(source, out var template, out var error)) throw new ArgumentException("The template is not valid Liquid: " + error, nameof(source));
        return template;
    }

    public bool TryParseTemplate(string source, out IFluidTemplate template, out string? error)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _parser.TryParse(source, out template, out error);
    }

    /// <summary>A template file, parsed once and parsed again when the file changes on disk.</summary>
    public IFluidTemplate Load(string path)
    {
        var full = Path.GetFullPath(path);
        var written = File.GetLastWriteTimeUtc(full);
        if (_files.TryGetValue(full, out var cached) && cached.Written == written) return cached.Template;
        var template = Parse(File.ReadAllText(full));
        _files[full] = (written, template);
        return template;
    }

    /// <summary>The template's output, with each value escaped (a <see cref="LiquidHtml"/> is printed as it is).</summary>
    public string Render(IFluidTemplate template, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        return template.Render(NewContext(values), HtmlEncoder.Default, false);
    }

    /// <summary>
    /// The page body rendered, then given to <paramref name="layout"/> as <c>content</c> (printed unescaped there, so the layout
    /// writes <c>{{ content }}</c> once and it is the body's markup).
    /// </summary>
    public string RenderPage(IFluidTemplate page, IFluidTemplate layout, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var body = Render(page, values);
        var withContent = new Dictionary<string, object?>(values, StringComparer.Ordinal) { ["content"] = new LiquidHtml(body) };
        return Render(layout, withContent);
    }

    private TemplateContext NewContext(IReadOnlyDictionary<string, object?> values)
    {
        var context = new TemplateContext(Options.Template);
        foreach (var (key, value) in values)
            context.SetValue(key, value is LiquidHtml html ? new StringValue(html.Html, false) : FluidValue.Create(value, Options.Template));
        return context;
    }
}
