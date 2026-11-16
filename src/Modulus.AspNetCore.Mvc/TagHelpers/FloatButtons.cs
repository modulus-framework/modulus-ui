using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Qr;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>Shared markup of the float buttons.</summary>
internal static class FloatButtons
{
    public static string Classes(string type, string shape) =>
        $"ant-float-btn ant-float-btn-{(type == "primary" ? "primary" : "default")} ant-float-btn-{(shape == "square" ? "square" : "circle")}";

    public static string Body(string? icon, string? description, string? tooltip, int? badgeCount, bool badgeDot)
    {
        var sb = new StringBuilder("""<div class="ant-float-btn-body"><div class="ant-float-btn-content">""");
        if (icon is not null || description is null) sb.Append($"""<div class="ant-float-btn-icon">{AntIcons.Span(icon ?? "file-text")}</div>""");
        if (description is not null) sb.Append($"""<div class="ant-float-btn-description">{Html.E(description)}</div>""");
        sb.Append("</div></div>");
        if (badgeDot) sb.Append("""<sup class="ant-scroll-number ant-badge-dot m-float-btn-badge"></sup>""");
        else if (badgeCount > 0) sb.Append($"""<sup class="ant-scroll-number ant-badge-count m-float-btn-badge">{(badgeCount > 99 ? "99+" : badgeCount.Value.ToString(CultureInfo.InvariantCulture))}</sup>""");
        if (tooltip is not null) sb.Append($"""<span class="m-float-btn-tip" aria-hidden="true">{Html.E(tooltip)}</span>""");
        return sb.ToString();
    }
}
