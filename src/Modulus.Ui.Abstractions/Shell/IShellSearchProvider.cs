using System.Security.Claims;

namespace Modulus.Ui.Abstractions.Shell;

/// <summary>
/// A source of header search results beyond the menu pages: records such as users or documents. The shell asks every registered provider in
/// parallel with a short budget; a provider that is slow or fails is left out of that answer and never blocks the menu results. A provider
/// must apply the caller's own permissions and limit its own rows, because the shell trusts what it returns.
/// </summary>
public interface IShellSearchProvider
{
    Task<IReadOnlyList<ShellSearchGroup>> SearchAsync(string query, ClaimsPrincipal user, CancellationToken ct);
}
