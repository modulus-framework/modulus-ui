using Modulus.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Modulus.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;

var builder = WebApplication.CreateBuilder(args);

// Validate from explicit [Required] attributes only, exactly like Blazor's DataAnnotations validator,
// so the same model produces the same errors in both flavors.
builder.Services.AddControllersWithViews(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// Modulus UI for MVC / Razor Pages: same theme options and menu contributors as the Blazor sample.
// Saved views in a SQLite file (App_Data/ui-state.db), so they outlive a restart; without it they stay in memory.
builder.Services.AddSampleUiState(builder.Environment.ContentRootPath);
builder.Services.AddModulusMvc(theme => builder.Configuration.GetSection("Modulus:Theme").Bind(theme));
// Compression, security headers, the CSP (enforced: the pages report no violations, see docs/UPGRADING.md), and /health.
builder.Services.AddModulusHardening(o => o.CspEnforce = true);
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
    app.UseExceptionHandler("/error");
    app.UseHsts();
}
// Htmx requests keep their status (a 422 from a form, say, is shown as a message); a page load gets the not-found page.
app.UseWhen(context => !context.Request.IsHtmx(), branch => branch.UseStatusCodePagesWithReExecute("/not-found"));
app.UseModulusHardening();
app.UseHttpsRedirection();
// Request culture from the theme settings language picker (Modulus:Theme:Cultures).
app.UseModulusLocalization();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapModulusHealth().AllowAnonymous();
app.MapStaticAssets().AllowAnonymous();
app.MapControllers().WithStaticAssets();
// The "Views" dropdown (<app-saved-views>) posts here.
app.MapModulusSavedViews();
// The dashboard layout tiles post here.
app.MapModulusDashboard();

app.Run();
