using NSA.Network.Core.Common;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSA.Network.Core.Api.v4;

public class ShowSelf : ManualBase<ShowSelfResponse>
{
    public required string NsaToken;
    public required ulong Id;

    public override string URL => "https://api-lp1.znc.srv.nintendo.net/v4/User/ShowSelf";

    public override string BuildRequest()
    {
        var paramData = new ShowSelfParameterData
        {
            Id = Id
        };

        var request = new ShowSelfRequest
        {
            Parameter = paramData
        };

        return JsonSerializer.Serialize(request);
    }
}


internal class ShowSelfRequest
{
    [JsonPropertyName("parameter")]
    public required ShowSelfParameterData Parameter { get; set; }
}

internal class ShowSelfParameterData
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }
}


public class ShowSelfResponse
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("nsaId")]
    public string NsaId { get; set; } = string.Empty;

    [JsonPropertyName("imageUri")]
    public string ImageUri { get; set; } = string.Empty;

    [JsonPropertyName("image2Uri")]
    public string Image2Uri { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("supportId")]
    public string SupportId { get; set; } = string.Empty;

    [JsonPropertyName("isChildRestricted")]
    public bool IsChildRestricted { get; set; }

    [JsonPropertyName("etag")]
    public string Etag { get; set; } = string.Empty;

    [JsonPropertyName("links")]
    public AccountLinks? Links { get; set; }

    [JsonPropertyName("permissions")]
    public UserPermissions? Permissions { get; set; }

    [JsonPropertyName("presence")]
    public UserPresence? Presence { get; set; }
}

public class AccountLinks
{
    [JsonPropertyName("friendCode")]
    public FriendCodeLink? FriendCode { get; set; }

    [JsonPropertyName("nintendoAccount")]
    public NintendoAccountLink? NintendoAccount { get; set; }
}

public class FriendCodeLink
{
    [JsonPropertyName("regenerable")]
    public bool Regenerable { get; set; }

    [JsonPropertyName("regenerableAt")]
    public long RegenerableAt { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

public class NintendoAccountLink
{
    [JsonPropertyName("membership")]
    public MembershipStatus? Membership { get; set; }
}

public class MembershipStatus
{
    [JsonPropertyName("active")]
    public bool Active { get; set; }
}

public class UserPermissions
{
    [JsonPropertyName("playLog")]
    public string PlayLog { get; set; } = string.Empty;

    [JsonPropertyName("presence")]
    public string Presence { get; set; } = string.Empty;

    [JsonPropertyName("friendRequestReception")]
    public bool FriendRequestReception { get; set; }
}

public class UserPresence
{
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public long UpdatedAt { get; set; }

    [JsonPropertyName("logoutAt")]
    public long LogoutAt { get; set; }

    [JsonPropertyName("game")]
    public FriendGame? Game { get; set; }
}
