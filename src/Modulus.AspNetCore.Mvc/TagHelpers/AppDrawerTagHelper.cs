using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Renders an Ant Design Drawer using Alpine.js for state management.
/// </summary>
[HtmlTargetElement("app-drawer")]
public class AppDrawerTagHelper : TagHelper
{
    [HtmlAttributeName("title")]
    public string Title { get; set; } = "Drawer";

    [HtmlAttributeName("placement")]
    public string Placement { get; set; } = "right";

    [HtmlAttributeName("width")]
    public int Width { get; set; } = 360;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "ant-drawer-wrapper");

        var placementClass = Placement switch
        {
            "left" => "ant-drawer-content-wrapper-left",
            "right" => "ant-drawer-content-wrapper-right",
            "top" => "ant-drawer-content-wrapper-top",
            "bottom" => "ant-drawer-content-wrapper-bottom",
            _ => "ant-drawer-content-wrapper-right"
        };

        var drawerStyle = Placement switch
        {
            "left" => $"width: {Width}px; left: 0;",
            "right" => $"width: {Width}px; right: 0;",
            "top" => $"height: {Width}px; top: 0;",
            "bottom" => $"height: {Width}px; bottom: 0;",
            _ => $"width: {Width}px; right: 0;"
        };

        var preHtml = $@"
            <div class=""ant-drawer {placementClass}""
                 style=""{drawerStyle} z-index: 1050;""
                 x-show=""visible""
                 x-transition>
                <div class=""ant-drawer-content"">
                    <div class=""ant-drawer-header"">
                        <div class=""ant-drawer-title"">{{{{ title }}}}</div>
                        <button class=""ant-drawer-close"" @click=""close()"" type=""button""><span class=""ant-modal-close-x""></span></button>
                    </div>
                    <div class=""ant-drawer-body"">
        ";

        var postHtml = @"
                    </div>
                    <div class=""ant-drawer-footer"">
                        <button class=""ant-btn ant-btn-default"" @click=""close()"">Close</button>
                    </div>
                </div>
            </div>
            <div class=""ant-drawer-mask"" x-show=""visible"" x-transition x-transition.opacity></div>
        ";

        output.PreContent.AppendHtml(preHtml);
        output.PostContent.AppendHtml(postHtml);
    }
}
