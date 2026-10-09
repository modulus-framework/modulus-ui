using Modulus.AspNetCore.Fluid;
using Modulus.AspNetCore.Hosting;
using Modulus.Documents;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;

// Liquid pages on the Modulus UI: the templates in Templates/ are a layout and two pages, rendered with Fluid. Every value is
// escaped; the tags {% modulus_head %}, {% icon %} and {% barcode %} and the | status_badge filter draw the Modulus components.
// QuestPDF needs the licence tier that applies to the host before the first PDF. Community is free for individuals and businesses
// under USD 1,000,000 in annual gross revenue, and not for public-sector bodies or publicly traded companies: choose the tier for your use.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SampleStore>();
builder.Services.AddModulusHardening();
builder.Services.AddModulusObservability();
// Invoice and its lines are model types here, so the templates may read their public properties (the rest stays closed).
builder.Services.AddSingleton(_ => new ModulusLiquid(new ModulusLiquidOptions().Register<Invoice>().Register<InvoiceLine>()));

var app = builder.Build();
var templates = Path.Combine(app.Environment.ContentRootPath, "Templates");

// Outside Development an unhandled exception gets the sample's error page with a 500 status, not the framework's bare response,
// and never the exception itself.
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler(errors => errors.Run(async context =>
    {
        var liquid = context.RequestServices.GetRequiredService<ModulusLiquid>();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Page(liquid, templates, "error", "Server error", new Dictionary<string, object?>()).ExecuteAsync(context);
    }));

app.UseModulusHardening();
app.MapModulusHealth();
app.MapStaticAssets();

app.MapGet("/", (SampleStore store, ModulusLiquid liquid) =>
{
    var page = store.QueryInvoices(new InvoiceQuery(), new ProTableRequest { PageSize = 50 });
    var rows = page.Data.Select(i => new Dictionary<string, object?>
    {
        ["id"] = i.Id,
        ["number"] = i.Number,
        ["company"] = i.ClientCompany,
        ["total"] = i.Total,
        ["status"] = StatusText(i.Status),
        ["tone"] = StatusTone(i.Status),
    }).ToList();
    return Page(liquid, templates, "index", "Invoices", new Dictionary<string, object?> { ["rows"] = rows });
});

app.MapGet("/invoices/{id:int}", (int id, SampleStore store, ModulusLiquid liquid) =>
{
    var invoice = store.FindInvoice(id);
    if (invoice is null) return Results.NotFound();
    return Page(liquid, templates, "invoice", invoice.Number, new Dictionary<string, object?>
    {
        ["invoice"] = invoice,
        ["status"] = StatusText(invoice.Status),
        ["tone"] = StatusTone(invoice.Status),
    });
});

// The same invoice as a document: the Liquid template in Templates/document-invoice.liquid renders HTML, which Modulus.Documents
// reads into blocks and writes as a PDF or a Word file. The merged PDF is the mail merge: one invoice per page of the list.
app.MapGet("/documents/invoices/{id:int}.pdf", (int id, SampleStore store, ModulusLiquid liquid) =>
{
    var invoice = store.FindInvoice(id);
    if (invoice is null) return Results.NotFound();
    return Results.File(DocumentPdf.WriteHtml(InvoiceHtml(liquid, templates, invoice)), DocumentPdf.ContentType, invoice.Number + ".pdf");
});

app.MapGet("/documents/invoices/{id:int}.docx", (int id, SampleStore store, ModulusLiquid liquid) =>
{
    var invoice = store.FindInvoice(id);
    if (invoice is null) return Results.NotFound();
    return Results.File(DocumentDocx.WriteHtml(InvoiceHtml(liquid, templates, invoice)), DocumentDocx.ContentType, invoice.Number + ".docx");
});

app.MapGet("/documents/mail-merge.pdf", (SampleStore store, ModulusLiquid liquid) =>
{
    var page = store.QueryInvoices(new InvoiceQuery(), new ProTableRequest { PageSize = 50 });
    var letters = page.Data.Select(i => HtmlBlocks.FromHtml(InvoiceHtml(liquid, templates, i))).ToList();
    return Results.File(DocumentPdf.WriteMerged(letters), DocumentPdf.ContentType, "invoices.pdf");
});

app.Run();

static string InvoiceHtml(ModulusLiquid liquid, string templates, Invoice invoice) =>
    liquid.Render(liquid.Load(Path.Combine(templates, "document-invoice.liquid")), new Dictionary<string, object?>
    {
        ["invoice"] = invoice,
        ["status"] = StatusText(invoice.Status),
    });

static IResult Page(ModulusLiquid liquid, string templates, string page, string title, Dictionary<string, object?> values)
{
    values["title"] = title;
    var html = liquid.RenderPage(
        liquid.Load(Path.Combine(templates, page + ".liquid")),
        liquid.Load(Path.Combine(templates, "_layout.liquid")),
        values);
    return Results.Content(html, "text/html; charset=utf-8");
}

static string StatusText(InvoiceStatus status) => status switch
{
    InvoiceStatus.PartialPayment => "Part paid",
    InvoiceStatus.PastDue => "Past due",
    _ => status.ToString(),
};

// The tone of the status badge (the Modulus tones: success, processing, error, warning, default).
static string StatusTone(InvoiceStatus status) => status switch
{
    InvoiceStatus.Paid => "success",
    InvoiceStatus.Sent => "processing",
    InvoiceStatus.PastDue => "error",
    InvoiceStatus.PartialPayment => "warning",
    _ => "default",
};
