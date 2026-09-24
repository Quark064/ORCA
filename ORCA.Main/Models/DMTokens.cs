using System.Text.Json.Serialization;

namespace ORCA.Main.Models;

public class DMTokens
{
    [JsonPropertyName("s")]
    public string? SessionToken { get; set; }

    [JsonPropertyName("g")]
    public string? NsaToken { get; set; }

    [JsonPropertyName("b")]
    public string? BulletToken { get; set; }

    [JsonPropertyName("bExpiresAt")]
    public long BulletTokenExpirationTimestamp { get; set; }

    [JsonIgnore]
    public ulong MessageId { get; set; }

    [JsonIgnore]
    public Memory<byte> AesKey { get; set; }
}
