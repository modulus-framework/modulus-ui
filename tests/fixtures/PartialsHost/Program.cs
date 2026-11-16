using Modulus.AspNetCore.Hosting;
using Modulus.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddModulusMvc();
builder.Services.AddModulusHardening();
var app = builder.Build();
app.UseStaticFiles();
app.UseModulusHardening();
app.MapRazorPages();
app.Run();
public partial class Program { }
