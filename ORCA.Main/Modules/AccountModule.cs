using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NSA.Network.Core.Accounts.Connect.v1;
using NSA.Network.Core.Accounts.v2;
using ORCA.Main.Bosses;
using ORCA.Main.Exceptions;
using ORCA.Main.Extensions;
using ORCA.Main.Models;
using ORCA.Main.Util;
using System.Security.Cryptography;
using System.Web;
using static ORCA.Main.Util.CommonOpUtil;

namespace ORCA.Main.Modules;

[SlashCommand("account", "Manage or link a Nintendo Account.")]
public class AccountModule(
    DatabaseBoss db,
    NetworkingBoss nw,
    SessionBoss sb,
    ILogger<AccountModule> logger)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly DatabaseBoss _db = db;
    private readonly NetworkingBoss _nw = nw;
    private readonly SessionBoss _sb = sb;

    private readonly ILogger _logger = logger;

    [SubSlashCommand("login_step_1", "Send sign-in instructions to DMs.")]
    public async Task LoginStep1()
    {
        await Context.Interaction.Defer(true);    

        var userId = Context.User.Id;
        var dmChannel = await Context.User.GetDMChannelAsync();

        // Delete an old login message if it exists.
        if (_db.LoginMessageDB.TryGet(userId, out ulong oldMsgId))
        {
            _db.LoginMessageDB.ForceDelete(userId);
            await dmChannel.TryDeleteMessage(oldMsgId);
        }

        if (_db.TokenDB.TryGet(userId, out ulong _))
        {
            throw new AccountExceptions.UserAlreadyExists();
        }

        // Generate a sign in URL and send it to the user.
        var authPair = Authorize.GenerateLoginPair();
        _db.AuthVerifierDB.Upsert(userId, authPair.Verifier);

        var response = new EmbedProperties
        {
            Title = "Login to your Nintendo Account",
            Description = string.Join("\n\n",
            [
                $"To link the bot to your Nintendo Account, sign into with your Nintendo Account [with this link]({authPair.URL}).",
                "On the 'Link your account' page, right-click (desktop) or long-press (mobile) the 'Select this account' button and copy the URL.",
                "Then, run the `/account login_step_2` command and provide the copied URL to complete the login."
            ]),
            Color = new Color(0x3EC995),
            Image = "https://iili.io/qxh1gLv.gif"
        };
        var loginMsg = await dmChannel.SendMessageAsync(new MessageProperties { Embeds = [response] });

        // Save the login message to the database.
        _db.LoginMessageDB.Upsert(userId, loginMsg.Id);

        await Context.Interaction.SendReplyDeferredAsync("Sent a login link to your DMs.", true);
    }

    [SubSlashCommand("login_step_2", "Provide your sign-in url to authenticate with Nintendo Services.")]
    public async Task LoginStep2(string url)
    {
        await Context.Interaction.Defer(true);

        var userId = Context.User.Id;
        var dmChannel = await Context.User.GetDMChannelAsync();

        // Parse the provided URL.
        string? sessionTokenCode;
        try
        {
            var uri = new Uri(url);
            var fragment = uri.Fragment.TrimStart('#');
            var fragmentParams = HttpUtility.ParseQueryString(fragment);

            sessionTokenCode = fragmentParams["session_token_code"];
        }
        catch (Exception)
        {
            throw new AccountExceptions.InvalidUrl();
        }

        // Validate the Session Token Code.
        if (string.IsNullOrWhiteSpace(sessionTokenCode))
        {
            throw new AccountExceptions.InvalidUrl();
        }

        if (!_db.AuthVerifierDB.TryGet(userId, out string verifier))
        {
            throw new AccountExceptions.AuthVerifierMissing();
        }

        // Get a Session Token from Nintendo.
        var request = new ApiSessionToken
        {
            SessionTokenCode = sessionTokenCode,
            SessionTokenCodeVerifier = verifier
        };
        var sessionTokenResult = await _nw.GetSessionToken(request);

        var disclaimer = string.Join("\n",
        [
            "Your required tokens will be stored encrypted in this DM.",
            "These are retrieved, decrypted, and sent directly to Nintendo; they are never stored on the ORCA server.",
            "You have the right to revoke access at anytime by blocking ORCA.",
            "Alternatively, this message can be deleted anytime with the `/account logout` command."
        ]);
        await dmChannel.SendMessageAsync(new MessageProperties { Content =  disclaimer });

        // Delete the login message.
        if (_db.LoginMessageDB.TryGet(userId, out ulong loginMsgId))
        {
            _db.LoginMessageDB.ForceDelete(userId);
            await dmChannel.TryDeleteMessage(loginMsgId);
        }

        // Create the Token Storage message.
        var tokenMsg = await dmChannel.SendMessageAsync(new MessageProperties { Content = "TokenMessage" });
        
        _db.TokenDB.Upsert(userId, tokenMsg.Id);
        _db.AuthVerifierDB.ForceDelete(userId);

        // Create an AES key.
        var aesKey = RandomNumberGenerator.GetBytes(32);
        _db.KeyDB.Upsert(userId, aesKey);

        var tokens = new DMTokens
        {
            SessionToken = sessionTokenResult.SessionToken,
            MessageId = tokenMsg.Id,
            AesKey = aesKey
        };

        await dmChannel.SetUserTokens(tokens);

        _logger.LogInformation("User {UserID} has linked a Nintendo Account.", userId);
        await Context.Interaction.SendReplyDeferredAsync("Successfully logged in.", true);
    }

    [SubSlashCommand("about", "Get details about the current Nintendo Account.")]
    public async Task About()
    {
        await Context.Interaction.Defer();

        var state = new ValidateRequest
        {
            UserId = Context.User.Id,
            DmChannel = await Context.User.GetDMChannelAsync(),
            Session = _sb.GetSession(Context.User.Id),

            DB = _db,
            NW = _nw
        };

        // Get and validate the Session Token.
        var tokens = await ValidateSessionToken(state);

        // Exchange for Api Tokens with Nintendo.
        var apiRequest = new ApiToken
        {
            SessionToken = tokens.SessionToken!
        };
        var apiTokens = await _nw.GetApiTokens(apiRequest);

        if (string.IsNullOrEmpty(apiTokens.AccessToken))
        {
            throw new NetworkExceptions.ApiTokensGetFailure();
        }

        // Get User information from Nintendo.
        var userRequest = new UsersMe
        {
            AccessToken = apiTokens.AccessToken
        };
        var userInfo = await _nw.GetUsersMe(userRequest);

        var response = new EmbedProperties
        {
            Title = userInfo.Nickname,
            Color = new Color(0x41A0AE),
            Thumbnail = new EmbedThumbnailProperties(userInfo.IconURI),
            Fields = [
                new EmbedFieldProperties
                {
                    Name = "ID",
                    Value = userInfo.ID,
                    Inline = false
                },
                new EmbedFieldProperties
                {
                    Name = "Account Created",
                    Value = $"{userInfo.CreatedAt:MMMM} {userInfo.CreatedAt.Day}, {userInfo.CreatedAt.Year}",
                    Inline = false
                }
            ]
        };

        await Context.Interaction.SendReplyDeferredAsync(response);
    }

    [SubSlashCommand("logout", "Unlink your Nintendo Account from ORCA.")]
    public async Task LogOut()
    {
        await Context.Interaction.Defer(true);

        var userId = Context.User.Id;
        var dmChannel = await Context.User.GetDMChannelAsync();

        if (!_db.TokenDB.TryGet(userId, out ulong _))
        {
            await Context.Interaction.SendReplyDeferredAsync("Cannot unlink. There is no account linked to this profile.", true);
            return;
        }

        await CommonOpUtil.Logout(userId, dmChannel, _db);

        _logger.LogInformation("User {UserID} has unlinked their Nintendo Account.", userId);
        await Context.Interaction.SendReplyDeferredAsync("Successfully unlinked your Nintendo Account.", true);
    }
}
