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

/// <summary>Your own filter row (replaces the built-in one; the nested grid filters on the server this way).</summary>
[HtmlTargetElement("grid-filters", ParentTag = "app-editable-grid")]
public class GridFiltersTagHelper : GridSlotTagHelper { }
