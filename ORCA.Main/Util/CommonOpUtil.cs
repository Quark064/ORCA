using NetCord;
using NSA.Network.Core.Accounts.Connect.v1;
using NSA.Network.Core.Api;
using NSA.Network.Core.Api.v4;
using NSA.Security.Core;
using NSA.Security.Core.Attestation.Constants;
using ORCA.Main.Bosses;
using ORCA.Main.Exceptions;
using ORCA.Main.Extensions;
using ORCA.Main.Models;

namespace ORCA.Main.Util;

public static class CommonOpUtil
{
    public static async Task Logout(ulong userId, DMChannel dmChannel, DatabaseBoss db)
    {
        if (db.LoginMessageDB.TryGet(userId, out ulong loginMsgId))
        {
            await dmChannel.TryDeleteMessage(loginMsgId);
            db.LoginMessageDB.ForceDelete(userId);
        }

        if (db.TokenDB.TryGet(userId, out ulong tokenMsgId))
        {
            await dmChannel.TryDeleteMessage(tokenMsgId);
            db.TokenDB.ForceDelete(userId);
        }

        db.AuthVerifierDB.ForceDelete(userId);
        db.KeyDB.ForceDelete(userId);
    }

    public class ValidateRequest
    {
        public required ulong UserId;
        public required DMChannel DmChannel;
        public required Session Session;

        public required DatabaseBoss DB;
        public required NetworkingBoss NW;
    }

    public static async Task<DMTokens> ValidateSessionToken(ValidateRequest request)
    {
        if (!request.DB.TokenDB.TryGet(request.UserId, out ulong tokenMsgId))
        {
            throw new AccountExceptions.SessionTokenMissing();
        }

        if (!request.DB.KeyDB.TryGet(request.UserId, out Memory<byte> aesKey))
        {
            throw new AccountExceptions.AesKeyMissing();
        }

        var tokens = await request.DmChannel.GetUserTokens(tokenMsgId, aesKey);

        if (string.IsNullOrEmpty(tokens.SessionToken))
        {
            await Logout(request.UserId, request.DmChannel, request.DB);
            throw new AccountExceptions.SessionTokenMissing();
        }

        if (JwtUtil.IsTokenExpired(tokens.SessionToken))
        {
            await Logout(request.UserId, request.DmChannel, request.DB);
            throw new AccountExceptions.SessionTokenExpired();
        }

        return tokens;
    }

    public static async Task<DMTokens> ValidateNsaToken(ValidateRequest request)
    {
        var tokens = await ValidateSessionToken(request);
        
        if (string.IsNullOrEmpty(tokens.NsaToken) || JwtUtil.IsTokenExpired(tokens.NsaToken))
        {
            var apiRequest = new ApiToken
            {
                SessionToken = tokens.SessionToken!
            };
            var apiTokens = await request.NW.GetApiTokens(apiRequest);

            var requestId = Guid.NewGuid();
            var nsaRequest = new AccountLogin
            {
                IdToken = apiTokens.IdToken,
                UnixTimestampSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                RequestId = requestId.ToString(),
                AttestationToken = request.Session.GenerateAttestationToken(GenMethod.H1, apiTokens.IdToken, requestId)
            };
            var nsaResult = await request.NW.GetNsaToken(nsaRequest, request.Session);

            tokens.NsaToken = nsaResult.WebApiServerCredential.NsaToken;
        }

        return tokens;
    }

    public static async Task<DMTokens> ValidateBulletToken(ValidateRequest request)
    {
        var tokens = await ValidateNsaToken(request);

        // Get a new Bullet token if it is missing or expired.
        if (string.IsNullOrEmpty(tokens.BulletToken) || DateTimeOffset.UtcNow.AddSeconds(5).ToUnixTimeSeconds() >= tokens.BulletTokenExpirationTimestamp)
        {
            var uuid = Guid.NewGuid();

            var wsTokenRequest = new WebServiceToken
            {
                AccountLoginToken = tokens.NsaToken!,
                ServiceId = 4834290508791808, // Splatoon 3 Applet ID
                RequestId = uuid,
                UnixTimestampSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                AttestationToken = request.Session.GenerateAttestationToken(GenMethod.H2, tokens.NsaToken!, uuid)
            };
            var wsResponse = await request.NW.GetWebServiceToken(wsTokenRequest, request.Session);

            var btRequest = new BulletToken
            {
                GameWebToken = wsResponse.GameWebToken
            };
            var btResponse = await request.NW.GetBulletToken(btRequest);

            tokens.BulletToken = btResponse.BulletToken;
            tokens.BulletTokenExpirationTimestamp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
        }

        return tokens;
    }
}
