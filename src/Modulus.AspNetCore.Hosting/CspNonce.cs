using Microsoft.AspNetCore.Http;

namespace Modulus.AspNetCore.Hosting;

/// <summary>
/// The per-request nonce of the Content-Security-Policy. Put it on an inline script the app writes:
/// <c>&lt;script nonce="@CspNonce.Of(Context)"&gt;…&lt;/script&gt;</c>. Modulus writes no inline scripts, so its own markup needs none.
/// </summary>
public static class CspNonce
{
    private const string Key = "modulus:csp-nonce";

    /// <summary>The nonce of this request (base64, 128 bits); an empty string when hardening is off.</summary>
    public static string Of(HttpContext context) =>
        context.Items.TryGetValue(Key, out var value) && value is string nonce ? nonce : "";

    internal static string Create(HttpContext context)
    {
        var nonce = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        context.Items[Key] = nonce;
        return nonce;
    }
}
