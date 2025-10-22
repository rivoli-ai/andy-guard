using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Andy.Guard.Scanning;
using Andy.Guard.Scanning.Abstractions;

namespace Andy.Guard.InputScanners;

public class PromptInjectionScanner : IInputScanner
{
    private const string DownstreamServiceName = "AndyInference";
    private const string BatchEndpointRelativePath = "predict/batch";
    private const string PromptInjectionModelId = "deberta-v3-base-prompt-injection-v2";
    private readonly InferenceApiClient _apiClient;
    private readonly IPromptInjectionResultMapper _resultMapper;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PromptInjectionScanner(InferenceApiClient apiClient, IPromptInjectionResultMapper resultMapper)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _resultMapper = resultMapper ?? throw new ArgumentNullException(nameof(resultMapper));
    }

    public string Name => nameof(PromptInjectionScanner);

    public async Task<ScanResult> ScanAsync(string text, ScanOptions? options = null)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var payload = new[]
            {
                new RemoteRequest
                {
                    Text = text,
                    Model = PromptInjectionModelId
                }
            };

            using var response = await _apiClient
                .SendAsync(DownstreamServiceName, HttpMethod.Post, relativePath: BatchEndpointRelativePath, content: payload)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var remoteBatch = JsonSerializer.Deserialize<List<BatchPredictionItem>>(content, JsonOptions);

            if (remoteBatch is null || remoteBatch.Count == 0)
                throw new InvalidOperationException("Inference API returned no predictions.");

            var remote = MapToRemoteResponse(remoteBatch[0]);
            sw.Stop();
            return _resultMapper.Map(remote, options, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return CreateErrorResult(ex, options, sw.Elapsed);
        }
    }

    private static ScanResult CreateErrorResult(Exception exception, ScanOptions? options, TimeSpan elapsed)
    {
        Dictionary<string, object>? metadata = null;

        if (options?.IncludeMetadata != false)
        {
            metadata = new Dictionary<string, object>
            {
                ["error"] = exception.Message,
                ["service"] = DownstreamServiceName
            };
        }
        return new ScanResult
        {
            IsThreatDetected = false,
            ConfidenceScore = 0f,
            RiskLevel = RiskLevel.Undefined,
            Metadata = metadata,
            ProcessingTime = elapsed
        };
    }

    private static RemoteResponse MapToRemoteResponse(BatchPredictionItem item)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string key, object? value)
        {
            if (value is null)
                return;

            metadata[key] = value;
        }

        TryAdd("label", item.Label);
        TryAdd("score", item.Score);

        if (item.Scores is { Count: > 0 })
            metadata["scores"] = item.Scores;

        TryAdd("isSafe", item.IsSafe);
        TryAdd("usingFallback", item.UsingFallback);
        TryAdd("predictionMethod", item.PredictionMethod);
        TryAdd("model", item.Model);
        TryAdd("text", item.Text);

        if (!string.IsNullOrWhiteSpace(item.PredictionMethod) &&
            item.PredictionMethod.StartsWith("error", StringComparison.OrdinalIgnoreCase))
        {
            TryAdd("error", item.PredictionMethod);
        }

        var metadataResult = metadata.Count > 0 ? metadata : null;

        return new RemoteResponse
        {
            Probability = CalculateInjectionProbability(item),
            IsThreat = DetermineIsThreat(item),
            RiskLevel = null,
            Threshold = null,
            Metadata = metadataResult
        };
    }

    private static float? CalculateInjectionProbability(BatchPredictionItem item)
    {
        if (item.Scores is { Count: > 0 })
        {
            foreach (var kvp in item.Scores)
            {
                if (string.Equals(kvp.Key, "INJECTION", StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            float? safeScore = null;

            foreach (var kvp in item.Scores)
            {
                if (string.Equals(kvp.Key, "SAFE", StringComparison.OrdinalIgnoreCase))
                {
                    safeScore = kvp.Value;
                    break;
                }
            }

            if (safeScore.HasValue)
                return 1f - safeScore.Value;
        }

        if (!string.IsNullOrWhiteSpace(item.Label))
        {
            if (string.Equals(item.Label, "INJECTION", StringComparison.OrdinalIgnoreCase))
                return item.Score;

            if (string.Equals(item.Label, "SAFE", StringComparison.OrdinalIgnoreCase))
                return 1f - item.Score;
        }

        return null;
    }

    private static bool? DetermineIsThreat(BatchPredictionItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Label))
        {
            if (string.Equals(item.Label, "INJECTION", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(item.Label, "SAFE", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(item.Label, "ERROR", StringComparison.OrdinalIgnoreCase))
                return null;
        }

        if (item.IsSafe.HasValue &&
            !string.Equals(item.Label, "ERROR", StringComparison.OrdinalIgnoreCase))
            return !item.IsSafe.Value;

        return null;
    }

    private sealed class BatchPredictionItem
    {
        public string? Label { get; init; }
        public float Score { get; init; }
        public Dictionary<string, float>? Scores { get; init; }
        public bool? IsSafe { get; init; }
        public string? Text { get; init; }
        public string? Model { get; init; }
        public bool? UsingFallback { get; init; }
        public string? PredictionMethod { get; init; }
    }
}
