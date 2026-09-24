using Microsoft.Extensions.DependencyInjection;
using NetCord.Gateway;
using NetCord.Services.ApplicationCommands;

namespace ORCA.Main.Extensions;

public static class StartupExtensions
{
    extension(IServiceProvider services)
    {
        public async Task ClearCommands(ulong? guildId)
        {
            var emptyService = new ApplicationCommandService<ApplicationCommandContext>();
            var client = services.GetRequiredService<GatewayClient>();

            await emptyService.RegisterCommandsAsync(
                client.Rest,
                client.Id,
                guildId);
        }

        public async Task RegisterCommands(ulong? guildId)
        {
            var commandService = services.GetRequiredService<ApplicationCommandService<ApplicationCommandContext>>();
            var client = services.GetRequiredService<GatewayClient>();

            await commandService.RegisterCommandsAsync(
                client.Rest,
                client.Id,
                guildId);
        }
    }
}
