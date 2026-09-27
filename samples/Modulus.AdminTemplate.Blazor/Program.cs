using Modulus.AdminTemplate.Blazor.Components;
using Modulus.Blazor;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Modulus UI: Ant Design Blazor + official ProLayout + theme tokens + menu pipeline.
// Theme values can also be bound from configuration ("Modulus:Theme" in appsettings.json).
builder.Services.AddModulusBlazor(theme => builder.Configuration.GetSection("Modulus:Theme").Bind(theme));
builder.Services.AddTransient<IMenuContributor, AdminMenuContributor>();
builder.Services.AddSingleton<IShellContentProvider, SampleShellContentProvider>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<SampleStore>();
builder.Services.AddSingleton<ProjectGridStore>();
builder.Services.AddSingleton<GarmentStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

// CSV export for the data table page (/tables/advanced); MVC and Razor Pages serve the same URL.
app.MapGet("/tables/advanced/export", (HttpRequest request) =>
{
    var q = request.Query;
    var query = new EmployeeQuery(q["keyword"], NullIfEmpty(q["department"]), NullIfEmpty(q["office"]),
        Enum.TryParse<EmployeeStatus>(q["status"], out var status) ? status : null, NullIfEmpty(q["label"]));
    var ids = q["ids"].Select(v => int.TryParse(v, out var id) ? id : 0).Where(id => id > 0).ToList();
    return Results.File(System.Text.Encoding.UTF8.GetBytes(EmployeeDirectory.Csv(EmployeeDirectory.ForExport(query, ids))), "text/csv", "employees.csv");

    static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
