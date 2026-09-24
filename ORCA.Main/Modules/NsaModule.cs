using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NSA.Network.Core.Api.v4;
using ORCA.Main.Bosses;
using ORCA.Main.Extensions;
using ORCA.Main.Util;
using static ORCA.Main.Util.CommonOpUtil;

namespace ORCA.Main.Modules;

[SlashCommand("nsa", "Use features of the Nintendo Switch App [NSA].")]
public class NsaModule(
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

    [SubSlashCommand("about", "Get details about the current Switch User (including Friend Code).")]
    public async Task GetDetails()
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

        var okNsa = await ValidateNsaToken(state);
        await state.DmChannel.SetUserTokens(okNsa);

        var selfRequest = new ShowSelf
        {
            NsaToken = okNsa.NsaToken!,
            Id = JwtUtil.GetSubU64FromNsaToken(okNsa.NsaToken!)
        };
        var result = await _nw.GetSelf(selfRequest, state.Session);

        var response = new EmbedProperties
        {
            Title = result.Name,
            Color = new Color(0x9C142D),
            Thumbnail = new EmbedThumbnailProperties(result.ImageUri),
            Fields =
            [
                new EmbedFieldProperties
                {
                    Name = "Friend Code",
                    Value = $"SW-{result.Links?.FriendCode?.Id}",
                    Inline = false
                },
                new EmbedFieldProperties
                {
                    Name = "ID",
                    Value = result.Id.ToString(),
                    Inline = false
                },
                new EmbedFieldProperties
                {
                    Name = "NSA ID",
                    Value = result.NsaId,
                    Inline = false
                }
            ]
        };

        await Context.Interaction.SendReplyDeferredAsync(response);
    }

    [SubSlashCommand("album_latest", "Upload a copy of your latest clip/screenshot from the NSA Album.")]
    public async Task GetAlbumLatest()
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

        var okNsa = await ValidateNsaToken(state);
        await state.DmChannel.SetUserTokens(okNsa);

        var mediaRequest = new MediaList
        {
            NsaToken = okNsa.NsaToken!,
        };
        var result = await _nw.GetMedia(mediaRequest, state.Session);

        var latestUpload = result.Media.FirstOrDefault();
        if (latestUpload == null)
        {
            await Context.Interaction.SendReplyDeferredAsync("There are currently no uploads in the album.");
            return;
        }

        // Videos are too large to reupload to Discord, so just embed the link.
        if (latestUpload.Type == "video")
        {
            await Context.Interaction.SendReplyDeferredAsync($"[Clip from {latestUpload.AppName}]({latestUpload.ContentUri})");
            return;
        }

        using var resource = await _nw.GetRemoteResourceStream(latestUpload.ContentUri);

        await Context.Interaction.ModifyResponseAsync(response =>
        {
            response.Content = $"Screenshot from {latestUpload.AppName}:";
            response.Attachments = [new AttachmentProperties($"{latestUpload.Id}.jpg", resource.Stream)];
        });
    }

    [SubSlashCommand("friends", "Show friends that are currently online.")]
    public async Task GetFriends()
    {
        const int MAX_DISPLAY = 10;

        await Context.Interaction.Defer();

        var state = new ValidateRequest
        {
            UserId = Context.User.Id,
            DmChannel = await Context.User.GetDMChannelAsync(),
            Session = _sb.GetSession(Context.User.Id),

            DB = _db,
            NW = _nw
        };

        var okNsa = await ValidateNsaToken(state);
        await state.DmChannel.SetUserTokens(okNsa);

        var friendRequest = new FriendList
        {
            NsaToken = okNsa.NsaToken!,
        };
        var result = await _nw.GetFriends(friendRequest, state.Session);

        var onlineFriends = result.Friends
            .TakeWhile(f => f.Presence.State is "ONLINE" or "PLAYING")
            .ToList();

        var embedList = onlineFriends
            .Take(MAX_DISPLAY)
            .Select(friend => new EmbedFieldProperties
            {
                Name = $"__{friend.Name}__",
                Value = friend.Presence.Game?.Name,
                Inline = false
            })
            .ToList();

        var embed = new EmbedProperties
        {
            Title = "Online Friends",
            Thumbnail = "https://iili.io/nK4E5EF.png",
            Color = new Color(0xF7832B),
            Fields = embedList,
            Footer = new EmbedFooterProperties
            {
                Text = $"Displaying {embedList.Count} of {onlineFriends.Count} online friends."
            }
        };

        await Context.Interaction.SendReplyDeferredAsync(embed);
    }

    [SubSlashCommand("playtime", "Show playtime statistics from your most recent games.")]
    public async Task GetPlaytime()
    {
        const int MAX_DISPLAY = 10;

        await Context.Interaction.Defer();

        var state = new ValidateRequest
        {
            UserId = Context.User.Id,
            DmChannel = await Context.User.GetDMChannelAsync(),
            Session = _sb.GetSession(Context.User.Id),

            DB = _db,
            NW = _nw
        };

        var okNsa = await ValidateNsaToken(state);
        await state.DmChannel.SetUserTokens(okNsa);

        var selfRequest = new ShowSelf
        {
            NsaToken = okNsa.NsaToken!,
            Id = JwtUtil.GetSubU64FromNsaToken(okNsa.NsaToken!)
        };
        var selfResult = await _nw.GetSelf(selfRequest, state.Session);

        var playLogRequest = new PlayLogShow
        {
            NsaToken = okNsa.NsaToken!,
            NsaId = selfResult.NsaId
        };
        var result = await _nw.GetPlayLog(playLogRequest, state.Session);

        var embedList = result
            .Take(MAX_DISPLAY)
            .Select(game => new EmbedFieldProperties
            {
                Name = $"__{game.Name}__",
                Value = $"Played for {game.TotalPlayTimeMin / 60} hours.",
                Inline = false
            })
            .ToList();

        var embed = new EmbedProperties
        {
            Title = "Recently Played",
            Color = new Color(0xE60012),
            Thumbnail = "https://iili.io/n2GHPTJ.png",
            Fields = embedList,
            Footer = new EmbedFooterProperties
            {
                Text = $"Displaying {embedList.Count} of {result.Count} recent games."
            }
        };

        await Context.Interaction.SendReplyDeferredAsync(embed);
    }
}
