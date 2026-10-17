using ContosoMvc.Data;
using ContosoMvc.Menu;
using Modulus.AspNetCore.Hosting;
using Modulus.AspNetCore.Mvc;
using Modulus.Ui.Abstractions.Navigation;

var builder = WebApplication.CreateBuilder(args);

// Validate from explicit [Required] attributes only, so the same rules apply as in the Blazor version.
builder.Services.AddControllersWithViews(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// Modulus UI: the shell layout, tag helpers, theme, htmx helpers and the menu pipeline.
builder.Services.AddModulusMvc(theme =>
{
    theme.Title = "Contoso Admin";
    theme.PrimaryColor = "#1677ff";
});

// Your menu: every IMenuContributor adds items to the sidebar and header.
builder.Services.AddTransient<IMenuContributor, AppMenuContributor>();

// Your data. A singleton in-memory store is enough for this tutorial.
builder.Services.AddSingleton<CustomerStore>();

// Brotli/gzip, security headers, a Content-Security-Policy and the /health endpoint.
builder.Services.AddModulusHardening();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseModulusHardening();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapModulusHealth().AllowAnonymous();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
