using System.Text.Json.Serialization;

namespace Andy.Guard.InputScanners;

internal sealed class RemoteRequest
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
    
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;
}
