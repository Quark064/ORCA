using NSA.Network.Core.Common;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NSA.Network.Core.Api.v4;

public class FriendList : ManualBase<FriendListResult>
{
    public required string NsaToken;

    public override string URL => "https://api-lp1.znc.srv.nintendo.net/v4/Friend/List";

    public override string BuildRequest()
    {
        var data = new JsonObject
        {
            ["parameter"] = new JsonObject()
        };

        return data.ToJsonString();
    }
}

public class FriendListResult
{
    [JsonPropertyName("friends")]
    public List<Friend> Friends { get; set; } = [];
}

public class Friend
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("nsaId")]
    public string NsaId { get; set; } = string.Empty;

    [JsonPropertyName("imageUri")]
    public string ImageUri { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isFavoriteFriend")]
    public bool IsFavorite { get; set; } = false;

    [JsonPropertyName("presence")]
    public required FriendPresence Presence { get; set; }
}

public class FriendPresence
{
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("game")]
    public FriendGame? Game { get; set; }
}

public class FriendGame
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("imageUri")]
    public string ImageUrl { get; set; } = string.Empty;
}
