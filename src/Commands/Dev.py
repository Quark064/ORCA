from discord.ext import commands
from discord import app_commands
import discord

from Models import AppState

class Dev(commands.Cog):
    group = app_commands.Group(
        name="dev",
        description="Developer Debug Commands",
        guild_ids=[920851074116636692]
    )

    def __init__(self, bot: commands.Bot, state: AppState):
        self.bot = bot
        self.state = state

    @group.command(name="online", description="Check the Bot Status")
    async def Online(self, interaction: discord.Interaction):
        await interaction.response.send_message(f"Online, took {round(self.bot.latency*1000)} ms")
    
    @group.command(name="user_count", description="Check the number of signed in users.")
    async def UserCount(self, interaction: discord.Interaction):
        await interaction.response.send_message(f"DB reports `{self.state.DB.Count(self.state.DB.TokenMessageDB)}` users.")