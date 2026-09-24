using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using ORCA.Main.Extensions;

namespace ORCA.Main.Exceptions;

public sealed class ModuleExceptionHandler() : IApplicationCommandResultHandler<ApplicationCommandContext>
{
    public async ValueTask HandleResultAsync(
        IExecutionResult result,
        ApplicationCommandContext context,
        GatewayClient? client,
        ILogger logger,
        IServiceProvider services)
    {
        if (result is IExceptionResult { Exception: var exception })
        {
            switch (exception)
            {
                case AccountExceptions.UserAlreadyExists:
                    await context.Interaction.SendReplyDeferredAsync(
                        "You appear to already be signed in. Please use `/account logout` command to sign out, and try again.", true);
                    break;

                case AccountExceptions.InvalidUrl:
                    await context.Interaction.SendReplyDeferredAsync(
                        "The provided URL was not valid. A correct URL will start with `npf`. Please try again.", true);
                    break;

                case AccountExceptions.AuthVerifierMissing:
                    await context.Interaction.SendReplyDeferredAsync(
                        "Couldn't find your verifier in the database. Did you run `/account login_step_1`?", true);
                    break;

                case AccountExceptions.AesKeyMissing:
                    await context.Interaction.SendReplyDeferredAsync(
                        "The AES key required to decrypt your tokens is missing. Please readd your account with `/account logout`, and `/account login_step_1`.", true);
                    break;

                case AccountExceptions.SessionTokenMissing:
                    await context.Interaction.SendReplyDeferredAsync(
                        "You don't appear to be signed in. You can sign in with the `/account login_step_1` command.", true);
                    break;

                case AccountExceptions.SessionTokenExpired:
                    await context.Interaction.SendReplyDeferredAsync(
                        "Your Session Token has expired. Please sign in again with the `/account login_step_1` command.", true);
                    break;



                case NetworkExceptions.SessionTokenGetFailure:
                    await context.Interaction.SendReplyDeferredAsync(
                        "Failed to get a Session Token from Nintendo. Please try again from `/account login_step_1`.", true);
                    break;

                case NetworkExceptions.ApiTokensGetFailure:
                    await context.Interaction.SendReplyDeferredAsync(
                        "Failed to exchange Session Token with Nintendo. Please try again later, or try re-adding your account.", true);
                    break;

                case NetworkExceptions.NsaTokenGetFailure:
                    await context.Interaction.SendReplyDeferredAsync(
                        "Failed to authenticate with Nintendo Switch App services. Please try again later.", true);
                    break;

                case NetworkExceptions.MediaGetFailure:
                    await context.Interaction.SendReplyDeferredAsync(
                        "Failed to get uploaded media from Nintendo. Please try again later.", true);
                    break;

                case NetworkExceptions.SelfGetFailure:
                    await context.Interaction.SendReplyDeferredAsync(
                        "Failed to get information about the current user. Please try again later.", true);
                    break;

                case NetworkExceptions.FriendGetFailure:
                    await context.Interaction.SendReplyDeferredAsync(
                        "Failed to get friend list from Nintendo. Please try again later.", true);
                    break;

                case NetworkExceptions.PlayLogShowGetFailure:
                    await context.Interaction.SendReplyDeferredAsync(
                        "Failed to play time information from Nintendo. Please try again later.", true);
                    break;

                case NetworkExceptions.GameWebTokenGetFailure:
                    await context.Interaction.SendReplyDeferredAsync(
                        "Failed to get applet access token from Nintendo. Please try again later.", true);
                    break;

                case NetworkExceptions.BulletTokenGetFailure:
                    await context.Interaction.SendReplyDeferredAsync(
                        "Failed to get Splatnet3 access token from Nintendo. Please try again later.", true);
                    break;

                case NetworkExceptions.RemoteResourceGetFailure:
                    await context.Interaction.SendReplyDeferredAsync(
                        "Failed to stream the media from Nintendo to Discord. Please try again later.", true);
                    break;



                case UtilExceptions.SubClaimJwtParseFailure:
                    await context.Interaction.SendReplyDeferredAsync(
                        "Failed to parse the sub claim from the NSA Token. You may need to re-add your account.", true);
                    break;


                case RestException { Error.Code: 50007 }: // Permissions Missing
                case RestException { Error.Code: 50278 }: // No Shared Guild
                    await context.Interaction.SendReplyDeferredAsync(
                        "Failed to access DMs. Please add ORCA to your apps to grant this permission and try again.", true);
                    break;



                default:
                    logger.LogError(exception, "An unhandled command exception was occurred!");
                    await context.Interaction.SendReplyDeferredAsync(
                        "An unhandled exception was reported and logged. Please try again later.", true);
                    break;
            }
        }
    }
}
