using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.Ui.Abstractions.Shell;
public static partial class ShellRenderer
{
    // Footer ----------------------------------------------------------------------------------------------

    public static string Footer(ShellView view)
    {
        var o = view.Options;
        var sb = new StringBuilder("""<footer class="m-footer"><div class="m-container m-footer-inner">""");
        if (view.Content.FooterLinks.Count > 0)
        {
            sb.Append("""<ul class="m-footer-links">""");
            foreach (var link in view.Content.FooterLinks)
                sb.Append($"""<li><a href="{Url(link.Href)}">{(link.Icon is null ? "" : Icon(link.Icon))}{E(link.Label)}</a></li>""");
            sb.Append("</ul>");
        }
        sb.Append("""<ul class="m-footer-meta">""");
        if (o.Copyright is { } copyright) sb.Append($"<li>Copyright © {E(copyright)}. All rights reserved.</li>");
        if (o.Version is { } version) sb.Append($"""<li><span class="m-footer-version">{E(version)}</span></li>""");
        sb.Append("</ul></div></footer>");
        return sb.ToString();
    }

}
