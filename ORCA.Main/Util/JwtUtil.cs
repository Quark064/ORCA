using Microsoft.IdentityModel.Tokens;
using ORCA.Main.Exceptions;
using System.Text.Json;

namespace ORCA.Main.Util;

public static class JwtUtil
{
    public static bool IsTokenExpired(string jwtToken, int padding = 5)
    {
        // Nintendo breaks some JWT standards, so parse out the EXP manually.
        try
        {
            var parts = jwtToken.Split('.');

            if (parts.Length != 3)
            {
                return true;
            }

            using var json = JsonDocument.Parse
            (
                Base64UrlEncoder.DecodeBytes(parts[1])
            );

            long exp = json.RootElement
                .GetProperty("exp")
                .GetInt64();

            return DateTimeOffset.FromUnixTimeSeconds(exp)
                <= DateTimeOffset.UtcNow.AddSeconds(padding);
        }
        catch
        {
            return true;
        }
    }

    public static ulong GetSubU64FromNsaToken(string nsaToken)
    {
        try
        {
            return GetSubClaim(nsaToken).GetUInt64();
        }
        catch (UtilExceptions.SubClaimJwtParseFailure)
        {
            throw;
        }
        catch (Exception)
        {
            throw new UtilExceptions.SubClaimJwtParseFailure();
        }
    }

    public static string GetSubStringFromSessionToken(string sessionToken)
    {
        try
        {
            return GetSubClaim(sessionToken).GetString()
                ?? throw new UtilExceptions.SubClaimJwtParseFailure();
        }
        catch (UtilExceptions.SubClaimJwtParseFailure)
        {
            throw;
        }
        catch (Exception)
        {
            throw new UtilExceptions.SubClaimJwtParseFailure();
        }
    }

    private static JsonElement GetSubClaim(string jwtToken)
    {
        var parts = jwtToken.Split('.');

        if (parts.Length != 3)
        {
            throw new UtilExceptions.SubClaimJwtParseFailure();
        }

        using var json = JsonDocument.Parse
        (
            Base64UrlEncoder.DecodeBytes(parts[1])
        );

        if (!json.RootElement.TryGetProperty("sub", out var subProperty))
        {
            throw new UtilExceptions.SubClaimJwtParseFailure();
        }

        return subProperty.Clone();
    }
}
