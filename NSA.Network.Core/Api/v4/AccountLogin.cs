using NSA.Network.Core.Common;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSA.Network.Core.Api.v4;

public class AccountLogin : ManualBase<AccountLoginResult>
{
    public required string IdToken;
    public required long UnixTimestampSec;
    public required string RequestId;
    public required string AttestationToken;

    public override string URL => "https://api-lp1.znc.srv.nintendo.net/v4/Account/Login";

    public override string BuildRequest()
    {
        var paramData = new AccountLoginParameterData
        {
            NaIdToken = IdToken,
            Language = "en-US",
            Timestamp = UnixTimestampSec,
            RequestId = RequestId,
            AttestationToken = AttestationToken
        };

        var request = new AccountLoginRequest
        {
            Parameter = paramData
        };

        return JsonSerializer.Serialize(request);
    }
}


internal class AccountLoginRequest
{
    [JsonPropertyName("parameter")]
    public required AccountLoginParameterData Parameter { get; set; }
}

internal class AccountLoginParameterData
{
    [JsonPropertyName("naIdToken")]
    public required string NaIdToken { get; set; }

    [JsonPropertyName("language")]
    public required string Language { get; set; }

    [JsonPropertyName("timestamp")]
    public required long Timestamp { get; set; }

    [JsonPropertyName("requestId")]
    public required string RequestId { get; set; }

    [JsonPropertyName("f")]
    public required string AttestationToken { get; set; }
}


public class AccountLoginResult
{
    [JsonPropertyName("webApiServerCredential")]
    public WebApiServerCredential WebApiServerCredential { get; set; } = new();
}

public class WebApiServerCredential
{
    [JsonPropertyName("accessToken")]
    public string NsaToken { get; set; } = string.Empty;

    [JsonPropertyName("expiresIn")]
    public int ExpiresIn { get; set; }
}