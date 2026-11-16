using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Plugins;
using SortDirection = Modulus.Ui.Abstractions.SortDirection;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

public partial class EditableGridTagHelper : TagHelper
{
    // ---- Column UI ----

    private string ColumnSettings()
    {
        var n = Html.E(Name);
        var sb = new StringBuilder();
        for (var i = 0; i < Columns.Count; i++)
        {
            var c = Columns[i];
            var key = Html.E(c.Key);
            var title = Html.E(c.Title);
            var shown = !c.Hidden;
            sb.Append($"""<li class="m-grid-columns-item"><label class="{Html.Cx("ant-checkbox-wrapper", shown ? "ant-checkbox-wrapper-checked" : null)}"><span class="{Html.Cx("ant-checkbox", shown ? "ant-checkbox-checked" : null)}"><input type="checkbox" class="ant-checkbox-input"{(shown ? " checked" : "")} @change="submitOp('toggle:{key}')" /><span class="ant-checkbox-inner"></span></span><span>{title}</span></label>""");
            if (AllowColumnEdit)
                sb.Append("""<span class="m-grid-columns-tools">""")
                  .Append(Tool($"pin:{key}:{(c.Fixed == GridFixed.Left ? "none" : "left")}", "vertical-align-top", -90, $"Pin {title} to the left", c.Fixed == GridFixed.Left, false))
                  .Append(Tool($"pin:{key}:{(c.Fixed == GridFixed.Right ? "none" : "right")}", "vertical-align-top", 90, $"Pin {title} to the right", c.Fixed == GridFixed.Right, false))
                  .Append(Tool($"move:{key}:-1", "arrow-up", 0, $"Move {title} up", false, i == 0))
                  .Append(Tool($"move:{key}:1", "arrow-down", 0, $"Move {title} down", false, i == Columns.Count - 1))
                  .Append("</span>");
            sb.Append("</li>");
        }
        return $$"""<div class="m-dropdown" x-data="dropdown" @click.outside="close()" @keydown.escape="close()"><div class="m-dropdown-trigger ant-dropdown-trigger" @click="toggle()" :class="open && 'ant-dropdown-open'" aria-haspopup="dialog" :aria-expanded="open"><button type="button" class="ant-btn ant-btn-default m-table-columns-toggle" aria-label="Columns" title="Show, hide, pin and order columns">{{AntIcons.Span("setting")}}<span>Columns</span></button></div>""" +
               $$"""<div class="ant-popover ant-popover-placement-bottomRight m-dropdown-panel m-dropdown-right m-grid-columns-popover" x-show="open" x-transition.opacity x-cloak><div class="ant-popover-content"><div class="ant-popover-inner" role="dialog" aria-label="Columns"><div class="ant-popover-title"><div class="m-grid-columns-head"><span>Columns</span><span class="m-muted">{{Columns.Count(c => !c.Hidden)}} of {{Columns.Count}} shown</span></div></div>""" +
               $$"""<div class="ant-popover-inner-content"><ul class="m-grid-columns" role="list">{{sb}}</ul></div></div></div></div></div>""";

        string Tool(string op, string icon, int rotate, string label, bool active, bool disabled) =>
            $"""<button type="submit" name="{n}.op" value="{op}" class="{Html.Cx("m-grid-tool", active ? "m-grid-tool-active" : null)}" aria-label="{label}" title="{label}"{(active ? " aria-pressed=\"true\"" : "")}{(disabled ? " disabled" : "")}>{Rotated(icon, rotate)}</button>""";
    }

    private static string Rotated(string icon, int rotate) => rotate == 0
        ? AntIcons.Span(icon)
        : AntIcons.Span(icon).Replace("<svg ", $"<svg style=\"transform: rotate({rotate}deg);\" ", StringComparison.Ordinal);

    // One floating menu for every column header: a menu inside the scrolling table would be clipped.
    private string ColumnMenu()
    {
        var n = Html.E(Name);
        string Item(string op, string icon, string label, string show = "true", string extra = "") =>
            $"""<li role="none" class="{Html.Cx("ant-dropdown-menu-item", extra)}" x-show="{show}"><button type="submit" role="menuitem" name="{n}.op" :value="{op}" :disabled="!menu.key"><span class="ant-dropdown-menu-title-content">{AntIcons.Span(icon)}<span>{label}</span></span></button></li>""";
        return """<div class="ant-dropdown ant-dropdown-placement-bottomRight m-grid-menu" x-show="menu.key" x-transition.opacity :style="menu.style" style="display: none;" @click.outside="closeMenu()">""" +
               """<ul class="ant-dropdown-menu ant-dropdown-menu-root ant-dropdown-menu-vertical ant-dropdown-menu-light" role="menu">""" +
               Item("'pin:' + menu.key + ':left'", "pushpin", "Pin left", "menu.fixed !== 'left'") +
               Item("'pin:' + menu.key + ':right'", "pushpin", "Pin right", "menu.fixed !== 'right'") +
               Item("'pin:' + menu.key + ':none'", "close", "Unpin", "menu.fixed !== 'none'") +
               """<li class="ant-dropdown-menu-item-divider" role="separator"></li>""" +
               Item("'move:' + menu.key + ':-1'", "arrow-left", "Move left", "!menu.first") +
               Item("'move:' + menu.key + ':1'", "arrow-right", "Move right", "!menu.last") +
               Item("'hide:' + menu.key", "eye-invisible", "Hide column") +
               """<li class="ant-dropdown-menu-item-divider" role="separator"></li>""" +
               Item("'remove:' + menu.key", "delete", "Remove column", "true", "ant-dropdown-menu-item-danger") +
               "</ul></div>";
    }

    private string ColumnDialog(string gridId)
    {
        var n = Html.E(Name);
        var titleId = $"{gridId}-add-column";
        var types = string.Concat(Enum.GetValues<GridCellType>().Select(t =>
            $"""<option value="{t}">{(t switch { GridCellType.MultiSelect => "Multi-select", GridCellType.Tag => "Tag (status)", _ => t.ToString() })}</option>"""));
        string Radio(string value, string label, bool on) =>
            $$"""<label class="{{Html.Cx("ant-radio-button-wrapper", on ? "ant-radio-button-wrapper-checked" : null)}}" :class="{ 'ant-radio-button-wrapper-checked': newFixed === '{{value}}' }"><span class="{{Html.Cx("ant-radio-button", on ? "ant-radio-button-checked" : null)}}" :class="{ 'ant-radio-button-checked': newFixed === '{{value}}' }"><input type="radio" class="ant-radio-button-input" name="{{n}}.new.fixed" value="{{value}}"{{(on ? " checked" : "")}} x-model="newFixed" /><span class="ant-radio-button-inner"></span></span><span>{{label}}</span></label>""";
        return $$"""<div class="m-modal" x-show="columnOpen" x-cloak style="display: none;" @keydown.escape.window="columnOpen = false">""" +
               """<div class="ant-modal-mask" x-show="columnOpen" x-transition.opacity></div>""" +
               $$"""<div class="ant-modal-wrap" role="dialog" aria-modal="true" aria-labelledby="{{titleId}}" @click.self="columnOpen = false"><div class="ant-modal" style="width: 480px;"><div class="ant-modal-content">""" +
               $$"""<button type="button" class="ant-modal-close" aria-label="Close" @click="columnOpen = false"><span class="ant-modal-close-x">{{AntIcons.Span("close", IconThemeType.Outline, "ant-modal-close-icon")}}</span></button>""" +
               $$"""<div class="ant-modal-header"><div class="ant-modal-title" id="{{titleId}}">Add column</div></div><div class="ant-modal-body"><div class="ant-form ant-form-vertical">""" +
               $$"""<div class="ant-form-item"><div class="ant-form-item-label"><label for="{{titleId}}-title" class="ant-form-item-required">Title</label></div><input type="text" id="{{titleId}}-title" class="ant-input" name="{{n}}.new.title" maxlength="60" placeholder="e.g. Region" x-model="newTitle" x-ref="newTitle" @keydown.enter.prevent="newTitle.trim() && submitOp('add-column')" /></div>""" +
               $$"""<div class="ant-form-item"><div class="ant-form-item-label"><label for="{{titleId}}-type">Type</label></div><select id="{{titleId}}-type" class="m-select" name="{{n}}.new.type" x-model="newType">{{types}}</select></div>""" +
               $$"""<div class="ant-form-item" x-show="newType === 'Select' || newType === 'MultiSelect' || newType === 'Tag'" style="display: none;"><div class="ant-form-item-label"><label for="{{titleId}}-options">Options</label></div><textarea id="{{titleId}}-options" class="ant-input" name="{{n}}.new.options" rows="3" placeholder="One per line, or comma-separated"></textarea></div>""" +
               $$"""<div class="ant-form-item"><div class="ant-form-item-label"><label>Pin</label></div><div><div class="ant-radio-group ant-radio-group-solid">{{Radio("None", "None", true)}}{{Radio("Left", "Left", false)}}{{Radio("Right", "Right", false)}}</div></div></div>""" +
               $$"""<div class="ant-form-item" style="margin-bottom: 0"><label class="ant-checkbox-wrapper" :class="{ 'ant-checkbox-wrapper-checked': newRequired }"><span class="ant-checkbox" :class="{ 'ant-checkbox-checked': newRequired }"><input type="checkbox" class="ant-checkbox-input" name="{{n}}.new.required" value="true" x-model="newRequired" /><span class="ant-checkbox-inner"></span></span><span>Required</span></label></div>""" +
               $$"""</div></div><div class="ant-modal-footer"><button type="button" class="ant-btn ant-btn-default" @click="columnOpen = false"><span>Cancel</span></button><button type="submit" name="{{n}}.op" value="add-column" class="ant-btn ant-btn-primary" :disabled="!newTitle.trim()"><span>Add column</span></button></div>""" +
               "</div></div></div></div>";
    }
}
