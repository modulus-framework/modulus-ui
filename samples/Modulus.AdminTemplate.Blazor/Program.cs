using Modulus.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Modulus.AdminTemplate.Blazor.Components;
using Modulus.Blazor;
using Modulus.Documents;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;
using Modulus.Ui.Abstractions.Theming;

var builder = WebApplication.CreateBuilder(args);

// The spreadsheet and document editors send their whole JSON back on each change; SignalR's 32 KB default would drop
// the circuit on a large workbook or document.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 8 * 1024 * 1024);

// Modulus UI: Ant Design Blazor + official ProLayout + theme tokens + menu pipeline.
// Theme values can also be bound from configuration ("Modulus:Theme" in appsettings.json).
// Saved views in a SQLite file (App_Data/ui-state.db), so they outlive a restart; without it they stay in memory.
builder.Services.AddSampleUiState(builder.Environment.ContentRootPath);
builder.Services.AddModulusBlazor(theme => builder.Configuration.GetSection("Modulus:Theme").Bind(theme));
// Compression, security headers and the CSP (report-only: Blazor's import map is an inline script with no nonce, see docs/UPGRADING.md), and /health.
builder.Services.AddModulusHardening();
builder.Services.AddTransient<IMenuContributor, AdminMenuContributor>();
builder.Services.AddSingleton<IShellContentProvider, SampleShellContentProvider>();
builder.Services.AddHttpContextAccessor();

// Cookie sign-in (SampleAuth): the login page posts to /auth/sign-in, which sets the cookie. With Samples:RequireSignIn
// every page but the sign-in and error pages needs it (they carry [AllowAnonymous]).
builder.Services.AddAuthentication(SampleAuth.Scheme).AddCookie(o =>
{
    o.LoginPath = SampleAuth.LoginPath;
    o.LogoutPath = SampleAuth.LogoutPath;
    o.AccessDeniedPath = SampleAuth.DeniedPath;
    o.SlidingExpiration = true;
});
builder.Services.AddAuthorization(o =>
{
    if (builder.Configuration.GetValue<bool>(SampleAuth.RequireSignInKey))
        o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddSingleton<SampleStore>();
builder.Services.AddSingleton<ProjectGridStore>();
builder.Services.AddSingleton<GarmentStore>();
builder.Services.AddSingleton<BillOfMaterialsStore>();
builder.Services.AddSingleton<CustomerImportStore>();
builder.Services.AddSingleton<FileStore>();
builder.Services.AddSingleton<PurchaseStore>();

var app = builder.Build();
app.Services.EnsureUiStateDatabase();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseModulusHardening();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Request culture from the theme settings language picker (Modulus:Theme:Cultures): the culture cookie first, then
// Accept-Language. The circuit keeps the culture of the request that opened it, and AntD's LocaleProvider follows it.
var cultures = ModulusCulture.Supported(app.Services.GetRequiredService<IOptions<ModulusThemeOptions>>().Value.Cultures);
if (cultures.Count > 0)
{
    var localization = new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture(cultures[0]),
        SupportedCultures = [.. cultures],
        SupportedUICultures = [.. cultures],
    };
    var cookie = localization.RequestCultureProviders.OfType<CookieRequestCultureProvider>().First();
    localization.RequestCultureProviders.Remove(cookie);
    localization.RequestCultureProviders.Insert(0, cookie);
    app.UseRequestLocalization(localization);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapModulusHealth().AllowAnonymous();
app.MapStaticAssets().AllowAnonymous();

// Sign in and out. The login page validates with AntD, then posts a native form here (a circuit can't set cookies).
app.MapPost("/auth/sign-in", async (HttpContext http, SampleStore store, [FromForm] LoginInput input, [FromQuery] string? returnUrl) =>
{
    if (SampleAuth.Check(store, input.Email, input.Password) is not { } user)
        return Results.LocalRedirect($"{SampleAuth.LoginPath}?error=1&email={Uri.EscapeDataString(input.Email)}" +
            (string.IsNullOrEmpty(returnUrl) ? "" : "&returnUrl=" + Uri.EscapeDataString(returnUrl)));
    await http.SignInAsync(SampleAuth.Scheme, SampleAuth.Principal(user), new AuthenticationProperties { IsPersistent = input.Remember });
    return Results.LocalRedirect(SampleAuth.SafeReturnUrl(returnUrl));
}).AllowAnonymous();
app.MapGet(SampleAuth.LogoutPath, async (HttpContext http) =>
{
    await http.SignOutAsync(SampleAuth.Scheme);
    return Results.LocalRedirect(SampleAuth.LoginPath);
}).AllowAnonymous();

// Excel download of the pivot page (/tables/pivot); MVC and Razor Pages serve the same URL.
app.MapGet(PivotTablePage.DownloadPath, (HttpRequest request) =>
{
    var q = request.Query;
    var query = q.SelectMany(kv => kv.Value.Select(v => new KeyValuePair<string, string?>(kv.Key, v))).ToList();
    var filter = new ShipmentFilter(Enum.TryParse<ShipmentStatus>(q["status"], out var status) ? status : null, Text(q["city"]), Text(q["keyword"]));
    return Results.File(PivotTablePage.Download(filter, query, PivotTablePage.Access.For(request.HttpContext.User)),
        Modulus.Ui.Abstractions.Pivot.PivotXlsx.ContentType, PivotTablePage.DownloadName);

    static string? Text(string? value) => string.IsNullOrEmpty(value) ? null : value;
});

// CSV export for the data table page (/tables/advanced); MVC and Razor Pages serve the same URL.
app.MapGet("/tables/advanced/export", (HttpRequest request) =>
{
    var q = request.Query;
    var query = new EmployeeQuery(q["keyword"], NullIfEmpty(q["department"]), NullIfEmpty(q["office"]),
        Enum.TryParse<EmployeeStatus>(q["status"], out var status) ? status : null, NullIfEmpty(q["label"]))
    {
        Searches = ColumnSearch.FromQuery(q.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value.ToString()))),
    };
    var ids = q["ids"].Select(v => int.TryParse(v, out var id) ? id : 0).Where(id => id > 0).ToList();
    return Results.File(System.Text.Encoding.UTF8.GetBytes(EmployeeDirectory.Csv(EmployeeDirectory.ForExport(query, ids))), "text/csv", "employees.csv");

    static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
});
// Upload target of the dropzone page (/plugins/dropzone): checks each file and throws it away. AntD's Upload posts
// without an antiforgery token, so the endpoint opts out; MVC and Razor Pages serve the same URL.
app.MapPost("/plugins/dropzone/upload", (IFormFile? file) =>
    PluginsContent.CheckUpload(file?.FileName, file?.Length ?? 0) is { } error ? Results.BadRequest(error) : Results.Ok())
    .DisableAntiforgery();

// Excel download of the spreadsheet page (/plugins/spreadsheet): the posted sheets (or the demo budget) as an xlsx workbook.
// The form lives in an interactive circuit, so no antiforgery token rides along; MVC and Razor Pages serve the same URL.
app.MapPost(PluginsContent.SpreadsheetDownloadUrl, ([FromForm] string? budget) =>
        Results.File(SpreadsheetXlsx.Write(PluginsContent.PostedSheets(budget) ?? PluginsContent.Budget),
            SpreadsheetXlsx.ContentType, PluginsContent.SpreadsheetDownloadName))
    .DisableAntiforgery();

// Word download of the document page (/plugins/document): the posted document (or the contract template) as a docx file.
// Like the spreadsheet download it only writes a file, so the token-less form from the circuit is fine.
app.MapPost(PluginsContent.DocumentDownloadUrl, ([FromForm] string? document) =>
        Results.File(DocumentDocx.Write(PluginsContent.PostedDocument(document) ?? PluginsContent.Contract),
            DocumentDocx.ContentType, PluginsContent.DocumentDownloadName))
    .DisableAntiforgery();

// Audio track of the inline player page (/plugins/player), made on the server; MVC and Razor Pages serve the same URL.
app.MapGet(PluginsContent.TrackUrl, () => Results.File(PluginsContent.DemoTrack(), "audio/wav", enableRangeProcessing: true));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
