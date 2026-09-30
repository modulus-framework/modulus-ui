using System.Security.Claims;

namespace Modulus.Samples.Shared;

/// <summary>
/// Cookie sign-in for the three samples: any active user of <see cref="SampleStore"/> signs in with <see cref="DemoPassword"/>.
/// The apps issue the cookie (<c>SignInAsync</c> with <see cref="Scheme"/>); the header then shows the signed-in user. With
/// <c>Samples:RequireSignIn</c> set to true every page except the sign-in and error pages asks for a sign-in first.
/// </summary>
public static class SampleAuth
{
    /// <summary>The cookie scheme (ASP.NET Core's <c>CookieAuthenticationDefaults.AuthenticationScheme</c>).</summary>
    public const string Scheme = "Cookies";

    /// <summary>Configuration key that turns on the sign-in requirement.</summary>
    public const string RequireSignInKey = "Samples:RequireSignIn";

    /// <summary>The password of every sample user.</summary>
    public const string DemoPassword = "modulus";

    public const string LoginPath = "/auth/login";
    public const string LogoutPath = "/auth/logout";
    public const string DeniedPath = "/misc/not-authorized";

    /// <summary>The user for these credentials, or null (unknown email, wrong password, or a suspended or pending account).</summary>
    public static AppUser? Check(SampleStore store, string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || password != DemoPassword) return null;
        var user = store.Users().FirstOrDefault(u => string.Equals(u.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));
        return user is { Status: UserStatus.Active } ? user : null;
    }

    /// <summary>The signed-in identity: id, name, email and role claims.</summary>
    public static ClaimsPrincipal Principal(AppUser user) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
        ], Scheme));

    /// <summary>
    /// Where to go after signing in: <paramref name="returnUrl"/> when it stays on this site (a path, not <c>//host</c> or
    /// <c>/\host</c>), else the home page.
    /// </summary>
    public static string SafeReturnUrl(string? returnUrl) =>
        returnUrl is { Length: > 0 } url && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) && !url.Contains("://", StringComparison.Ordinal)
            ? url
            : "/";

    /// <summary>The error shown for credentials that do not sign in.</summary>
    public const string WrongCredentials = "Wrong email or password. Sample users sign in with the password “modulus”.";
}
