using Modulus.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;

var builder = WebApplication.CreateBuilder(args);

// Validate from explicit [Required] attributes only, exactly like Blazor's DataAnnotations validator,
// so the same model produces the same errors in both flavors.
builder.Services.AddControllersWithViews(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// Modulus UI for MVC / Razor Pages: same theme options and menu contributors as the Blazor sample.
builder.Services.AddModulusMvc(theme => builder.Configuration.GetSection("Modulus:Theme").Bind(theme));
builder.Services.AddTransient<IMenuContributor, AdminMenuContributor>();
builder.Services.AddSingleton<IShellContentProvider, SampleShellContentProvider>();

builder.Services.AddSingleton<SampleStore>();
builder.Services.AddSingleton<ProjectGridStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers().WithStaticAssets();

app.Run();
