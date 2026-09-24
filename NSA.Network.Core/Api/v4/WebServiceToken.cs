using NSA.Network.Core.Common;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSA.Network.Core.Api.v4;

public class WebServiceToken : ManualBase<WebServiceTokenResponse>
{
    public required string AccountLoginToken;

    public required ulong ServiceId;
    public required Guid RequestId;
    public required long UnixTimestampSec;
    public required string AttestationToken;

    public override string URL => "https://api-lp1.znc.srv.nintendo.net/v4/Game/GetWebServiceToken";

    public override string BuildRequest()
    {
        var paramData = new WebServiceTokenParameterData
        {
            ServiceId = ServiceId,
            AttestationToken = AttestationToken,
            RequestId = RequestId,
            Timestamp = UnixTimestampSec
        };

        var request = new WebServiceTokenRequest
        {
            Parameter = paramData
        };

        return JsonSerializer.Serialize(request);
    }
}

internal class WebServiceTokenRequest
{
    [JsonPropertyName("parameter")]
    public required WebServiceTokenParameterData Parameter { get; set; }
}

internal class WebServiceTokenParameterData
{
    [JsonPropertyName("id")]
    public required ulong ServiceId { get; set; }

    [JsonPropertyName("registrationToken")]
    public string RegistrationToken { get; set; } = string.Empty;

    [JsonPropertyName("f")]
    public required string AttestationToken { get; set; }

    [JsonPropertyName("requestId")]
    public required Guid RequestId { get; set; }

    [JsonPropertyName("timestamp")]
    public required long Timestamp { get; set; }
}

public class WebServiceTokenResponse
{
    [JsonPropertyName("accessToken")]
    public string GameWebToken { get; set; } = string.Empty;

    [JsonPropertyName("expiresIn")]
    public int ExpiresIn { get; set; }
}