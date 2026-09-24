using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;

namespace NSA.Network.Core.Accounts.Connect.v1;

public class Authorize
{
    public static LoginPair GenerateLoginPair()
    {
        const string requestUrl =
            "https://accounts.nintendo.com/connect/1.0.0/authorize";

        var authState = WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(36));

        var authVerifier = WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(32));

        var authChallenge = WebEncoders.Base64UrlEncode(
            SHA256.HashData(Encoding.ASCII.GetBytes(authVerifier)));

        var query = new Dictionary<string, string?>
        {
            ["state"] = authState,
            ["redirect_uri"] = "npf71b963c1b7b6d119://auth",
            ["client_id"] = "71b963c1b7b6d119",
            ["scope"] = "openid user user.birthday user.mii user.screenName",
            ["response_type"] = "session_token_code",
            ["session_token_code_challenge"] = authChallenge,
            ["session_token_code_challenge_method"] = "S256",
            ["theme"] = "login_form"
        };

        return new LoginPair(
            QueryHelpers.AddQueryString(requestUrl, query),
            authVerifier);
    }
}

public sealed record LoginPair(string URL, string Verifier);
