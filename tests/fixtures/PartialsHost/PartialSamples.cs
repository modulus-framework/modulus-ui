using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Assistant;
using Modulus.Ui.Abstractions.Chat;
using Modulus.Ui.Abstractions.Barcode;
using Modulus.Ui.Abstractions.Files;
using Modulus.Ui.Abstractions.Forms;
using Modulus.Ui.Abstractions.Import;
using Modulus.Ui.Abstractions.Query;
using Modulus.Ui.Abstractions.Workflow;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Plugins;

/// <summary>
/// Sample values for the plain Razor Pages partials, by partial name. The partial page applies them when called with
/// <c>?sample=true</c>, so each partial renders with realistic data (the same state its page would give it). A partial with
/// no entry here is rendered with empty data only. Typed models come from the sample apps' builders where one exists.
/// </summary>
public static class PartialSamples
{
    private static Dictionary<string, object?> D(params (string Key, object? Value)[] items) => items.ToDictionary(i => i.Key, i => i.Value);

    private static readonly DateTime Today = new(2026, 11, 15);

    public static IReadOnlyDictionary<string, object?> For(string name) => name switch
    {
        "approval-timeline" => D(("Steps", new List<ApprovalStep>
        {
            new("Submitted", ApprovalState.Done, "Ana", new DateTime(2026, 11, 10, 9, 0, 0)),
            new("Manager review", ApprovalState.Current, "Ben", null, "Waiting for a decision"),
            new("Finance", ApprovalState.Waiting, "Cara"),
        }), ("AriaLabel", "Approval steps")),
        "audit-trail" => D(("Entries", new List<AuditEntry>
        {
            new(1, new DateTime(2026, 11, 14, 10, 30, 0), "Ana", "Edited", "Quantity", "2", "5"),
            new(2, new DateTime(2026, 11, 13, 16, 5, 0), "Ben", "Approved", Note: "Within budget"),
        }), ("Action", null), ("Url", "/audit"), ("AriaLabel", "Audit trail")),
        "card" => D(("Title", "Sales"), ("Body", new HtmlString("<p>Body text</p>")), ("Footer", "Updated today")),
        "chat" => D(("Turns", new List<ChatTurn>
        {
            new(ChatRole.User, "What is still open?"),
            new(ChatRole.Assistant, "Three orders are waiting for approval."),
        }), ("Placeholder", "Ask about orders")),
        "image-cropper" => D(("Id", "hero-crop"), ("Src", "data:image/gif;base64,R0lGODlhAQABAAAAACw="), ("Alt", "Hero"), ("Name", "crop"), ("Ratio", "16/9"), ("Label", "Hero image")),
        "radio-group" => D(("Name", "priority"), ("Label", "Priority"), ("Items", new List<SelectListItem> { new("Low", "low"), new("Normal", "normal"), new("High", "high", true) }),
            ("Buttons", true), ("Vertical", false), ("Help", "Sets the queue order")),
        "alert" => D(("Type", "warning"), ("Title", "Heads up"), ("Message", "Your trial ends in 3 days."), ("Dismissible", true)),
        "approval-card" => D(("Form", "approve-form"), ("Request", new ApprovalRequest("req-1", "purchase.order.approve", "{}", "Approve purchase order PO-2041",
            [new ApprovalField("Supplier", "Acme"), new ApprovalField("Amount", "$1,240")]))),
        "auto-complete" => D(("Name", "city"), ("Label", "City"), ("Value", "Ams"), ("Options", new List<string> { "Amsterdam", "Antwerp" }),
            ("Placeholder", "Search a city"), ("Help", "Type to search")),
        "avatar" => D(("Name", "Ada Park"), ("Size", "lg")),
        "back-top" => D(("VisibilityHeight", 400)),
        "badge" => D(("Status", "active"), ("Label", "Active")),
        "barcode" => D(("Value", "5012345678900"), ("Kind", BarcodeKind.Ean13), ("Height", 80), ("ShowText", true), ("AriaLabel", "Product barcode")),
        "breadcrumb" => D(("Items", new List<object> { new { label = "Home", url = "/" }, new { label = "Sales", url = "/sales" }, new { label = "Orders", url = "" } })),
        "button" => D(("Label", "Save"), ("Variant", "primary"), ("Size", "sm"), ("Type", "submit")),
        "calendar" => D(("Date", Today), ("Fullscreen", false), ("Mode", "month")),
        "carousel" => D(("Slides", new List<object> { "Slide one", "Slide two" }), ("Autoplay", 3000), ("Dots", true)),
        "cascader" => D(("Name", "region"), ("Label", "Region"), ("Value", "eu"), ("Placeholder", "Pick a region"), ("Options", Regions())),
        "checkbox" => D(("Name", "terms"), ("Label", "Accept terms"), ("Value", "yes"), ("Checked", false), ("Help", "Required")),
        "checkbox-group" => D(("Name", "channels"), ("Label", "Channels"), ("Vertical", false), ("Help", "Pick any"),
            ("Items", new List<SelectListItem> { new("Email", "email", true), new("SMS", "sms"), new("Post", "post") })),
        "code-editor" => D(("Name", "sql"), ("Label", "Query"), ("Value", "{ \"ok\": true }"), ("Language", "json"), ("ReadOnly", false), ("Height", "240px")),
        "collapse" => D(("Items", new List<object> { new { Id = "a", Title = "Shipping", Content = "Ships in 2 days." }, new { Id = "b", Title = "Returns", Content = "30 days." } }),
            ("Accordion", true), ("Bordered", true), ("Open", "a")),
        "color-picker" => D(("Name", "brand"), ("Label", "Brand colour"), ("Value", "#0969da")),
        "countdown" => D(("Title", "Sale ends"), ("Target", Today.AddHours(2)), ("Format", "HH:mm:ss")),
        "data-grid" => D(("Columns", new List<GridColumn> { new() { Key = "name", Title = "Name" }, new() { Key = "qty", Title = "Qty" } }),
            ("Rows", new List<GridRecord> { new("1", new Dictionary<string, string?> { ["name"] = "Ada", ["qty"] = "3" }) }),
            ("Title", "Stock"), ("Name", "stock")),
        "data-table" => D(("Columns", new List<(string Title, string Key)> { ("Name", "name"), ("Qty", "qty") }),
            ("Rows", new List<IDictionary<string, object?>> { new Dictionary<string, object?> { ["name"] = "Ada", ["qty"] = 3 } })),
        "date-picker" => D(("Name", "due"), ("Label", "Due date"), ("Value", "2026-11-15"), ("Picker", "date"), ("ShowTime", false), ("Help", "Optional")),
        "date-range-picker" => D(("Name", "period"), ("Label", "Period"), ("NameFrom", "from"), ("NameTo", "to"), ("From", "2026-11-01"), ("To", "2026-11-30")),
        "descriptions" => D(("Title", "Order"), ("Columns", 3), ("Bordered", true),
            ("Items", new List<object> { new { Label = "Customer", Value = "Ada Park", Span = 1 }, new { Label = "Address", Value = "Amsterdam", Span = 2 } })),
        "divider" => D(("Text", "Details"), ("Orientation", "left"), ("Dashed", false), ("Vertical", false)),
        "document" => D(("Name", "body"), ("Id", "doc"), ("Value", "<p>Hello</p>"), ("Label", "Body"), ("ReadOnly", false), ("Height", "320px")),
        "document-upload" => D(("Name", "documents"), ("Documents", new List<KnowledgeDocument> { new("handbook.pdf", 12) })),
        "drawer" => D(("DrawerId", "details"), ("Title", "Details"), ("Placement", "right"), ("Width", 480), ("Body", new HtmlString("<p>Drawer body</p>"))),
        // Every item carries all the properties the template reads (a dynamic member that is missing throws).
        "dropdown" => D(("Label", "Actions"), ("Items", new List<object>
        {
            new { label = "Edit", url = "/edit", divider = false },
            new { label = "", url = "", divider = true },
            new { label = "Delete", url = "/delete", divider = false },
        })),
        "empty-state" => D(("Title", "No orders"), ("Message", "Create one to start."), ("ActionText", "New order"), ("ActionUrl", "/orders/new")),
        "file-manager" => D(("View", FileManagerModel.Build(new FileStore().Entries(), null, null, Today)), ("Url", "/files")),
        "flow-editor" => D(("Name", "flow"), ("Height", 480), ("ReadOnly", false), ("Graph", FlowDemo.SupportDesk())),
        "form-group" => D(("Label", "Email"), ("Required", true), ("Help", "Work email"), ("Content", new HtmlString("<input class=\"ant-input\" />"))),
        "form-input" => D(("Name", "email"), ("Label", "Email"), ("Type", "email"), ("Value", "ada@example.com"), ("Placeholder", "you@example.com"),
            ("Help", "Work email"), ("Required", true)),
        "gantt" => D(("View", GanttPage.Board(Today))),
        "grid" => D(("Gutter", 16), ("Columns", new List<object>
        {
            new { Content = new HtmlString("<div>Left</div>"), Span = 12 },
            new { Content = new HtmlString("<div>Right</div>"), Span = 12 },
        })),
        "image" => D(("Src", "data:image/gif;base64,R0lGODlhAQABAAAAACw="), ("Alt", "Sample"), ("Width", 120), ("Height", 80), ("Preview", true)),
        "input-number" => D(("Name", "qty"), ("Label", "Quantity"), ("Value", 3), ("Min", 0m), ("Max", 10m), ("Step", 1m)),
        "import-wizard" => D(("View", ImportWizardView.Start(CustomerImport.Fields)), ("Action", "/tables/import")),
        "kanban" => D(("Board", KanbanPage.Board(
            [
                new KanbanTask { Id = 1, Column = "todo", Order = 1, Title = "Write the docs", Label = "docs" },
                new KanbanTask { Id = 2, Column = "doing", Order = 1, Title = "Fix the export", Label = "bug" },
                new KanbanTask { Id = 3, Column = "done", Order = 1, Title = "Ship the release", Label = "release" },
            ])), ("Url", "/kanban")),
        "list" => D(("Header", "Recent"), ("Bordered", true), ("Items", new List<object>
        {
            new { Title = "Invoice 1", Description = "Paid", Href = "/invoices/1", AvatarName = "IN" },
            new { Title = "Invoice 2", Description = "Due Friday", Href = "/invoices/2", AvatarName = "IN" },
        })),
        "menu" => D(("Mode", "inline"), ("Selected", "orders"), ("Items", new List<object>
        {
            new { Key = "home", Label = "Home", Url = "/", Icon = "home", Children = (object?)null },
            new { Key = "sales", Label = "Sales", Url = "", Icon = "shop", Children = (object?)new List<object> { new { Key = "orders", Label = "Orders", Url = "/orders" } } },
        })),
        "modal" => D(("ModalId", "confirm"), ("Title", "Confirm"), ("Size", "sm"), ("Body", new HtmlString("<p>Are you sure?</p>"))),
        "money-input" => D(("Name", "price"), ("Label", "Price"), ("Value", "12.50"), ("Currency", "EUR")),
        "otp-input" => D(("Name", "code"), ("Label", "Code"), ("Length", 6), ("Numeric", true)),
        "page-header" => D(("Title", "Orders"), ("Subtitle", "All orders this month"), ("Pretitle", "Sales"), ("Body", new HtmlString("<button>New</button>"))),
        "pagination" => D(("CurrentPage", 2), ("TotalPages", 5), ("PageSize", 10), ("BaseUrl", "/orders")),
        "pdf-button" => D(("Form", "invoice-form"), ("Action", "/invoices/pdf"), ("Label", "Download PDF")),
        "pdf-viewer" => D(("Url", "/docs/sample.pdf"), ("Id", "pdf"), ("Label", "Sample"), ("Page", 1), ("Height", "480px")),
        "pivot-grid" => D(("Result", PivotTablePage.Load(new ShipmentFilter(), []).Result), ("Label", "Shipments"), ("DownloadHref", "/shipments/pivot.xlsx")),
        "popconfirm" => D(("Title", "Delete?"), ("Label", "Delete"), ("OkText", "Yes"), ("Url", "/rows/1"), ("Danger", true)),
        "popover" => D(("Title", "Hint"), ("Content", "More information"), ("Label", "Details"), ("Trigger", "click"), ("Placement", "bottom")),
        "progress" => D(("Percentage", 40), ("Color", "success"), ("ShowLabel", true)),
        "query-builder" => D(("Schema", QueryPage().Schema), ("Filter", QueryPage().Filter), ("Action", "/tables/query")),
        "qr-code" => D(("Value", "https://modulus.dev"), ("Size", 160), ("Bordered", true)),
        "rate" => D(("Name", "stars"), ("Label", "Rating"), ("Value", 4), ("Count", 5), ("Help", "Out of five")),
        "rich-text-editor" => D(("Name", "notes"), ("Label", "Notes"), ("Value", "<p>Hi</p>"), ("Placeholder", "Write here"), ("MinHeight", 200)),
        "result" => D(("Status", "success"), ("Title", "Done"), ("SubTitle", "Your changes are saved."), ("Body", new HtmlString("<a href=\"/\">Back</a>"))),
        "saved-views" => D(("Page", "orders"), ("Id", "views")),
        "scheduler" => D(("View", CalendarPage.Load(new SampleStore(), CalendarPage.Query(null, null, null, false, Today), Today)), ("Url", "/calendar")),
        "schema-form" => D(("Schema", DynamicFormDemo.Schema), ("Values", new Dictionary<string, string?>())),
        "search-input" => D(("Name", "q"), ("Label", "Search"), ("Value", "order"), ("Placeholder", "Find an order"), ("Help", "Searches all fields")),
        "segmented" => D(("Name", "range"), ("Label", "Range"), ("Value", "week"), ("Block", false), ("Items", new List<string> { "day", "week", "month" })),
        "select" => D(("Name", "country"), ("Label", "Country"), ("Placeholder", "Pick a country"), ("Multiple", false), ("Help", "Shipping country"),
            ("Items", new List<SelectListItem> { new("Netherlands", "nl", true), new("Belgium", "be"), new("Germany", "de") })),
        "skeleton" => D(("Rows", 3), ("Avatar", false), ("Loading", true)),
        "slider" => D(("Name", "volume"), ("Label", "Volume"), ("Value", 50), ("Min", 0m), ("Max", 100m), ("Step", 5m)),
        "space" => D(("Size", "small"), ("Vertical", false), ("Body", new HtmlString("<span>a</span><span>b</span>"))),
        "spinner" => D(("Size", "lg"), ("Label", "Loading")),
        "splitter" => D(("Height", 200), ("Vertical", false), ("Panels", new List<object>
        {
            new { Content = new HtmlString("<p>Left</p>"), Size = "30%", Min = (string?)null, Max = (string?)null },
            new { Content = new HtmlString("<p>Right</p>"), Size = (string?)null, Min = (string?)null, Max = (string?)null },
        })),
        "spreadsheet" => D(("Name", "sheet"), ("Id", "sheet"), ("Label", "Stock"), ("ReadOnly", false), ("Height", "300px"),
            ("Sheets", new List<SpreadsheetSheet> { new("Sheet1", new List<IReadOnlyList<object?>> { new List<object?> { "Name", "Qty" }, new List<object?> { "Ada", 3 } }) })),
        "stat-card" => D(("Label", "Revenue"), ("Value", "12.4k"), ("Change", "+12%")),
        "statistic" => D(("Title", "Users"), ("Value", 1280), ("Suffix", "users"), ("Precision", 0)),
        "status-badge" => D(("Text", "Shipped"), ("Tone", "success")),
        "steps" => D(("Current", 1), ("Vertical", false), ("Items", new List<object>
        {
            new { Title = "Cart", Description = "Items added" },
            new { Title = "Pay", Description = "Card" },
            new { Title = "Done", Description = "Shipped" },
        })),
        "switch" => D(("Name", "active"), ("Label", "Active"), ("Checked", true), ("OnText", "On"), ("OffText", "Off"), ("Help", "Shows the account")),
        "tabs" => D(("Tabs", new List<object>
        {
            new { id = "one", label = "One", content = "First tab" },
            new { id = "two", label = "Two", content = "Second tab" },
        })),
        "tag" => D(("Text", "New"), ("Color", "blue"), ("Closable", true)),
        "tag-input" => D(("Name", "tags"), ("Label", "Tags"), ("Max", 5), ("Placeholder", "Add a tag"), ("Help", "Up to five")),
        "textarea" => D(("Name", "bio"), ("Label", "Bio"), ("Value", "Hello"), ("Placeholder", "Tell us more"), ("Rows", 3), ("ShowCount", true), ("Help", "Short")),
        "time-picker" => D(("Name", "at"), ("Label", "Time"), ("Format", "HH:mm"), ("Help", "24-hour")),
        "timeline" => D(("Pending", "Delivery"), ("Reverse", false), ("Items", new List<object>
        {
            new { Text = "Created", Color = "green", Label = "Mon", Icon = (string?)null },
            new { Text = "Shipped", Color = "blue", Label = "Tue", Icon = (string?)null },
        })),
        "tooltip" => D(("Text", "More information"), ("Label", "Hover me"), ("Placement", "top")),
        "transfer" => D(("Name", "perms"), ("Label", "Permissions"), ("Help", "Pick what the role can do"),
            ("Options", new List<TreeOption> { new("read", "Read"), new("write", "Write") }), ("TargetKeys", new List<string> { "read" })),
        "tree" => D(("Options", Regions()), ("Checkable", true), ("ExpandAll", true)),
        "tree-select" => D(("Name", "dept"), ("Label", "Department"), ("Value", "nl"), ("Placeholder", "Pick a region"), ("Checkable", false), ("Options", Regions())),
        "upload" => D(("Name", "file"), ("Label", "Attachment"), ("Accept", ".pdf"), ("Multiple", true), ("Drag", true), ("Text", "Drop files here"),
            ("Hint", "PDF up to 5 MB"), ("MaxSize", 5.0), ("MaxCount", 3), ("Action", "/upload"), ("Help", "Optional")),
        _ => new Dictionary<string, object?>(),
    };

    private static Modulus.Samples.Shared.QueryPage QueryPage() => Modulus.Samples.Shared.QueryPage.Load(new FilterGroup(FilterJoin.And, []));

    private static List<TreeOption> Regions() =>
    [
        new("eu", "Europe", [new TreeOption("nl", "Netherlands"), new TreeOption("be", "Belgium")]),
        new("us", "Americas", [new TreeOption("ca", "Canada", null, true)]),
    ];
}
