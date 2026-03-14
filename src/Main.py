import os

import discord
from discord.ext import commands

from Models import AppConfig, AppState
from Database import KeyValDB

from Commands.Dev import Dev
from Commands.Account import Account
from Commands.S3 import S3

intents = discord.Intents.default()
bot = commands.Bot(command_prefix=commands.when_mentioned, intents=intents)

commandCogs = [Dev, Account, S3]

devMode = False

dbPath = "/app/ORCA.db"
tokenServiceUrl = "localhost:5000"
if devMode:
    dbPath = "ORCA.db"
    tokenServiceUrl = "frontend-nsa:5000"

config = AppConfig(
    NSAVersion = "3.2.1",
    DevGuild = 920851074116636692,
    TokenServiceURL = tokenServiceUrl
)

@bot.event
async def on_ready():
    emojis = await bot.fetch_application_emojis()

    state = AppState(
        Config = config,
        DB = KeyValDB(dbPath),
        EmojiTable = {emoji.name: emoji.id for emoji in emojis}
    )

    for cog in commandCogs:
        await bot.add_cog(cog(bot, state))
    await bot.tree.sync()  
    print(f"Logged in as {bot.user}")

bot.run(os.environ["DISCORD_ORCA_TOKEN"])