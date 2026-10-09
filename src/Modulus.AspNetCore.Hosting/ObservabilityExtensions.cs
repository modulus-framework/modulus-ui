using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Modulus.AspNetCore.Hosting;

/// <summary>Registers OpenTelemetry for a Modulus app: request traces, request metrics and the logs, optionally sent over OTLP.</summary>
public static class ObservabilityExtensions
{
    /// <summary>The standard OpenTelemetry variable that names the collector. Its presence turns OTLP export on by default.</summary>
    public const string OtlpEndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    // Paths that are plumbing, not requests of the app: the health probe, the library's assets, Blazor's framework and its live
    // connection. Traces of them would be noise, and a live connection's span would stay open for as long as the page does.
    private static readonly string[] UntracedPrefixes = ["/health", "/_content", "/_framework", "/_blazor"];

    /// <summary>
    /// Adds OpenTelemetry. Call it in the builder, before the app is built. A query string is recorded with its sensitive values
    /// redacted (the instrumentation's default), so a token in a URL is not written to a trace.
    /// </summary>
    public static IServiceCollection AddModulusObservability(this IServiceCollection services, Action<ModulusObservabilityOptions>? configure = null)
    {
        var options = new ModulusObservabilityOptions();
        configure?.Invoke(options);
        var service = options.ServiceName ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "modulus-app";
        var otlp = options.Otlp ?? !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OtlpEndpointVariable));

        var openTelemetry = services.AddOpenTelemetry().ConfigureResource(resource => resource.AddService(service));
        if (options.Traces)
            openTelemetry.WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(o => o.Filter = context => ShouldTrace(context.Request.Path));
                if (otlp) tracing.AddOtlpExporter();
            });
        if (options.Metrics)
            openTelemetry.WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                if (otlp) metrics.AddOtlpExporter();
            });
        if (options.Logs)
            services.AddLogging(logging => logging.AddOpenTelemetry(log =>
            {
                log.IncludeFormattedMessage = true;
                log.IncludeScopes = true;
                if (otlp) log.AddOtlpExporter();
            }));
        return services;
    }

    /// <summary>Whether a request on this path gets a trace: everything but the health probe and the library's plumbing.</summary>
    public static bool ShouldTrace(PathString path)
    {
        var value = path.Value ?? "";
        foreach (var prefix in UntracedPrefixes)
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
