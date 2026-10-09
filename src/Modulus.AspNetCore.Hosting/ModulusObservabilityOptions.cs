namespace Modulus.AspNetCore.Hosting;

/// <summary>Settings of <see cref="ObservabilityExtensions.AddModulusObservability"/>.</summary>
public sealed class ModulusObservabilityOptions
{
    /// <summary>The service name on every signal. Null: the entry assembly's name.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Request traces (ASP.NET Core spans). On by default.</summary>
    public bool Traces { get; set; } = true;

    /// <summary>Request metrics (duration and active requests). On by default.</summary>
    public bool Metrics { get; set; } = true;

    /// <summary>The application's logs, with their scopes, as OpenTelemetry log records. On by default.</summary>
    public bool Logs { get; set; } = true;

    /// <summary>
    /// Send the signals to an OTLP collector. Null (the default) turns it on only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set,
    /// and the endpoint, headers and protocol then come from the standard OpenTelemetry environment variables. Without it the
    /// signals stay in the process (nothing is sent).
    /// </summary>
    public bool? Otlp { get; set; }
}
