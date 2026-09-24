using NetCord;
using NetCord.Rest;
using ORCA.Main.Models;
using ORCA.Main.Util;
using System.Text.Json;

namespace ORCA.Main.Extensions;

public static class DiscordExtensions
{
    extension(Interaction interaction)
    {
        public async Task Defer(bool ephemeral = false)
        {
            MessageFlags? flags = null;
            if (ephemeral)
            {
                flags = MessageFlags.Ephemeral;
            }
            
            await interaction.SendResponseAsync(
                InteractionCallback.DeferredMessage(flags)
            );
        }

        public async Task SendReplyDeferredAsync(string msg, bool ephemeral = false)
        {
            await interaction.ModifyResponseAsync(message =>
            {
                message.WithContent(msg);
                if (ephemeral)
                {
                    message.WithFlags(MessageFlags.Ephemeral);
                }
            });
        }

        public async Task SendReplyDeferredAsync(EmbedProperties embed, bool ephemeral = false)
        {
            await interaction.ModifyResponseAsync(message =>
            {
                message.WithEmbeds([embed]);
                if (ephemeral)
                {
                    message.WithFlags(MessageFlags.Ephemeral);
                }
            });
        }
    }

    extension(DMChannel dmChannel)
    {
        public async Task<DMTokens> GetUserTokens(ulong messageId, Memory<byte> aesKey)
        {
            var tokenMsg = await dmChannel.GetMessageAsync(messageId);

            var tokens = CipherUtil.Decrypt(tokenMsg.Content, aesKey.ToArray());
            var result = JsonSerializer.Deserialize<DMTokens>(tokens)!;

            result.MessageId = messageId;
            result.AesKey = aesKey;

            return result;
        }

        public async Task SetUserTokens(DMTokens tokens)
        {
            var tokenMsg = await dmChannel.GetMessageAsync(tokens.MessageId);

            var tokenB64 = CipherUtil.Encrypt
            (
                JsonSerializer.Serialize(tokens),
                tokens.AesKey.ToArray()
            );

            await tokenMsg.ModifyAsync(options =>
            {
                options.Content = tokenB64;
            });
        }

        public async Task<bool> TryDeleteMessage(ulong messageId)
        {
            try
            {
                await dmChannel.DeleteMessageAsync(messageId);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
