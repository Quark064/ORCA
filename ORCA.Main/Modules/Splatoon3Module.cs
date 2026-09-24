using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NSA.Network.Core.Api.v4;
using ORCA.Main.Bosses;
using ORCA.Main.Extensions;
using ORCA.Main.Util;
using System;
using System.Collections.Generic;
using System.Text;
using static ORCA.Main.Util.CommonOpUtil;

namespace ORCA.Main.Modules;


[SlashCommand("s3", "Use Splatnet3 features [must have an active NSO subscription and Splatoon 3].")]
public class Splatoon3Module(
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

    [SubSlashCommand("debug", "Temp token debugging function.")]
    public async Task GetPlaytime()
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

        var okBullet = await ValidateBulletToken(state);
        await state.DmChannel.SetUserTokens(okBullet);


        await Context.Interaction.SendReplyDeferredAsync("Okay");
    }
}