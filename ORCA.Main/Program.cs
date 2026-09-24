using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Services.ApplicationCommands;
using ORCA.Main.Bosses;
using ORCA.Main.Exceptions;
using ORCA.Main.Extensions;
using System.Net;

var builder = Host.CreateApplicationBuilder(args);

// Add Middleware
builder.Services
    .AddDiscordGateway()
    .AddMemoryCache()
    .AddApplicationCommands<ApplicationCommandInteraction, ApplicationCommandContext>(
        options =>
        {
            options.ResultHandler = new ModuleExceptionHandler();
        });

// Configure HTTP Clients
builder.Services.AddHttpClient("WebClient", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", builder.Configuration["ORCA:WebUserAgent"]);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestVersion = HttpVersion.Version11;
}).RemoveAllLoggers();

builder.Services.AddHttpClient("ApiClient", client =>
{
    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "NASDKAPI; Android");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestVersion = HttpVersion.Version11;
}).RemoveAllLoggers();

builder.Services.AddHttpClient("AppClient", client =>
{
    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", builder.Configuration["ORCA:AppUserAgent"]);
    client.DefaultRequestVersion = HttpVersion.Version20;
}).RemoveAllLoggers();

// Add DI Services
builder.Services.AddSingleton<DatabaseBoss>();
builder.Services.AddSingleton<NetworkingBoss>();
builder.Services.AddSingleton<SessionBoss>();

var host = builder.Build();
host.AddModules(typeof(Program).Assembly);

// Register Modules with Discord
const ulong guildId = 920851074116636692;
//await host.Services.ClearCommands(guildId);
await host.Services.RegisterCommands(guildId);


await host.RunAsync();
