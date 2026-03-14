import secrets
from urllib.parse import parse_qs, urlparse

from ff3 import FF3Cipher
from discord.ext import commands
from discord import app_commands
import discord

import Network
import Database
from Models import AppState
from Commands.CommandBase import CommandBase

class Account(CommandBase):
    group = app_commands.Group(name="account", description="Manage the linked Nintendo Account")

    def __init__(self, bot: commands.Bot, state: AppState):
        super().__init__(bot, state)

    @group.command(name="login_stage_1", description="Get a Nintendo Account login link.")
    async def LoginStage1(self, interaction: discord.Interaction):
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
    
    @group.command(name="login_stage_2", description="Provide your sign in URL to complete the login.")
    @app_commands.checks.cooldown(1, 5, key=lambda i: i.user.id)
    async def LoginStage2(self, interaction: discord.Interaction, copied_url: str):
        await interaction.response.defer(ephemeral=True)

        # Make sure that the user ran stage 1 before attempting stage 2.
        verifier = self.state.DB.Get(self.state.DB.AuthVerifierDB, interaction.user.id)
        if verifier is None:
            raise Database.MissingAuthVerifier()
        
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
        sessionToken = await Network.NintendoRequest.GetSessionToken(
            client = self.client,
            sessionCode = sessionTokenCode,
            authVerifier = verifier
        )
            
        # Send the disclaimer about the DM token storage.
        disclaimer = [
            "In order to avoid saving any login information in the bot, your Nintendo access tokens are encrypted and stored in this DM.",
            "This storage message can be deleted with the `/account logout` command at any time. When you run a command, these keys are fetched and decrypted.",
            "These tokens are never cached in O.R.C.A. and are sent directly to Nintendo - either by O.R.C.A. or its accompanying token synthesis process."
        ]
        await interaction.user.send("\n".join(disclaimer))

        # Create the token storage message.
        oldTokenStore = self.state.DB.Get(self.state.DB.TokenMessageDB, interaction.user.id)
        if oldTokenStore is not None:
            await Network.DiscordRequest.AttemptDeleteDmMsg(interaction.user, int(oldTokenStore))
        
        # Create a unique encryption key for the tokens.
        keyStr = secrets.token_hex(24).upper()
        cipher = Network.TokenManager.CreateCipher(keyStr)

        # Create the token message.
        storageMsgId = await Network.TokenManager.CreateTokenMessage(interaction.user, cipher)
        
        # Save parameters to the database.
        self.state.DB.Set(self.state.DB.TokenMessageDB, interaction.user.id, storageMsgId)
        self.state.DB.Set(self.state.DB.TokenEncryptKeyDB, interaction.user.id, keyStr)

        # Set the Session Token into the newly created message.
        tokens = Network.TokenManager.CachedTokens(sessionToken, None, None)
        await Network.TokenManager.SetTokens(interaction.user, storageMsgId, tokens, cipher)

        await interaction.followup.send(
            "✅ Successfully signed in.",
            ephemeral=True
        )

    @group.command(name="about_me", description="Get information about the current Nintendo Account.")
    @app_commands.checks.cooldown(1, 5, key=lambda i: i.user.id)
    async def AboutMe(self, interaction: discord.Interaction):
        await interaction.response.defer()

        tokens = await self._getAndVerifyTokensHelper(interaction.user)

        # Attempt to get the Access Token from Nintendo.
        assert tokens.Session is not None
        connectTokens = await Network.NintendoRequest.GetConnectTokens(self.client, tokens.Session)
        
        # Get User Info from Nintendo using the access token.
        userInfo = await Network.NintendoRequest.GetUserInfo(self.client, connectTokens.Access)
        
        # Send the resulting embed.
        embed = discord.Embed(
            title=userInfo.Nickname,
            color=0x41A0AE
        )
        embed.set_thumbnail(url=userInfo.IconURI)
        embed.add_field(name="ID", value=userInfo.ID, inline=False)
        embed.add_field(
            name="Account Created",
            value=f"{userInfo.CreatedAt.strftime('%B')} {userInfo.CreatedAt.day}, {userInfo.CreatedAt.year}",
            inline=False
        )

        await interaction.followup.send(embed=embed)

    
    @group.command(name="logout", description="Unlink your Nintendo Account.")
    @app_commands.checks.cooldown(1, 5, key=lambda i: i.user.id)
    async def Logout(self, interaction: discord.Interaction):
        await interaction.response.defer()

        # Get all messages linked to the current sign in.
        messages = [
            self.state.DB.Get(self.state.DB.AuthMessageDB, interaction.user.id),
            self.state.DB.Get(self.state.DB.TokenMessageDB, interaction.user.id)
        ]

        # Delete the fetched messages.
        for message in messages:
            if message is not None:
                await Network.DiscordRequest.AttemptDeleteDmMsg(interaction.user, int(message))
            
        # Clear all user information from the database.
        self.state.DB.Del(self.state.DB.AuthMessageDB, interaction.user.id)
        self.state.DB.Del(self.state.DB.TokenMessageDB, interaction.user.id)
        self.state.DB.Del(self.state.DB.AuthVerifierDB, interaction.user.id)
            
        await interaction.followup.send("✅ Successfully signed out.")
    