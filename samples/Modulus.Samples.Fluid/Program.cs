using Modulus.AspNetCore.Fluid;
using Modulus.AspNetCore.Hosting;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;

// Liquid pages on the Modulus UI: the templates in Templates/ are a layout and two pages, rendered with Fluid. Every value is
// escaped; the tags {% modulus_head %}, {% icon %} and {% barcode %} and the | status_badge filter draw the Modulus components.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SampleStore>();
builder.Services.AddModulusHardening();
// Invoice and its lines are model types here, so the templates may read their public properties (the rest stays closed).
builder.Services.AddSingleton(_ => new ModulusLiquid(new ModulusLiquidOptions().Register<Invoice>().Register<InvoiceLine>()));

var app = builder.Build();
var templates = Path.Combine(app.Environment.ContentRootPath, "Templates");

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

app.Run();

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
