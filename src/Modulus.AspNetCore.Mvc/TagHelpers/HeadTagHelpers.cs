using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// <c>&lt;modulus-head /&gt;</c> in &lt;head&gt;: the official Ant Design stylesheets (the same files
/// Blazor loads), the Modulus design layer and shell, generated brand tokens and the theme and
/// shell scripts. Theme cookies are read on the server, so the right stylesheet, colour scheme and
/// radius are active from the first byte.
/// </summary>
[HtmlTargetElement("modulus-head", TagStructure = TagStructure.WithoutEndTag)]
public class ModulusHeadTagHelper(IOptions<ModulusThemeOptions> options) : TagHelper
{
    [ViewContext, HtmlAttributeNotBound] public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var cookies = ViewContext.HttpContext.Request.Cookies;
        var theme = ThemeCss.ResolveOptions(options.Value, name => cookies[name]);
        var mode = ThemeCss.ResolveMode(cookies[ThemeCss.ModeCookie], theme.DefaultMode);
        var dark = mode == "dark";
        var @default = theme.DefaultMode switch { ThemeMode.Dark => "dark", ThemeMode.Light => "light", _ => "" };

        output.TagName = null;
        output.Content.SetHtmlContent($"""
            <link rel="preload" href="/_content/Modulus.Ui.Assets/fonts/inter/inter-latin-wght-normal.woff2" as="font" type="font/woff2" crossorigin />
            <link rel="stylesheet" href="/_content/AntDesign/css/ant-design-blazor.variable.min.css" data-theme-css="light" media="{(dark ? "not all" : "all")}" />
            <link rel="stylesheet" href="/_content/AntDesign/css/ant-design-blazor.dark.min.css" data-theme-css="dark" media="{(dark ? "all" : "not all")}" />
            <link rel="stylesheet" href="/_content/Modulus.Ui.Assets/css/modulus.css" />
            <link rel="stylesheet" href="/_content/Modulus.Ui.Assets/css/modulus-shell.css" />
            <link rel="stylesheet" href="/_content/Modulus.Ui.Assets/css/modulus-pages.css" />
            <style id="modulus-brand">{ThemeCss.Build(theme)}</style>
            <script src="/_content/Modulus.Ui.Assets/js/modulus-theme.js" data-default="{@default}" {ThemeCss.ScriptAttributes(theme)}></script>
            <script src="/_content/Modulus.Ui.Assets/js/modulus-shell.js" defer></script>
            """);
    }
}

/// <summary>
/// <c>&lt;modulus-scripts /&gt;</c> before &lt;/body&gt;: pinned htmx and Alpine.js (served locally,
/// no CDN) plus the Modulus MVC runtime (drawer, dropdowns, toasts, confirm dialogs).
/// </summary>
[HtmlTargetElement("modulus-scripts", TagStructure = TagStructure.WithoutEndTag)]
public class ModulusScriptsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.Content.SetHtmlContent("""
            <script src="/_content/Modulus.Ui.Assets/lib/htmx/htmx.min.js"></script>
            <script src="/_content/Modulus.Ui.Assets/js/modulus-mvc.js"></script>
            <script src="/_content/Modulus.Ui.Assets/js/modulus-widgets.js"></script>
            <script defer src="/_content/Modulus.Ui.Assets/lib/alpinejs/alpine.min.js"></script>
            """);
    }
}

/// <summary>
/// Adds <c>hx-headers</c> with the antiforgery token to an element (put it on &lt;body&gt;):
/// <c>&lt;body modulus-antiforgery&gt;</c>. Every htmx request then passes [ValidateAntiForgeryToken].
/// </summary>
[HtmlTargetElement(Attributes = "modulus-antiforgery")]
public class AntiforgeryHeadersTagHelper(IAntiforgery antiforgery) : TagHelper
{
    [ViewContext, HtmlAttributeNotBound] public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("modulus-antiforgery");
        var tokens = antiforgery.GetAndStoreTokens(ViewContext.HttpContext);
        output.Attributes.SetAttribute("hx-headers", $$"""{"{{tokens.HeaderName}}":"{{tokens.RequestToken}}"}""");
    }
}
