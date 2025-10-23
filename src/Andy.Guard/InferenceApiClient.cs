using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Identity.Abstractions;
using Polly;
using Polly.Retry;

namespace Andy.Guard;

public class InferenceApiClient
{
    private readonly IDownstreamApi _downstreamApi;
    private static readonly AsyncRetryPolicy<HttpResponseMessage> RetryPolicy = Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .OrResult(response => (int)response.StatusCode >= 500)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: GetBackoffWithJitter);
    private static readonly IAsyncPolicy<HttpResponseMessage> NoRetryPolicy = Policy.NoOpAsync<HttpResponseMessage>();

    public InferenceApiClient(IDownstreamApi downstreamApi)
    {
        _downstreamApi = downstreamApi ?? throw new ArgumentNullException(nameof(downstreamApi));
    }

    public Task<HttpResponseMessage> SendAsync(
        string serviceName,
        HttpMethod method,
        string? relativePath = null,
        object? content = null,
        Action<HttpRequestMessage>? configureRequest = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name must be provided.", nameof(serviceName));

        var policy = content is HttpContent ? NoRetryPolicy : RetryPolicy;

        return policy.ExecuteAsync(ct =>
        {
            var httpContent = PrepareContent(method, content);

            return _downstreamApi.CallApiForAppAsync(
                serviceName,
                options =>
                {
                    options.HttpMethod = method.Method;

                    if (!string.IsNullOrWhiteSpace(relativePath))
                        options.RelativePath = relativePath;

                    options.CustomizeHttpRequestMessage = request =>
                    {
                        ApplyDefaultHeaders(request);
                        configureRequest?.Invoke(request);
                    };
                },
                httpContent,
                ct);
        }, cancellationToken);
    }

    private static void ApplyDefaultHeaders(HttpRequestMessage request)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static HttpContent PrepareContent(HttpMethod method, object? content)
    {
        if (method == HttpMethod.Get || method == HttpMethod.Delete)
            return new StringContent(string.Empty);

        if (content is null)
            return new StringContent(string.Empty);

        if (content is HttpContent httpContent)
            return httpContent;

        var json = JsonSerializer.Serialize(content);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static TimeSpan GetBackoffWithJitter(int retryAttempt)
    {
        var baseDelayMilliseconds = Math.Pow(2, retryAttempt) * 100;
        var jitterMilliseconds = Random.Shared.Next(0, 150);
        return TimeSpan.FromMilliseconds(baseDelayMilliseconds + jitterMilliseconds);
    }
}
