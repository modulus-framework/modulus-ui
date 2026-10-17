using ContosoAdmin.Components;
using ContosoAdmin.Data;
using ContosoAdmin.Menu;
using Modulus.AspNetCore.Hosting;
using Modulus.Blazor;
using Modulus.Ui.Abstractions.Navigation;

var builder = WebApplication.CreateBuilder(args);

// Blazor Web App with interactive server rendering.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Modulus UI: Ant Design Blazor, the shell, the theme and the menu pipeline.
builder.Services.AddModulusBlazor(theme =>
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
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseModulusHardening();
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapModulusHealth().AllowAnonymous();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
