using NSA.Network.Core.Common;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSA.Network.Core.Api.v4;

public class PlayLogShow : ManualBase<List<PlayLogShowResponse>>
{
    public required string NsaToken;
    public required string NsaId;

    public override string URL => "https://api-lp1.znc.srv.nintendo.net/v4/User/PlayLog/Show";

    public override string BuildRequest()
    {
        var paramData = new PlayLogShowParameterData
        {
            NsaId = NsaId
        };

        var request = new PlayLogShowRequest
        {
            Parameter = paramData
        };

        return JsonSerializer.Serialize(request);
    }
}

internal class PlayLogShowRequest
{
    [JsonPropertyName("parameter")]
    public required PlayLogShowParameterData Parameter { get; set; }
}

internal class PlayLogShowParameterData
{
    [JsonPropertyName("nsaId")]
    public string NsaId { get; set; } = string.Empty;
}


public class PlayLogShowResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("imageUri")]
    public string ImageUri { get; set; } = string.Empty;

    [JsonPropertyName("shopUri")]
    public string ShopUri { get; set; } = string.Empty;

    [JsonPropertyName("totalPlayTime")]
    public int TotalPlayTimeMin { get; set; }

    [JsonPropertyName("firstPlayedAt")]
    public long FirstPlayedAtUnix { get; set; }
}