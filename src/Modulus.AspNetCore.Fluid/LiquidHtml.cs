namespace Modulus.AspNetCore.Fluid;

/// <summary>
/// Markup a template is given to print as it is, not escaped: a page's body inside its layout, or a fragment the application built
/// with a Modulus renderer. Only pass markup the application made or has encoded; everything else is escaped on output.
/// </summary>
public sealed record LiquidHtml(string Html);
