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

/// <summary>More actions in the batch bar (they act on the checked rows: <c>input[data-select-row]:checked</c>).</summary>
[HtmlTargetElement("grid-batch", ParentTag = "app-editable-grid")]
public class GridBatchTagHelper : GridSlotTagHelper { }
