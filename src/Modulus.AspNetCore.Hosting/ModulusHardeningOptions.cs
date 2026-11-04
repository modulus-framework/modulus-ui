namespace Modulus.AspNetCore.Hosting;

/// <summary>Settings of <see cref="HostingExtensions.AddModulusHardening"/>.</summary>
public sealed class ModulusHardeningOptions
{
    /// <summary>Brotli and gzip for text responses (pages, scripts, styles, JSON, SVG). On by default.</summary>
    public bool Compression { get; set; } = true;

    /// <summary>
    /// Compression level of dynamic responses (the pages). The framework default of <c>Fastest</c> leaves a 470 KB admin page at
    /// about twice the size Optimal gives (measured: 115 KB against 49 KB with Brotli) for 2 ms more CPU, so <c>Optimal</c> is the
    /// default here. Scripts and styles are served pre-compressed by <c>MapStaticAssets</c> and ignore this.
    /// </summary>
    public System.IO.Compression.CompressionLevel CompressionLevel { get; set; } = System.IO.Compression.CompressionLevel.Optimal;

    /// <summary>
    /// The security headers: no MIME sniffing, a referrer policy, framing limited to the same site, and a permissions policy that
    /// switches off the camera, microphone and location. On by default.
    /// </summary>
    public bool SecurityHeaders { get; set; } = true;

    /// <summary>
    /// Send the Content-Security-Policy. It starts in report-only mode (<see cref="CspEnforce"/> false), so a violation is logged
    /// by the browser and nothing breaks; turn enforcement on when the pages report none.
    /// </summary>
    public bool ContentSecurityPolicy { get; set; } = true;

    /// <summary>Enforce the policy rather than report its violations.</summary>
    public bool CspEnforce { get; set; }

    /// <summary>
    /// Extra origins the pages may connect to (fetch, WebSocket), for example a map tile or API host. Schemes or origins only, such as
    /// <c>https://tiles.example.com</c>.
    /// </summary>
    public IList<string> ConnectSources { get; } = [];

    /// <summary>Where the browser reports violations (a report-uri), or null for none.</summary>
    public string? CspReportUri { get; set; }
}
