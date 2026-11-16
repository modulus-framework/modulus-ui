using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// File upload: a button, or a drag-and-drop area with <c>drag</c>. Selected files are listed and can be
/// removed before submit (the enclosing form needs <c>enctype="multipart/form-data"</c>).
/// <code>&lt;app-upload name="files" drag multiple accept=".pdf,image/*" hint="PDF or images up to 10 MB" /&gt;</code>
/// With <c>action</c> each file is posted there as soon as it is picked (field <c>name</c>, antiforgery header
/// included) and shows its progress. <c>max-size</c> (MB), <c>max-count</c> and <c>accept</c> reject files in the
/// browser with a message; <c>list-type="picture"</c> shows image thumbnails.
/// </summary>
[HtmlTargetElement("app-upload")]
public class UploadTagHelper : TagHelper
{
    public string Name { get; set; } = "file";
    public bool Multiple { get; set; }
    public string? Accept { get; set; }
    public bool Drag { get; set; }
    public bool Disabled { get; set; }

    /// <summary>Main text (drag area) or button text.</summary>
    public string? Text { get; set; }

    /// <summary>Secondary text in the drag area.</summary>
    public string? Hint { get; set; }

    /// <summary>text | picture</summary>
    public string ListType { get; set; } = "text";

    /// <summary>Largest file in megabytes.</summary>
    public double? MaxSize { get; set; }

    /// <summary>Most files in the list.</summary>
    public int? MaxCount { get; set; }

    /// <summary>URL each file is posted to as soon as it is added; unset keeps the files in the form.</summary>
    public string? Action { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var custom = (await output.GetChildContentAsync()).GetContent();
        var extra = output.TakeAttributes();
        var picture = ListType == "picture";
        var input = $"""<input type="file" class="m-upload-input" x-ref="input" @change="sync()" name="{Html.E(Name)}"{Html.Attr("multiple", Multiple)}{Html.Attr("accept", Accept)}{Html.Attr("disabled", Disabled)}{extra} />""";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-upload-wrapper", "m-upload");
        var inv = CultureInfo.InvariantCulture;
        output.Attributes.SetAttribute("x-data", MaxSize is null && MaxCount is null && Action is null && !picture && Accept is null
            ? "modulusUpload"
            : "modulusUpload({ " + string.Join(", ", new[]
            {
                MaxSize is { } mb ? $"maxSize: {(mb * 1024 * 1024).ToString("0", inv)}" : null,
                MaxCount is { } n ? $"maxCount: {n.ToString(inv)}" : null,
                Accept is null ? null : $"accept: '{Js(Accept)}'",
                Action is null ? null : $"action: '{Js(Action)}'",
                picture ? "picture: true" : null,
            }.Where(x => x is not null)) + " })");

        // Values go into a single-quoted JS string inside an attribute that SetAttribute encodes.
        static string Js(string value) => value.Replace("\\", "", StringComparison.Ordinal).Replace("'", "", StringComparison.Ordinal);

        string trigger;
        if (Drag)
        {
            var body = string.IsNullOrWhiteSpace(custom)
                ? $"""<p class="ant-upload-drag-icon">{AntIcons.Span("inbox")}</p><p class="ant-upload-text">{Html.E(Text ?? "Click or drag files to this area to upload")}</p>""" +
                  (Hint is null ? "" : $"""<p class="ant-upload-hint">{Html.E(Hint)}</p>""")
                : custom;
            trigger =
                $"""<div class="{Html.Cx("ant-upload", "ant-upload-drag", Disabled ? "ant-upload-disabled" : null)}" :class="over && 'ant-upload-drag-hover'" @dragover.prevent="over = true" @dragleave="over = false" @drop.prevent="drop($event)">""" +
                $"""<label class="ant-upload ant-upload-btn">{input}<div class="ant-upload-drag-container">{body}</div></label></div>""";
        }
        else
        {
            var body = string.IsNullOrWhiteSpace(custom) ? $"""{AntIcons.Span("upload")}<span>{Html.E(Text ?? "Click to upload")}</span>""" : custom;
            trigger = $"""<div class="ant-upload ant-upload-select"><label class="{Html.Cx("ant-btn", "ant-btn-default", Disabled ? "m-disabled" : null)}">{input}{body}</label></div>""" +
                      (Hint is null ? "" : $"""<div class="m-upload-hint">{Html.E(Hint)}</div>""");
        }

        var type = picture ? "picture" : "text";
        var icon = picture
            ? $"""<span class="ant-upload-list-item-thumbnail"><img x-show="file.url" :src="file.url" :alt="file.name" class="ant-upload-list-item-image" /><span x-show="!file.url">{AntIcons.Span("file")}</span></span>"""
            : $"""<div class="ant-upload-text-icon">{AntIcons.Span("paper-clip")}</div>""";
        output.Content.SetHtmlContent(trigger +
            $"""<div class="ant-upload-list ant-upload-list-{type}" aria-live="polite"><template x-for="(file, i) in files" :key="file.key"><div class="ant-upload-list-{type}-container">""" +
            $"""<div class="ant-upload-list-item ant-upload-list-item-list-type-{type}" :class="'ant-upload-list-item-' + file.status">""" +
            $"""<div class="ant-upload-list-item-info"><span class="ant-upload-span">{icon}<span class="ant-upload-list-item-name" x-text="file.name" :title="file.error || file.name"></span>""" +
            """<span class="m-upload-size" x-text="file.size"></span>""" +
            $"""<span class="ant-upload-list-item-card-actions"><button type="button" class="ant-btn ant-btn-text ant-btn-sm ant-btn-icon-only ant-upload-list-item-card-actions-btn" :aria-label="'Remove ' + file.name" @click="remove(i)">{AntIcons.Span("delete")}</button></span></span></div>""" +
            """<div class="ant-upload-list-item-progress" x-show="file.status === 'uploading'"><div class="ant-progress ant-progress-line ant-progress-status-active ant-progress-small" role="progressbar" :aria-valuenow="file.percent" aria-valuemin="0" aria-valuemax="100" :aria-label="'Uploading ' + file.name">""" +
            """<div class="ant-progress-outer"><div class="ant-progress-inner"><div class="ant-progress-bg" :style="'width:' + file.percent + '%;height:2px'"></div></div></div></div></div>""" +
            """<div class="m-upload-error" x-show="file.error" x-text="file.error"></div>""" +
            """</div></div></template></div>""");
    }
}
