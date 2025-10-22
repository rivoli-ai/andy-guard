using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
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
            throw new InvalidOperationException(
                "Downstream API invocation requested an app token but no authorization header provider is configured. " +
                "Configure Microsoft.Identity.Web token acquisition or disable RequestAppToken.");
        }

        return Task.FromResult(string.Empty);
    }
}
