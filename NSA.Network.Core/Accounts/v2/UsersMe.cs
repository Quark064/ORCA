using NSA.Network.Core.Common.Interfaces;
using System.Text.Json.Serialization;

namespace NSA.Network.Core.Accounts.v2;

public class UsersMe : IRequestBase
{
    public required string AccessToken;

    public string URL => "https://api.accounts.nintendo.com/2.0.0/users/me";
}

public class UsersMeResponse
{
    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("birthday")]
    public string? Birthday { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("iconUri")]
    public string? IconURI { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("isChild")]
    public bool IsChild { get; set; }

    [JsonPropertyName("createdAt")]
    public long CreatedAtUnix { get; set; }

    [JsonIgnore]
    public DateTime CreatedAt =>
        DateTimeOffset.FromUnixTimeSeconds(CreatedAtUnix).UtcDateTime;

    [JsonPropertyName("language")]
    public string? Language { get; set; }
}
