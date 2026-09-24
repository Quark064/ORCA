using NSA.Network.Core.Common.Interfaces;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace NSA.Network.Core.Accounts.Connect.v1;

public class ApiToken : IContentBase
{
    public required string SessionToken;

    public string URL => "https://accounts.nintendo.com/connect/1.0.0/api/token";

    public HttpContent BuildRequest()
    {
        var request = new ApiTokenRequest
        {
            ClientId = "71b963c1b7b6d119",
            SessionToken = SessionToken,
            GrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer-session-token"
        };

        return JsonContent.Create(request);
    }
}

internal class ApiTokenRequest
{
    [JsonPropertyName("client_id")]
    public required string ClientId { get; set; }

    [JsonPropertyName("session_token")]
    public required string SessionToken { get; set; }

    [JsonPropertyName("grant_type")]
    public required string GrantType { get; set; }
}

public class ApiTokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }

    [JsonPropertyName("id_token")]
    public required string IdToken { get; set; }
}