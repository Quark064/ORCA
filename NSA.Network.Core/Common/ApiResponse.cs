using System.Text.Json.Serialization;

namespace NSA.Network.Core.Common;

public class ApiResponse<T>
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; } = default;

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;
}
