using Microsoft.AspNetCore.Authorization;
using Modulus.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddModulusMvc(theme => builder.Configuration.GetSection("Modulus:Theme").Bind(theme));
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/misc/server-error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/misc/not-found");
app.UseHttpsRedirection();
// Request culture from the theme settings language picker (Modulus:Theme:Cultures).
app.UseModulusLocalization();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorPages().WithStaticAssets();
// Audio track of the inline player page (/plugins/player), made on the server; the other samples serve the same URL.
app.MapGet(PluginsContent.TrackUrl, () => Results.File(PluginsContent.DemoTrack(), "audio/wav", enableRangeProcessing: true));

app.Run();
