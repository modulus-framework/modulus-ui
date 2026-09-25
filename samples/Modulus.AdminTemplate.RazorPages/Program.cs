using Modulus.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;

var builder = WebApplication.CreateBuilder(args);

// Validate from explicit [Required] attributes only, exactly like Blazor's DataAnnotations validator,
// so the same model produces the same errors in every flavor.
builder.Services.AddRazorPages()
    .AddMvcOptions(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// Modulus UI for Razor Pages: same theme options, menu and header content as the Blazor and MVC samples.
builder.Services.AddModulusMvc(theme => builder.Configuration.GetSection("Modulus:Theme").Bind(theme));
builder.Services.AddTransient<IMenuContributor, AdminMenuContributor>();
builder.Services.AddSingleton<IShellContentProvider, SampleShellContentProvider>();

builder.Services.AddSingleton<SampleStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/misc/server-error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/misc/not-found");
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
