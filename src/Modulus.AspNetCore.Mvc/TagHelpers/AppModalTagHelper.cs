using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Renders an Ant Design Modal using Alpine.js for state management.
/// </summary>
[HtmlTargetElement("app-modal")]
public class AppModalTagHelper : TagHelper
{
    [HtmlAttributeName("title")]
    public string Title { get; set; } = "Modal";

    [HtmlAttributeName("ok-text")]
    public string OkText { get; set; } = "OK";

    [HtmlAttributeName("cancel-text")]
    public string CancelText { get; set; } = "Cancel";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "ant-modal-wrap");

        var preHtml = $@"
            <div class=""ant-modal ant-modal-centered""
                 style=""display: block;""
                 x-show=""visible""
                 x-transition
                 @click=""if ($event.target === $el) close()"">
                <div class=""ant-modal-content"" @click.stop>
                    <div class=""ant-modal-header"">
                        <div class=""ant-modal-title"">{{{{ title }}}}</div>
                        <button class=""ant-modal-close"" @click=""close()"" type=""button"">
                            <span class=""ant-modal-close-x""></span>
                        </button>
                    </div>
                    <div class=""ant-modal-body"">
        ";

        var postHtml = $@"
                    </div>
                    <div class=""ant-modal-footer"">
                        <button class=""ant-btn ant-btn-default"" @click=""close()"" :disabled=""loading"">{CancelText}</button>
                        <button class=""ant-btn ant-btn-primary""
                                @click=""handleSubmit()""
                                :disabled=""loading""
                                :class=""{{{{ 'ant-btn-loading': loading }}}}"">
                            {OkText}
                        </button>
                    </div>
                </div>
            </div>
            <div class=""ant-modal-mask"" x-show=""visible"" x-transition x-transition.opacity></div>
        ";

        output.PreContent.AppendHtml(preHtml);
        output.PostContent.AppendHtml(postHtml);
    }
}
