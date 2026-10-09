using System.Net;
using System.Text;

namespace Modulus.Ui.Abstractions.Plugins;

/// <summary>
/// The code editor's markup, shared by the Blazor <c>AppCodeEditor</c> and the MVC <c>app-code-editor</c>. CodeMirror 6
/// (modulus-plugins.js) mounts the editor into the host; until it runs, the host shows the code as plain text. The text lives
/// in a hidden input (<c>data-m-code-value</c>): the editor starts from it and writes each pause in typing back with a change
/// event, so a form posts it and Blazor binds it. Blazor renders the root and the hidden input itself and uses
/// <see cref="Host"/> for the host.
/// </summary>
public static class CodeEditorHtml
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>The languages the editor highlights; <c>""</c> is plain text.</summary>
    public static IReadOnlyList<string> Languages { get; } = ["", "javascript", "typescript", "json", "css", "html"];

    /// <summary>The root element's opening tag. <paramref name="height"/> is the editor's CSS height.</summary>
    public static string RootAttributes(string id, string? label, string language, bool readOnly, string height, string? cssClass = null)
    {
        CheckLanguage(language);
        return $"""<div class="m-code{(cssClass is { Length: > 0 } c ? " " + E(c) : "")}" id="{E(id)}" data-m-code data-language="{E(language)}"{(readOnly ? " data-readonly" : "")} data-label="{E(label ?? "Code")}" style="--m-code-height: {E(height)}">""";
    }

    /// <summary>The editor's host: the code as plain text until the editor replaces it.</summary>
    public static string Host(string? code) =>
        $"""<div class="m-code-host" data-m-code-host><pre class="m-code-fallback">{E(code)}</pre></div>""";

    /// <summary>The whole editor for MVC: the root, the host and a hidden input named <paramref name="name"/> that holds the text.</summary>
    public static string Editor(string id, string name, string? code, string language = "", string? label = null, bool readOnly = false,
        string height = "360px", string? cssClass = null)
    {
        var hidden = $"""<input type="hidden" name="{E(name)}" id="{E(id)}-value" value="{E(code)}" data-m-code-value />""";
        return RootAttributes(id, label, language, readOnly, height, cssClass) + Host(code) + hidden + "</div>";
    }

    private static void CheckLanguage(string language)
    {
        if (!Languages.Contains(language)) throw new ArgumentException($"A language is one of: {string.Join(", ", Languages.Where(l => l.Length > 0))}.", nameof(language));
    }
}
