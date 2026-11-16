using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
// Sign-in is the sample's cookie (see SampleAuth): the same users and password as the other samples. The forms carry an antiforgery token.
builder.Services.AddAuthentication(SampleAuth.Scheme).AddCookie(SampleAuth.Scheme, options =>
{
    options.LoginPath = SampleAuth.LoginPath;
    options.LogoutPath = SampleAuth.LogoutPath;
    options.AccessDeniedPath = SampleAuth.DeniedPath;
    options.ReturnUrlParameter = "returnUrl"; // the sign-in page reads returnUrl
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();
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
app.UseAuthentication();
app.UseAuthorization();
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

// Sign-in, sign-out and the password pages. Each POST checks the antiforgery token first; a bad token is a 400.
app.MapGet("/auth/login", (HttpContext context, IAntiforgery antiforgery, ModulusLiquid liquid) =>
    AuthPage(liquid, templates, "auth/login", "Sign in", context, antiforgery, new Dictionary<string, object?>
    {
        ["reset"] = context.Request.Query["reset"].ToString() == "1",
        ["return_url"] = SampleAuth.SafeReturnUrl(context.Request.Query["returnUrl"].ToString()),
    }));

app.MapPost("/auth/login", async (HttpContext context, IAntiforgery antiforgery, SampleStore store, ModulusLiquid liquid) =>
{
    if (!await ValidAntiforgery(context, antiforgery)) return Results.BadRequest();
    var form = await context.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var returnUrl = SampleAuth.SafeReturnUrl(form["return_url"].ToString());
    if (SampleAuth.Check(store, email, form["password"].ToString()) is { } user)
    {
        await context.SignInAsync(SampleAuth.Scheme, SampleAuth.Principal(user), new AuthenticationProperties { IsPersistent = form["remember"] == "true" });
        return Results.LocalRedirect(returnUrl);
    }
    return AuthPage(liquid, templates, "auth/login", "Sign in", context, antiforgery, new Dictionary<string, object?>
    {
        ["error"] = SampleAuth.WrongCredentials,
        ["email"] = email,
        ["return_url"] = returnUrl,
    });
});

app.MapPost("/auth/logout", async (HttpContext context, IAntiforgery antiforgery) =>
{
    if (!await ValidAntiforgery(context, antiforgery)) return Results.BadRequest();
    await context.SignOutAsync(SampleAuth.Scheme);
    return Results.LocalRedirect(SampleAuth.LoginPath);
});

app.MapGet("/auth/forgot-password", (HttpContext context, IAntiforgery antiforgery, ModulusLiquid liquid) =>
    AuthPage(liquid, templates, "auth/forgot-password", "Forgot password", context, antiforgery, new Dictionary<string, object?>()));

app.MapPost("/auth/forgot-password", async (HttpContext context, IAntiforgery antiforgery, ModulusLiquid liquid) =>
{
    if (!await ValidAntiforgery(context, antiforgery)) return Results.BadRequest();
    var form = await context.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    // The sample sends no mail; a plausible address gets the same confirmation whether or not an account exists.
    return AuthPage(liquid, templates, "auth/forgot-password", "Forgot password", context, antiforgery, new Dictionary<string, object?>
    {
        ["sent"] = email.Contains('@'),
        ["email"] = email,
    });
});

app.MapGet("/auth/reset-password", (HttpContext context, IAntiforgery antiforgery, ModulusLiquid liquid) =>
    AuthPage(liquid, templates, "auth/reset-password", "Reset password", context, antiforgery, new Dictionary<string, object?>
    {
        ["token"] = context.Request.Query["token"].ToString(),
    }));

app.MapPost("/auth/reset-password", async (HttpContext context, IAntiforgery antiforgery, ModulusLiquid liquid) =>
{
    if (!await ValidAntiforgery(context, antiforgery)) return Results.BadRequest();
    var form = await context.Request.ReadFormAsync();
    var password = form["password"].ToString();
    var error = password.Length < 8 ? "Use at least 8 characters."
        : password != form["confirm"].ToString() ? "The passwords do not match."
        : null;
    if (error is not null)
        return AuthPage(liquid, templates, "auth/reset-password", "Reset password", context, antiforgery, new Dictionary<string, object?>
        {
            ["error"] = error,
            ["token"] = form["token"].ToString(),
        });
    // The sample keeps its shared demo password, so a reset only checks the form and returns to sign-in.
    return Results.LocalRedirect("/auth/login?reset=1");
});

// Two-factor: the sample takes any 6-digit code (the other samples do the same) and goes to the home page.
app.MapGet("/auth/two-factor", (HttpContext context, IAntiforgery antiforgery, ModulusLiquid liquid) =>
    AuthPage(liquid, templates, "auth/two-factor", "Two-factor authentication", context, antiforgery, new Dictionary<string, object?>()));

app.MapPost("/auth/two-factor", async (HttpContext context, IAntiforgery antiforgery, ModulusLiquid liquid) =>
{
    if (!await ValidAntiforgery(context, antiforgery)) return Results.BadRequest();
    var form = await context.Request.ReadFormAsync();
    var code = form["code"].ToString();
    if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^\d{6}$"))
        return AuthPage(liquid, templates, "auth/two-factor", "Two-factor authentication", context, antiforgery, new Dictionary<string, object?>
        {
            ["error"] = "Enter the 6-digit code",
        });
    return Results.LocalRedirect("/");
});

// Sessions are the signed-in user's; a visitor who is not signed in is sent to the sign-in page.
app.MapGet("/account/sessions", (ModulusLiquid liquid) =>
    Page(liquid, templates, "account/sessions", "Sessions", new Dictionary<string, object?>())).RequireAuthorization();

// Customers: list, create, edit and delete (signed-in users only); the routes are in CustomerPages.cs.
CustomerPages.Map(app, templates);
// The chart pages: SVG drawn on the server (see ChartPages.cs).
ChartPages.Map(app, templates);

app.Run();

static async Task<bool> ValidAntiforgery(HttpContext context, IAntiforgery antiforgery)
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
        return true;
    }
    catch (AntiforgeryValidationException)
    {
        return false;
    }
}

static IResult AuthPage(ModulusLiquid liquid, string templates, string page, string title, HttpContext context, IAntiforgery antiforgery, Dictionary<string, object?> values)
{
    values["antiforgery_token"] = antiforgery.GetAndStoreTokens(context).RequestToken;
    return Page(liquid, templates, page, title, values);
}

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
