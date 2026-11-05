using Modulus.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Modulus.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;

// The PDF download needs the QuestPDF licence tier that applies to the host (Community: free under USD 1,000,000 in annual gross
// revenue, not for public-sector or publicly traded companies). Choose the tier for your use.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
Modulus.Samples.Shared.AssistantPage.Configure(key => builder.Configuration[key]);

// Validate from explicit [Required] attributes only, exactly like Blazor's DataAnnotations validator,
// so the same model produces the same errors in every flavor.
builder.Services.AddRazorPages(o =>
    {
        // Sign-in and error pages stay open when Samples:RequireSignIn asks for a sign-in everywhere else.
        o.Conventions.AllowAnonymousToFolder("/Auth");
        o.Conventions.AllowAnonymousToFolder("/Misc");
    })
    .AddMvcOptions(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// Modulus UI for Razor Pages: same theme options, menu and header content as the Blazor and MVC samples.
// Saved views in a SQLite file (App_Data/ui-state.db), so they outlive a restart; without it they stay in memory.
builder.Services.AddSampleUiState(builder.Environment.ContentRootPath);
builder.Services.AddModulusMvc(theme => builder.Configuration.GetSection("Modulus:Theme").Bind(theme));
// Compression, security headers, the CSP (enforced: the pages report no violations, see docs/DOCUMENTATION.md#upgrading-ant-design-blazor-htmx-alpinejs-and-the-plugin-libraries), and /health.
builder.Services.AddModulusHardening(o => o.CspEnforce = true);
builder.Services.AddModulusObservability();
builder.Services.AddTransient<IMenuContributor, AdminMenuContributor>();
builder.Services.AddSingleton<IShellContentProvider, SampleShellContentProvider>();

// Cookie sign-in (SampleAuth). With Samples:RequireSignIn every page but the sign-in and error pages needs it.
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
    app.UseExceptionHandler("/misc/server-error");
    app.UseHsts();
}
// Htmx requests keep their status (a 422 from a form, say, is shown as a message); a page load gets the not-found page.
app.UseWhen(context => !context.Request.IsHtmx(), branch => branch.UseStatusCodePagesWithReExecute("/misc/not-found"));
app.UseModulusHardening();
app.UseHttpsRedirection();
// Request culture from the theme settings language picker (Modulus:Theme:Cultures).
app.UseModulusLocalization();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapModulusHealth().AllowAnonymous();
app.MapStaticAssets().AllowAnonymous();
app.MapRazorPages().WithStaticAssets();
// The "Views" dropdown (<app-saved-views>) posts here.
app.MapModulusSavedViews();
// The header search (htmx) asks here for its results.
app.MapModulusShellSearch();
app.MapModulusDashboard();
// Audio track of the inline player page (/plugins/player), made on the server; the other samples serve the same URL.
app.MapGet(PluginsContent.TrackUrl, () => Results.File(PluginsContent.DemoTrack(), "audio/wav", enableRangeProcessing: true));

app.Run();
