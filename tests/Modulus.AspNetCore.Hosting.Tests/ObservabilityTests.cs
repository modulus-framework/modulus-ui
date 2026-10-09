using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modulus.AspNetCore.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace Modulus.AspNetCore.Hosting.Tests;

/// <summary>The OpenTelemetry registration: which requests are traced, and the signals the services hold.</summary>
public class ObservabilityTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/invoices/42")]
    [InlineData("/documents/mail-merge.pdf")]
    public void AppRequestsAreTraced(string path) => Assert.True(ObservabilityExtensions.ShouldTrace(path));

    [Theory]
    [InlineData("/health")]
    [InlineData("/HEALTH")]
    [InlineData("/_content/Modulus.Ui.Abstractions/modulus.css")]
    [InlineData("/_framework/blazor.web.js")]
    [InlineData("/_blazor")]
    public void PlumbingIsNotTraced(string path) => Assert.False(ObservabilityExtensions.ShouldTrace(path));

    [Fact]
    public void TheDefaults_TraceMeterAndLog()
    {
        var options = new ModulusObservabilityOptions();

        Assert.True(options.Traces);
        Assert.True(options.Metrics);
        Assert.True(options.Logs);
        Assert.Null(options.Otlp);
        Assert.Null(options.ServiceName);
    }

    [Fact]
    public void TheServicesHoldATracerAndAMeter()
    {
        var services = new ServiceCollection();
        services.AddModulusObservability(o => o.ServiceName = "tests");

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<TracerProvider>());
        Assert.NotNull(provider.GetService<MeterProvider>());
    }

    [Fact]
    public void ASwitchedOffSignalIsNotRegistered()
    {
        var services = new ServiceCollection();
        services.AddModulusObservability(o =>
        {
            o.Traces = false;
            o.Metrics = false;
        });

        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<TracerProvider>());
        Assert.Null(provider.GetService<MeterProvider>());
    }
}
