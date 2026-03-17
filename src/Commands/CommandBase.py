import time

import discord
import httpx
from discord.ext import commands
from discord import app_commands

import Network
import Database
from Models import AppState

class CommandBase(commands.Cog):
    def __init__(self, bot: commands.Bot, state: AppState, useHTTP2=False):
        self.bot = bot
        self.state = state
        self.client = httpx.AsyncClient(http2=useHTTP2)
    
    async def _getAndVerifyTokensHelper(
        self,
        user: discord.User | discord.Member,
        wranglePrivTokens = False
    ) -> Network.TokenManager.CachedTokens:
            tokenMsg = self.state.DB.Get(self.state.DB.TokenMessageDB, user.id)
            if tokenMsg is None:
                raise Database.MissingTokenMessage()
            
            # Get the token decryption key, and sign the user out if it was not found.
            key = self.state.DB.Get(self.state.DB.TokenEncryptKeyDB, user.id)
            if not key:
                await Network.DiscordRequest.AttemptDeleteDmMsg(user, int(tokenMsg))
                self.state.DB.Del(self.state.DB.TokenMessageDB, user.id)
                
                raise Database.MissingTokenKey()
            
            cipher = Network.TokenManager.CreateCipher(key)

            
            # Fetch the tokens from Discord using the message ID.
            tokens = await Network.TokenManager.GetTokens(user, int(tokenMsg), cipher)

            # Throw an error if the Session Token is missing or expired.
            if not tokens.Session:
                raise Network.TokenManager.SessionMissingFromStore()
            if Network.TokenManager.IsTokenExpired(tokens.Session):
                raise Network.TokenManager.SessionExpired()
            
            bulletExp = self.state.DB.Get(self.state.DB.BulletExpDB, user.id)

            # Check and update the GameWeb/Bullet tokens if necessary.
            if wranglePrivTokens:
                
                # Tokens are missing or expired, perform a full refresh.
                if not tokens.GameWeb or not tokens.Bullet or not bulletExp or Network.TokenManager.IsTokenExpired(tokens.GameWeb):
                    refreshedTokens = await Network.TokenSynthRequest.GetPrivilegedTokens(self.client, tokens.Session, self.state.Config.TokenServiceURL)
                    tokens.GameWeb = refreshedTokens.GameWeb
                    tokens.Bullet = refreshedTokens.Bullet
                    self.state.DB.Set(self.state.DB.BulletExpDB, user.id, int(time.time() + 7000))
                    await Network.TokenManager.SetTokens(user, int(tokenMsg), tokens, cipher)
                
                # GameWeb is valid but Bullet Token has expired, perform a partial refresh.
                elif time.time() >= int(bulletExp):
                    tokens.Bullet = await Network.NintendoRequest.GetBulletToken(self.client, self.state.Config.NSAVersion, tokens.GameWeb) 
                    self.state.DB.Set(self.state.DB.BulletExpDB, user.id, int(time.time() + 7000))
                    await Network.TokenManager.SetTokens(user, int(tokenMsg), tokens, cipher)

            return tokens


    async def cog_unload(self):
        await self.client.aclose()

    async def cog_app_command_error(
        self,
        interaction: discord.Interaction,
        error: app_commands.AppCommandError
    ):
        if isinstance(error, app_commands.CommandOnCooldown):
            await self._sendError(
                interaction,
                f"This command is on cooldown. Try again in {error.retry_after:.1f}s."
            )
        
        # General Discord Errors ------------
        elif isinstance(error, discord.Forbidden):
            await self._sendError(
                interaction,
                "Couldn't access DMs. Add O.R.C.A. to your apps to grant this permission and try again."
            )
        elif isinstance(error, discord.HTTPException):
            await self._sendError(
                interaction,
                "There was an issue communicating with Discord. Please try again later."
            )

        # Synth Endpoint Errors -------------
        elif isinstance(error, Network.TokenSynthRequest.SynthException):
            await self._sendError(
                interaction,
                f"Received an error from the Token Synth Endpoint: `{error}`. Please try again later."
            )
        elif isinstance(error, Network.TokenSynthRequest.NoBulletAccessException):
            await self._sendError(
                interaction,
                f"This service requires Splatoon 3 to have been played on this account and an active NSO subscription."
            )

        # Nintendo Request Errors -----------
        elif isinstance(error, Network.NintendoRequest.SessionTokenException):
            await self._sendError(
                interaction,
                "An error occurred getting a Session Token from Nintendo. Please try signing in again."
            )
        elif isinstance(error, Network.NintendoRequest.ConnectTokenException):
            await self._sendError(
                interaction,
                "Unable to get Access/ID Tokens from Nintendo with the current Session Token. Please try signing in again."
            )
        elif isinstance(error, Network.NintendoRequest.UserInfoException):
            await self._sendError(
                interaction,
                "Unable to get User Info from the received Access Token. Please try again later."
            )
        elif isinstance(error, Network.NintendoRequest.GraphQLOperation):
            await self._sendError(
                interaction,
                "An error occurred while executing the GraphQL request. Please try again later."
            )

        # Token Management Errors -----------
        elif isinstance(error, Network.TokenManager.SessionMissingFromStore):
            await self._sendError(
                interaction,
                "Token Store is present but the Session Token is missing. Please sign in again."
            )
        elif isinstance(error, Network.TokenManager.SessionExpired):
            await self._sendError(
                interaction,
                "The Session Token in the DM store is expired. Please try signing in again."
            )

        # Database Errors -------------------
        elif isinstance(error, Database.MissingAuthVerifier):
            await self._sendError(
                interaction,
                "Couldn't find your verifier in the database, did you run `/account login_stage_1`?"
            )
        elif isinstance(error, Database.MissingTokenMessage):
            await self._sendError(
                interaction,
                "You don't appear to be signed in. Please add O.R.C.A. to your apps, run `/account login_stage_1`, follow the login instructions, and try again."
            )
        elif isinstance(error, Database.MissingTokenKey):
            await self._sendError(
                interaction,
                "The key to decrypt your DM tokens was not found in the database. Please sign in again."
            )

        else:
            await self._sendError(
                interaction,
                f"An unknown error occurred: `{error}`"
            )
    

    async def _sendError(self, interaction: discord.Interaction, message: str):
        message = f"⛔ {message}"

        if interaction.response.is_done():
            await interaction.followup.send(message, ephemeral=True)
        else:
            await interaction.response.send_message(message, ephemeral=True)