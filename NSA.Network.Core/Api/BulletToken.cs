using NSA.Network.Core.Common.Interfaces;
using System.Text.Json.Serialization;

namespace NSA.Network.Core.Api;

public class BulletToken : IRequestBase
{
    public required string GameWebToken;

    public string URL => "https://api.lp1.av5ja.srv.nintendo.net/api/bullet_tokens";
}

public class BulletTokenResponse
{
    [JsonPropertyName("bulletToken")]
    public string BulletToken { get; set; } = string.Empty;
}