using NSA.Network.Core.Common.Interfaces;
using System.Text.Json.Serialization;

namespace NSA.Network.Core.Accounts.Connect.v1;

public class ApiSessionToken : IContentBase
{
    public required string SessionTokenCode;
    public required string SessionTokenCodeVerifier;

    public string URL => "https://accounts.nintendo.com/connect/1.0.0/api/session_token";

    public HttpContent BuildRequest()
    {
        var formData = new Dictionary<string, string>
        {
            { "client_id", "71b963c1b7b6d119" },
            { "session_token_code", SessionTokenCode },
            { "session_token_code_verifier", SessionTokenCodeVerifier }
        };

        return new FormUrlEncodedContent(formData);
    }
}

public class ApiSessionTokenResponse
{
    [JsonPropertyName("session_token")]
    public string? SessionToken {  get; set; }

    [JsonPropertyName("code")]
    public string? Code {  get; set; }
}
