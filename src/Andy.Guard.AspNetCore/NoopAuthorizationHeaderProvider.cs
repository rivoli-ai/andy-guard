using System.Security.Claims;
using Microsoft.Identity.Abstractions;

namespace Andy.Guard.AspNetCore;

/// <summary>
/// Provides a no-op authorization header for downstream API calls when tokens are not required.
/// </summary>
internal sealed class NoopAuthorizationHeaderProvider : IAuthorizationHeaderProvider
{
    public Task<string> CreateAuthorizationHeaderForUserAsync(
        IEnumerable<string> scopes,
        AuthorizationHeaderProviderOptions? options = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default)
        => CreateHeader(options);

    public Task<string> CreateAuthorizationHeaderForAppAsync(
        string serviceName,
        AuthorizationHeaderProviderOptions? options = null,
        CancellationToken cancellationToken = default)
        => CreateHeader(options);

    public Task<string> CreateAuthorizationHeaderAsync(
        IEnumerable<string> scopes,
        AuthorizationHeaderProviderOptions? options = null,
        ClaimsPrincipal? user = null,
        CancellationToken cancellationToken = default)
        => CreateHeader(options);

    private static Task<string> CreateHeader(AuthorizationHeaderProviderOptions? options)
    {
        if (options?.RequestAppToken == true)
        {
            // DownstreamApi.CallApiForAppAsync always toggles RequestAppToken to true; return an empty header
            // so local development can proceed without Entra integration.
            return Task.FromResult(string.Empty);
        }

        return Task.FromResult(string.Empty);
    }
}
