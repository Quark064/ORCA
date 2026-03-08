import re

from urllib.parse import parse_qs, urlparse

from discord.ext import commands
from discord import app_commands
import discord
import httpx

import Network
from Models import AppState

class Account(commands.Cog):
    group = app_commands.Group(name="account", description="Manage the linked Nintendo Account")

    def __init__(self, bot: commands.Bot, state: AppState):
        self.bot = bot
        self.state = state
        self.client = httpx.AsyncClient()

    @group.command(name="login_stage_1", description="Get a Nintendo Account login link.")
    async def LoginStage1(self, interaction: discord.Interaction):
        try:
            # Generate a auth pair and save the verifier to the DB.
            pair = Network.NintendoRequest.GenerateLoginPair()
            self.state.DB.Set(self.state.DB.AuthVerifierDB, interaction.user.id, pair.Verifier)

            # Create the login embed with the generated URL.
            embed = discord.Embed(
                title="Login to your Nintendo Account",
                description="\n".join([
                    f"To link the bot to your Nintendo Account, sign into with your Nintendo Account [with this link]({pair.URL}).\n",
                    "On the 'Link your account' page, right-click (desktop) or long-press (mobile) the 'Select this account' button and copy the URL.\n",
                    "Then, run the `/account login_stage_2` command and provide the copied URL to complete the login."
                ]),
                color=0x3EC995
            )
            embed.set_image(url="https://iili.io/qxh1gLv.gif")

            # Check to see if there's a previous login message and remove it to prevent confusion.
            oldLoginMsgId = self.state.DB.Get(self.state.DB.AuthMessageDB, interaction.user.id)
            if oldLoginMsgId is not None:
                await Network.DiscordRequest.AttemptDeleteDmMsg(interaction.user, int(oldLoginMsgId))

            # Send the login message and save the ID.
            dmMsg = await interaction.user.send(embed=embed)
            self.state.DB.Set(self.state.DB.AuthMessageDB, interaction.user.id, dmMsg.id)

            await interaction.response.send_message(
                "✅ Sent login instructions to DM!",
                ephemeral=True
            )

        except discord.Forbidden:
            await interaction.response.send_message(
                "⛔ Couldn't access DMs. Check your DM permissions or rerun the command in O.R.C.A's DMs.",
                ephemeral=True
            )
        except discord.HTTPException:
            await interaction.response.send_message(
                "⛔ There was an issue communicating with Discord. Please try again later.",
                ephemeral=True
            )
        except Exception as ex:
            await interaction.response.send_message(
                f"⛔ An unknown error occurred while sending the login link: `{ex}`.",
                ephemeral=True
            )
    

    @group.command(name="login_stage_2", description="Provide your sign in URL to complete the login.")
    @app_commands.checks.cooldown(1, 5, key=lambda i: i.user.id)
    async def LoginStage2(self, interaction: discord.Interaction, copied_url: str):
        await interaction.response.defer(ephemeral=True)
        
        try:
            # Make sure that the user ran stage 1 before attempting stage 2.
            verifier = self.state.DB.Get(self.state.DB.AuthVerifierDB, interaction.user.id)
            if verifier is None:
                await interaction.followup.send(
                    "Couldn't find your verifier in the database, did you run `/account login_stage_1`?",
                    ephemeral=True
                )
                return
            
            # Delete the old login message if it exists.
            oldLoginMsg = self.state.DB.Get(self.state.DB.AuthMessageDB, interaction.user.id)
            if oldLoginMsg is not None:
                await Network.DiscordRequest.AttemptDeleteDmMsg(interaction.user, int(oldLoginMsg))

            # Pull the code out of the provided URL.
            fragment = urlparse(copied_url).fragment
            params = parse_qs(fragment)
            sessionTokenCode = params.get("session_token_code", [None])[0]
            
            if not copied_url.startswith("npf71b963c1b7b6d119") or sessionTokenCode is None:
                await interaction.followup.send(
                    "This doesn't appear to be a valid login URL. The copied URL should start with `npf`. Please try again.",
                    ephemeral=True
                )
                return

            # Attempt to get a Session Token from Nintendo.
            try:
                sessionToken = await Network.NintendoRequest.GetSessionToken(
                    client = self.client,
                    sessionCode = sessionTokenCode,
                    authVerifier = verifier
                )
            except Exception:
                await interaction.followup.send(
                    "⛔ An error occurred getting a Session Token from Nintendo. Please try from `/account login_stage_1` again.",
                    ephemeral=True
                )
                return
            
            # Send the disclaimer about the DM token storage.
            disclaimer = [
                "In order to avoid saving any login information in the bot, your Nintendo access tokens will be stored and retrieved from this DM."
                "This storage message can be deleted with the `/account logout` command at any time, or you can revoke DM permissions from O.R.C.A."
            ]
            await interaction.user.send("\n".join(disclaimer))

            # Create the token storage message.
            storageMsgId = await Network.TokenManager.CreateTokenMessage(interaction.user)
            self.state.DB.Set(self.state.DB.TokenMessageDB, interaction.user.id, storageMsgId)

            # Set the Session Token into the newly created message.
            tokens = Network.TokenManager.CachedTokens(sessionToken, None, None)
            await Network.TokenManager.SetTokens(interaction.user, storageMsgId, tokens)

            await interaction.followup.send(
                "✅ Signed in successfully!",
                ephemeral=True
            )

        except discord.Forbidden:
            await interaction.followup.send(
                "⛔ Couldn't access DMs. Check your DM permissions or rerun the command in O.R.C.A's DMs.",
                ephemeral=True
            )
        except discord.HTTPException:
            await interaction.followup.send(
                "⛔ There was an issue communicating with Discord. Please try again later.",
                ephemeral=True
            )
        except Exception as ex:
            await interaction.followup.send(
                f"⛔ An unknown error occurred while attempting login: `{ex}`.",
                ephemeral=True
            )


    
                



    async def cog_load(self):
        devGuild = discord.Object(id=self.state.Config.DevGuild)
        self.bot.tree.add_command(self.group, guild=devGuild)
    
    async def cog_unload(self):
        await self.client.aclose()



