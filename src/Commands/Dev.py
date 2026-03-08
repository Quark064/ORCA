from discord.ext import commands
from discord import app_commands
import discord

from Models import AppState

class Dev(commands.Cog):
    group = app_commands.Group(name="dev", description="Developer Debug Commands")

    def __init__(self, bot: commands.Bot, state: AppState):
        self.bot = bot
        self.state = state

    @group.command(name="online", description="Check the Bot Status")
    async def Online(self, interaction: discord.Interaction):
        await interaction.response.send_message(f"Online, took {round(self.bot.latency*1000)} ms")
    
    async def cog_load(self):
        devGuild = discord.Object(id=self.state.Config.DevGuild)
        self.bot.tree.add_command(self.group, guild=devGuild)