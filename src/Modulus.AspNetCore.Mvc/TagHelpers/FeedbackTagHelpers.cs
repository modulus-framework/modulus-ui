using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Alert: <c>&lt;app-alert type="warning" message="Your trial ends in 3 days" show-icon closable /&gt;</c>.
/// Child content becomes the description; <c>&lt;alert-action&gt;</c> renders on the right.
/// </summary>
[HtmlTargetElement("app-alert")]
public class AlertTagHelper : TagHelper
{
    /// <summary>success | info | warning | error</summary>
    public string Type { get; set; } = "info";

    public string? Message { get; set; }

    /// <summary>Plain-text description (or use child content).</summary>
    public string? Description { get; set; }

    public bool ShowIcon { get; set; }
    public bool Closable { get; set; }

    /// <summary>Full-width page banner (icon on, no border).</summary>
    public bool Banner { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new Dictionary<string, string>();
        context.Items[typeof(AlertTagHelper)] = slots;
        var child = (await output.GetChildContentAsync()).GetContent();
        var description = string.IsNullOrWhiteSpace(child) ? (Description is null ? null : Html.E(Description)) : child;
        var icon = ShowIcon || Banner;
        var glyph = Type switch { "success" => "check-circle", "warning" => "exclamation-circle", "error" => "close-circle", _ => "info-circle" };

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("role", Type is "error" or "warning" ? "alert" : "status");
        output.AddClasses("ant-alert", $"ant-alert-{Type}", description is null ? null : "ant-alert-with-description", icon ? null : "ant-alert-no-icon", Banner ? "ant-alert-banner" : null, Closable ? "ant-alert-closable" : null);
        if (Closable)
        {
            output.Attributes.SetAttribute("x-data", "{ show: true }");
            output.Attributes.SetAttribute("x-show", "show");
            output.Attributes.SetAttribute("x-transition.opacity.duration.200ms", "");
        }
        output.Content.SetHtmlContent(
            (icon ? AntIcons.Span(glyph, description is null ? IconThemeType.Fill : IconThemeType.Outline, "ant-alert-icon") : "") +
            """<div class="ant-alert-content">""" +
            (Message is null ? "" : $"""<div class="ant-alert-message">{Html.E(Message)}</div>""") +
            (description is null ? "" : $"""<div class="ant-alert-description">{description}</div>""") + "</div>" +
            (slots.TryGetValue("alert-action", out var action) ? $"""<div class="ant-alert-action">{action}</div>""" : "") +
            (Closable ? $"""<button type="button" class="ant-alert-close-icon" aria-label="Close" @click="show = false">{AntIcons.Span("close")}</button>""" : ""));
    }
}

[HtmlTargetElement("alert-action", ParentTag = "app-alert")]
public class AlertActionTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(AlertTagHelper), out var slots))
            ((Dictionary<string, string>)slots)["alert-action"] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}

/// <summary>
/// Progress bar or ring: <c>&lt;app-progress percent="70" /&gt;</c>, <c>type="circle"</c>, <c>type="dashboard"</c>,
/// <c>status="exception"</c>. Reaching 100 switches to <c>success</c>.
/// </summary>
[HtmlTargetElement("app-progress", TagStructure = TagStructure.WithoutEndTag)]
public class ProgressTagHelper : TagHelper
{
    public double Percent { get; set; }

    /// <summary>line | circle | dashboard</summary>
    public string Type { get; set; } = "line";

    /// <summary>normal | active | success | exception</summary>
    public string? Status { get; set; }

    /// <summary>default | small</summary>
    public string Size { get; set; } = "default";

    public bool ShowInfo { get; set; } = true;

    /// <summary>Circle / dashboard diameter in pixels.</summary>
    public int Width { get; set; } = 120;

    /// <summary>Line thickness in pixels (line) or percent of the diameter (circle).</summary>
    public int? StrokeWidth { get; set; }

    public string? StrokeColor { get; set; }

    /// <summary>Text format, e.g. "{0}%" or "{0} days".</summary>
    public string Format { get; set; } = "{0}%";

    /// <summary>Accessible name, e.g. "Storage used".</summary>
    public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var p = Math.Clamp(Percent, 0, 100);
        var status = Status ?? (p >= 100 ? "success" : "normal");
        var inv = CultureInfo.InvariantCulture;
        var line = Type == "line";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-progress", $"ant-progress-status-{status}", $"ant-progress-{(Size == "small" ? "small" : "default")}", $"ant-progress-{(line ? "line" : "circle")}", ShowInfo ? "ant-progress-show-info" : null);
        output.Attributes.SetAttribute("role", "progressbar");
        output.Attributes.SetAttribute("aria-valuenow", p.ToString("0.##", inv));
        output.Attributes.SetAttribute("aria-valuemin", "0");
        output.Attributes.SetAttribute("aria-valuemax", "100");
        if (AriaLabel is not null) output.Attributes.SetAttribute("aria-label", AriaLabel);

        var text = string.Format(inv, Format, p.ToString("0.##", inv));
        var info = !ShowInfo ? "" : status switch
        {
            "success" => $"""<span class="ant-progress-text">{AntIcons.Span(line ? "check-circle" : "check", line ? IconThemeType.Fill : IconThemeType.Outline)}</span>""",
            "exception" => $"""<span class="ant-progress-text">{AntIcons.Span(line ? "close-circle" : "close", line ? IconThemeType.Fill : IconThemeType.Outline)}</span>""",
            _ => $"""<span class="ant-progress-text" title="{Html.E(text)}">{Html.E(text)}</span>""",
        };

        if (line)
        {
            var height = StrokeWidth ?? (Size == "small" ? 6 : 8);
            var color = StrokeColor is null ? "" : $"background:{Html.E(StrokeColor)};";
            output.Content.SetHtmlContent(
                $"""<div class="ant-progress-outer"><div class="ant-progress-inner"><div class="ant-progress-bg" style="{color}width:{p.ToString("0.##", inv)}%;height:{height}px"></div></div></div>{info}""");
            return;
        }

        // rc-progress geometry: 100×100 viewBox, radius 50 − stroke/2; the dashboard leaves a 75° gap at the bottom.
        var stroke = StrokeWidth ?? 6;
        var r = 50 - stroke / 2.0;
        var len = 2 * Math.PI * r;
        var dashboard = Type == "dashboard";
        var gap = dashboard ? 75 / 360.0 * len : 0;
        var arc = dashboard
            ? $"M 50,50 m 0,{F(r)} a {F(r)},{F(r)} 0 1 1 0,-{F(2 * r)} a {F(r)},{F(r)} 0 1 1 0,{F(2 * r)}"
            : $"M 50,50 m 0,-{F(r)} a {F(r)},{F(r)} 0 1 1 0,{F(2 * r)} a {F(r)},{F(r)} 0 1 1 0,-{F(2 * r)}";
        var offset = dashboard ? $"stroke-dashoffset:-{F(gap / 2)}px;" : "stroke-dashoffset:0;";
        var pathColor = StrokeColor is null ? "" : $"stroke:{Html.E(StrokeColor)};";
        var filled = p / 100 * (len - gap);
        output.Content.SetHtmlContent(
            $"""<div class="ant-progress-inner" style="width:{Width}px;height:{Width}px;font-size:{F(Width * 0.15 + 6)}px">""" +
            $"""<svg class="ant-progress-circle" viewBox="0 0 100 100" aria-hidden="true">""" +
            $"""<path class="ant-progress-circle-trail" d="{arc}" stroke-linecap="round" stroke-width="{stroke}" fill-opacity="0" style="stroke-dasharray:{F(len - gap)}px, {F(len)}px;{offset}"></path>""" +
            $"""<path class="ant-progress-circle-path" d="{arc}" stroke-linecap="round" stroke-width="{stroke}" fill-opacity="0" opacity="{(p == 0 ? 0 : 1)}" style="{pathColor}stroke-dasharray:{F(filled)}px, {F(len)}px;{offset}transition:stroke-dashoffset .3s ease 0s, stroke-dasharray .3s ease 0s, stroke .3s, stroke-width .06s ease .3s, opacity .3s ease 0s"></path>""" +
            $"""</svg>{info}</div>""");

        static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Result page body: <c>&lt;app-result status="success" title="Order placed" sub-title="…"&gt;&lt;result-extra&gt;…buttons…&lt;/result-extra&gt;&lt;/app-result&gt;</c>.
/// Child content (outside result-extra) renders in the grey content box.
/// </summary>
[HtmlTargetElement("app-result")]
public class ResultTagHelper : TagHelper
{
    /// <summary>success | info | warning | error</summary>
    public string Status { get; set; } = "info";

    public string? Title { get; set; }
    public string? SubTitle { get; set; }

    /// <summary>Custom icon name.</summary>
    public string? Icon { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new Dictionary<string, string>();
        context.Items[typeof(ResultTagHelper)] = slots;
        var content = (await output.GetChildContentAsync()).GetContent();
        var glyph = Icon ?? Status switch { "success" => "check-circle", "warning" => "warning", "error" => "close-circle", _ => "exclamation-circle" };
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-result", $"ant-result-{Html.E(Status)}");
        output.Content.SetHtmlContent(
            $"""<div class="ant-result-icon">{AntIcons.Span(glyph, IconThemeType.Fill)}</div>""" +
            (Title is null ? "" : $"""<div class="ant-result-title">{Html.E(Title)}</div>""") +
            (SubTitle is null ? "" : $"""<div class="ant-result-subtitle">{Html.E(SubTitle)}</div>""") +
            (slots.TryGetValue("result-extra", out var extra) ? $"""<div class="ant-result-extra">{extra}</div>""" : "") +
            (string.IsNullOrWhiteSpace(content) ? "" : $"""<div class="ant-result-content">{content}</div>"""));
    }
}

[HtmlTargetElement("result-extra", ParentTag = "app-result")]
public class ResultExtraTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(ResultTagHelper), out var slots))
            ((Dictionary<string, string>)slots)["result-extra"] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}

/// <summary>
/// Loading placeholder: <c>&lt;app-skeleton active avatar rows="3" /&gt;</c>. With <c>loading="false"</c> the
/// child content renders instead, so the same markup works before and after data arrives.
/// </summary>
[HtmlTargetElement("app-skeleton")]
public class SkeletonTagHelper : TagHelper
{
    public bool Active { get; set; }
    public bool Avatar { get; set; }
    public bool Title { get; set; } = true;
    public int? Rows { get; set; }
    public bool Round { get; set; }
    public bool Loading { get; set; } = true;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (!Loading)
        {
            output.TagName = null;
            output.Content.SetHtmlContent((await output.GetChildContentAsync()).GetContent());
            return;
        }
        var rows = Rows ?? (Avatar ? 2 : 3);
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-skeleton", Avatar ? "ant-skeleton-with-avatar" : null, Active ? "ant-skeleton-active" : null, Round ? "ant-skeleton-round" : null);
        output.Attributes.SetAttribute("aria-busy", "true");
        output.Attributes.SetAttribute("aria-label", "Loading");
        var sb = new StringBuilder();
        if (Avatar) sb.Append("""<div class="ant-skeleton-header"><span class="ant-skeleton-avatar ant-skeleton-avatar-lg ant-skeleton-avatar-circle"></span></div>""");
        sb.Append("""<div class="ant-skeleton-content">""");
        if (Title) sb.Append($"""<h3 class="ant-skeleton-title" style="width:{(Avatar ? 50 : 38)}%"></h3>""");
        sb.Append("""<ul class="ant-skeleton-paragraph">""");
        for (var i = 0; i < rows; i++) sb.Append(i == rows - 1 && rows > 1 ? """<li style="width:61%"></li>""" : "<li></li>");
        output.Content.SetHtmlContent(sb.Append("</ul></div>").ToString());
    }
}

/// <summary>
/// Spinner. Around content it dims the content while <c>spinning</c>:
/// <c>&lt;app-spin tip="Loading…" spinning="@Model.Busy"&gt;…&lt;/app-spin&gt;</c>.
/// Add <c>class="htmx-indicator"</c> to a bare spinner to show it only while an htmx request runs.
/// </summary>
[HtmlTargetElement("app-spin")]
public class SpinTagHelper : TagHelper
{
    /// <summary>small | default | large</summary>
    public string Size { get; set; } = "default";

    public string? Tip { get; set; }
    public bool Spinning { get; set; } = true;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var child = (await output.GetChildContentAsync()).GetContent();
        var spin = $"""<div class="{Html.Cx("ant-spin", Spinning ? "ant-spin-spinning" : null, Size == "default" ? null : $"ant-spin-{(Size == "small" ? "sm" : "lg")}", Tip is null ? null : "ant-spin-show-text")}" role="status" aria-live="polite">""" +
                   """<span class="ant-spin-dot ant-spin-dot-spin"><i class="ant-spin-dot-item"></i><i class="ant-spin-dot-item"></i><i class="ant-spin-dot-item"></i><i class="ant-spin-dot-item"></i></span>""" +
                   (Tip is null ? """<span class="m-sr-only">Loading</span>""" : $"""<div class="ant-spin-text">{Html.E(Tip)}</div>""") + "</div>";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        if (string.IsNullOrWhiteSpace(child))
        {
            output.AddClasses("m-spin");
            output.Content.SetHtmlContent(spin);
            return;
        }
        output.AddClasses("ant-spin-nested-loading");
        output.Content.SetHtmlContent(
            (Spinning ? $"<div>{spin}</div>" : "") +
            $"""<div class="{Html.Cx("ant-spin-container", Spinning ? "ant-spin-blur" : null)}"{(Spinning ? " aria-busy=\"true\"" : "")}>{child}</div>""");
    }
}

/// <summary>
/// Dialog. Open it from anywhere with <c>$dispatch('modulus:modal-open', 'invite')</c> (Alpine) or
/// <c>modulusModal.open('invite')</c>; close with <c>Response.HxCloseModal()</c> after an htmx submit.
/// <code>
/// &lt;app-modal name="invite" title="Invite teammates" ok-text="Send invites" form="invite-form"&gt;
///   &lt;form id="invite-form" hx-post="/team/invite" hx-target="this"&gt;…&lt;/form&gt;
/// &lt;/app-modal&gt;
/// </code>
/// With <c>form</c>, OK submits that form; otherwise OK raises <c>modulus:modal-ok</c> and closes.
/// Replace the footer with <c>&lt;modal-footer&gt;</c>, or hide it with <c>footer="false"</c>.
/// </summary>
[HtmlTargetElement("app-modal")]
public class ModalTagHelper : TagHelper
{
    public string Name { get; set; } = "";
    public string? Title { get; set; }
    public int Width { get; set; } = 520;
    public string OkText { get; set; } = "OK";
    public string CancelText { get; set; } = "Cancel";
    public bool OkDanger { get; set; }

    /// <summary>id of the form the OK button submits.</summary>
    public string? Form { get; set; }

    public bool Footer { get; set; } = true;

    /// <summary>Vertically centred.</summary>
    public bool Centered { get; set; }

    /// <summary>Clicking the mask closes the dialog.</summary>
    public bool MaskClosable { get; set; } = true;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new Dictionary<string, string>();
        context.Items[typeof(ModalTagHelper)] = slots;
        var body = (await output.GetChildContentAsync()).GetContent();
        var titleId = $"{context.UniqueId}-title";
        var ok = Form is not null
            ? $"""<button type="submit" form="{Html.E(Form)}" class="{Html.Cx("ant-btn", "ant-btn-primary", OkDanger ? "ant-btn-dangerous" : null)}"><span>{Html.E(OkText)}</span></button>"""
            : $"""<button type="button" class="{Html.Cx("ant-btn", "ant-btn-primary", OkDanger ? "ant-btn-dangerous" : null)}" @click="ok()"><span>{Html.E(OkText)}</span></button>""";
        var footer = slots.TryGetValue("modal-footer", out var f) ? f
            : Footer ? $"""<button type="button" class="ant-btn ant-btn-default" @click="hide()"><span>{Html.E(CancelText)}</span></button>{ok}"""
            : null;

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-modal", Centered ? "m-modal-centered" : null);
        output.Attributes.SetAttribute("x-data", $"modulusModal('{Html.E(Name)}')");
        output.Attributes.SetAttribute("x-show", "open");
        output.Attributes.SetAttribute("x-cloak", "");
        output.Attributes.SetAttribute("@keydown.escape.window", "open && hide()");
        output.Content.SetHtmlContent(
            """<div class="ant-modal-mask" x-show="open" x-transition.opacity></div>""" +
            $"""<div class="ant-modal-wrap" role="dialog" aria-modal="true"{(Title is null ? "" : $" aria-labelledby=\"{titleId}\"")}{(MaskClosable ? " @click.self=\"hide()\"" : "")}>""" +
            $"""<div class="ant-modal" style="width:{Width}px" x-show="open" x-transition:enter="m-zoom-enter" x-transition:enter-start="m-zoom-from" x-transition:leave="m-zoom-enter" x-transition:leave-end="m-zoom-from"><div class="ant-modal-content">""" +
            $"""<button type="button" class="ant-modal-close" aria-label="Close" @click="hide()"><span class="ant-modal-close-x">{AntIcons.Span("close", IconThemeType.Outline, "ant-modal-close-icon")}</span></button>""" +
            (Title is null ? "" : $"""<div class="ant-modal-header"><div class="ant-modal-title" id="{titleId}">{Html.E(Title)}</div></div>""") +
            $"""<div class="ant-modal-body">{body}</div>""" +
            (footer is null ? "" : $"""<div class="ant-modal-footer">{footer}</div>""") + "</div></div></div>");
    }
}

[HtmlTargetElement("modal-footer", ParentTag = "app-modal")]
public class ModalFooterTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(ModalTagHelper), out var slots))
            ((Dictionary<string, string>)slots)["modal-footer"] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}

/// <summary>Repeating text watermark over its content: <c>&lt;app-watermark content="Confidential"&gt;…&lt;/app-watermark&gt;</c>.</summary>
[HtmlTargetElement("app-watermark")]
public class WatermarkTagHelper : TagHelper
{
    public string Content { get; set; } = "";
    public int Rotate { get; set; } = -22;
    public int FontSize { get; set; } = 16;
    public string Color { get; set; } = "rgba(0,0,0,0.12)";
    public int GapX { get; set; } = 100;
    public int GapY { get; set; } = 100;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var width = Content.Length * FontSize * 0.6 + GapX;
        var height = FontSize * 1.5 + GapY;
        var inv = CultureInfo.InvariantCulture;
        var svg = $"""<svg xmlns="http://www.w3.org/2000/svg" width="{width.ToString("0", inv)}" height="{height.ToString("0", inv)}"><text x="50%" y="50%" text-anchor="middle" dominant-baseline="middle" transform="rotate({Rotate} {(width / 2).ToString("0", inv)} {(height / 2).ToString("0", inv)})" font-family="-apple-system, 'Segoe UI', Roboto, sans-serif" font-size="{FontSize}" fill="{Html.E(Color)}">{Html.E(Content)}</text></svg>""";
        var uri = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-watermark");
        output.PostContent.SetHtmlContent($"""<div class="m-watermark-layer" aria-hidden="true" style="background-image:url('{uri}')"></div>""");
    }
}
