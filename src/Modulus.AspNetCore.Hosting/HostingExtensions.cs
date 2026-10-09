using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;

namespace Modulus.AspNetCore.Hosting;

/// <summary>Registers and maps the hardening defaults: compression, security headers, the CSP nonce, and a health endpoint.</summary>
public static class HostingExtensions
{
    /// <summary>The MIME types compressed: the text the pages, styles, scripts, data and icons are made of.</summary>
    private static readonly string[] CompressedTypes =
        ["text/html", "text/css", "text/plain", "text/javascript", "application/javascript", "application/json", "image/svg+xml"];

    /// <summary>Adds the services: response compression (Brotli and gzip) and the health checks.</summary>
    public static IServiceCollection AddModulusHardening(this IServiceCollection services, Action<ModulusHardeningOptions>? configure = null)
    {
        var options = new ModulusHardeningOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddResponseCompression(o =>
        {
            // Off over HTTPS by default: a compressed response that echoes a secret next to attacker text can leak it (BREACH).
            o.EnableForHttps = false;
            o.MimeTypes = CompressedTypes;
        });
        services.AddHealthChecks();
        return services;
    }

    /// <summary>
    /// Adds the middleware: compression, the security headers and the Content-Security-Policy (with a nonce per request). Call it
    /// before the static files and the endpoints, so every response carries the headers.
    /// </summary>
    public static IApplicationBuilder UseModulusHardening(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetService<ModulusHardeningOptions>() ?? new ModulusHardeningOptions();
        if (options.Compression) app.UseResponseCompression();
        // A library asset requested with its content version (?v=, added by the Modulus head tags) changes when its content does,
        // so the browser may keep it for a year. Other requests keep the static files' own caching (an ETag, revalidated).
        app.Use((context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/_content") && context.Request.Query.ContainsKey("v"))
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                    return Task.CompletedTask;
                });
            return next(context);
        });
        if (options.SecurityHeaders || options.ContentSecurityPolicy)
        {
            app.Use(async (context, next) =>
            {
                var nonce = CspNonce.Create(context);
                var headers = context.Response.Headers;
                if (options.SecurityHeaders)
                {
                    headers["X-Content-Type-Options"] = "nosniff";
                    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                    headers["X-Frame-Options"] = "SAMEORIGIN";
                    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                }
                if (options.ContentSecurityPolicy)
                {
                    var name = options.CspEnforce ? "Content-Security-Policy" : "Content-Security-Policy-Report-Only";
                    headers[name] = PolicyFor(options, nonce);
                }
                await next(context);
            });
        }
        return app;
    }

    /// <summary>
    /// The policy of the pages: scripts from the app and the nonce (AntD and Alpine need 'unsafe-eval' for their expressions),
    /// styles from the app and inline (Ant Design sets styles from script), images from the app, data and https, and no
    /// plug-ins, objects or foreign framing. Connections go to the app, its WebSocket (Blazor), and the sources listed in
    /// <see cref="ModulusHardeningOptions.ConnectSources"/>.
    /// </summary>
    public static string PolicyFor(ModulusHardeningOptions options, string nonce)
    {
        var connect = new StringBuilder("'self' ws: wss: https:");
        foreach (var source in options.ConnectSources) connect.Append(' ').Append(source);
        var sb = new StringBuilder();
        sb.Append("default-src 'self'; ");
        sb.Append($"script-src 'self' 'nonce-{nonce}' 'unsafe-eval'; ");
        sb.Append("style-src 'self' 'unsafe-inline'; ");
        sb.Append("img-src 'self' data: blob: https:; ");
        sb.Append("font-src 'self' data:; ");
        sb.Append("media-src 'self' blob: https:; ");
        sb.Append("worker-src 'self' blob:; ");
        sb.Append($"connect-src {connect}; ");
        sb.Append("object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'");
        if (options.CspReportUri is { Length: > 0 } uri) sb.Append("; report-uri ").Append(uri);
        return sb.ToString();
    }

    /// <summary>Maps <c>/health</c>: 200 with "Healthy" while the app's health checks pass, 503 otherwise. Nothing else is reported.</summary>
    public static IEndpointConventionBuilder MapModulusHealth(this IEndpointRouteBuilder endpoints, string path = "/health") =>
        endpoints.MapHealthChecks(path, new()
        {
            ResponseWriter = (context, _) =>
            {
                context.Response.ContentType = "text/plain; charset=utf-8";
                return context.Response.WriteAsync(context.Response.StatusCode == 200 ? "Healthy" : "Unhealthy");
            },
        });
}
