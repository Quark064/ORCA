import base64
import time
import io
import re

from datetime import datetime

import mmh3
import orjson
import discord
from discord.ext import commands
from discord import app_commands

import Network

from Models import AppState
from Commands.CommandBase import CommandBase

class S3(CommandBase):
    group = app_commands.Group(
        name="s3",
        description="Splatnet3 Commands"
    )

    battleGroup = app_commands.Group(
        name="battle",
        description="Commands related to battle records.",
        parent=group
    )

    replayGroup = app_commands.Group(
        name="replay",
        description="Commands for uploading and downloading replays.",
        parent=group
    )

    matchTypeLookup = {
        "REGULAR":   "Regular",
        "BANKARA":   "Ranked",
        "FEST":      "Splatfest",
        "LEAGUE":    "League",
        "X_MATCH":   "X",
        "PRIVATE":   "Private",
        "CHALLENGE": "Challenge"
    }

    coopTypeLookup = {
        "BIG_RUN": "BigRun",
        "TEAM_CONTEST": "EggstraWork"
    }

    rankedTypeLookup = {
        "VnNSdWxlLTE=": "SplatZones",   # VsRule-1
        "VnNSdWxlLTI=": "TowerControl", # VsRule-2
        "VnNSdWxlLTM=": "Rainmaker",    # VsRule-3
        "VnNSdWxlLTQ=": "ClamBlitz"     # VsRule-4
    }

    outcomeLookup = {
        "WIN":           "Win",
        "LOSE":          "Lose",
        "EXEMPTED_LOSE": "Lose",
        "DRAW":          "NoContest"
    }

    def __init__(self, bot: commands.Bot, state: AppState):
        super().__init__(bot, state, useHTTP2=True)


    @group.command(name="friends", description="Shows friends that are currently playing online.")
    @app_commands.checks.cooldown(1, 10, key=lambda i: i.user.id)
    async def Friends(self, interaction: discord.Interaction):
        MAX_FRIEND_DISPLAY = 15
        
        await interaction.response.defer()

        # Get and refresh tokens.
        tokens = await self._getAndVerifyTokensHelper(interaction.user, wranglePrivTokens=True)

        # Send GraphQL request.
        assert tokens.Bullet is not None
        queryResult = await Network.NintendoRequest.SendGraphQL(
            self.client,
            self.state.Config.NSAVersion,
            tokens.Bullet,
            Network.BuiltGraphQLOperation.FriendListQuery
        )


        # Build the currently playing friends embed.
        playerList = queryResult["data"]["friends"]["nodes"]
        playingCount = 0

        joinableEmoji = self._iconFromName("Joinable")
        spaceEmoji = self._iconFromName("Space")

        outputList = []
        
        for player in playerList:
            onlineState = player["onlineState"]
            if onlineState == "ONLINE":
                continue
            
            # Results are sorted so online players are at the top.
            if onlineState == "OFFLINE":
                break

            playingCount += 1
            if playingCount >= MAX_FRIEND_DISPLAY:
                continue

            playerName = player["playerName"]
            nickname = player["nickname"]

            if playerName != nickname:
                nickname = f"{playerName} *({nickname})*"
            
            playingIcon = spaceEmoji
            if onlineState == "MINI_GAME_PLAYING":
                playingIcon = self._iconFromName("TableTurf")
            elif player["coopRule"]:
                playingIcon = self._coopModeToEmoji(player["coopRule"])
            elif player["vsMode"]:
                playingIcon = self._vsModeToEmoji(player["vsMode"]["mode"])

            joinable = joinableEmoji if self._isJoinable(onlineState) else ""
            locked = ":lock:" if player["isLocked"] else ""

            outputList.append(f"{playingIcon} **{playerName}** {joinable}{locked}")

        embed = discord.Embed(
            title=f"**Online Friends [{playingCount} of {len(playerList)}]**",
            description="\n".join(outputList),
            color=0x41A0AE
        )
        embed.set_thumbnail(url="https://iili.io/qlTWEWQ.png")
        embed.set_footer(text=f"Displaying {len(outputList)} of {playingCount} playing friends")


        await interaction.followup.send(embed=embed)
    
    @group.command(name="generate_seed_checker_json", description="Generates a JSON file for use on Lean's S3 seed checker.")
    @app_commands.checks.cooldown(1, 900, key=lambda i: i.user.id)
    async def GenerateSeedJson(self, interaction: discord.Interaction):
        await interaction.response.defer()
        startTime = datetime.now()

        # Get and refresh tokens.
        tokens = await self._getAndVerifyTokensHelper(interaction.user, wranglePrivTokens=True)
        assert tokens.Bullet is not None

        # Send GraphQL requests.
        gearResult = await Network.NintendoRequest.SendGraphQL(
            self.client,
            self.state.Config.NSAVersion,
            tokens.Bullet,
            Network.BuiltGraphQLOperation.MyOutfitCommonDataEquipmentsQuery
        )

        historyResult = await Network.NintendoRequest.SendGraphQL(
            self.client,
            self.state.Config.NSAVersion,
            tokens.Bullet,
            Network.BuiltGraphQLOperation.LatestBattleHistoriesQuery
        )

        networkFinishTime = datetime.now()


        # Build and return the result Embed/JSON.
        try:
            encodedPlayerId = historyResult["data"]["latestBattleHistories"]["historyGroupsOnlyFirst"]["nodes"][0]["historyDetails"]["nodes"][0]["player"]["id"]
            decodedId = base64.b64decode(encodedPlayerId).decode("utf-8")
            playerId = decodedId.split(":")[-1]

        except (KeyError, IndexError, TypeError):
            await interaction.followup.send(
                embed = discord.Embed(
                    title = "This command requires you to have played a battle online recently."
                )
            )
            return
        
        h = mmh3.hash(playerId) & 0xFFFFFFFF
        key = base64.b64encode(bytes([k ^ (h & 0xFF) for k in bytes(playerId, "utf-8")]))
        timeStamp = int(time.time())

        result = {
            "key": key.decode("utf-8"),
            "h": h,
            "timestamp": timeStamp,
            "gear": gearResult
        }

        URL_REGEX = re.compile(b"https?://[^ \\t\\n\\r\\f\\v\"']+", re.IGNORECASE)

        resultJsonBytes = orjson.dumps(result)
        trimmedJson = URL_REGEX.sub(b"", resultJsonBytes)
        resultBytes = io.BytesIO(trimmedJson)

        encodingFinishTime = datetime.now()

        embed = discord.Embed(
            title="Using with LeanYoshi's Seed Checker",
            description="\n".join([
                "You can upload this file to the [Splatoon 3 Seed Checker](https://leanny.github.io/splat3seedchecker/#/settings).\n",
                "Use the link above to navigate to user settings.",
                "Please upload this JSON file via `Import from SplatNet` -> `Seed Key and Gear` -> `Upload`."
            ]),
            color=0x3EC995
        )
        embed.set_footer(text=f"Network {(networkFinishTime - startTime).total_seconds():.1f} sec; Processing {(encodingFinishTime - networkFinishTime).total_seconds():.1f} sec")
        embed.set_image(url="https://iili.io/qlV0iOv.png")


        await interaction.followup.send(embed=embed, file=discord.File(resultBytes, f"gear_{timeStamp}.json"))

    @group.command(name="album", description="Displays the most recent picture uploaded to the S3 album.")
    async def GetLatestAlbum(self, interaction: discord.Interaction):
        await interaction.response.defer()

        # Get and refresh tokens.
        tokens = await self._getAndVerifyTokensHelper(interaction.user, wranglePrivTokens=True)
        assert tokens.Bullet is not None

        queryResult = await Network.NintendoRequest.SendGraphQL(
            self.client,
            self.state.Config.NSAVersion,
            tokens.Bullet,
            Network.BuiltGraphQLOperation.PhotoAlbumQuery
        )

        album = queryResult["data"]["photoAlbum"]["items"]["nodes"]

        if len(album) == 0:
            await interaction.followup.send(content="Your album is currently empty.")
            return
        
        
        await interaction.followup.send(content=album[0]["photo"]["url"])


    @battleGroup.command(name="latest", description="Displays results and information about the latest battle.")
    @app_commands.checks.cooldown(1, 10, key=lambda i: i.user.id)
    async def LatestBattle(self, interaction: discord.Interaction):
        await interaction.response.defer()

        # Get and refresh tokens.
        tokens = await self._getAndVerifyTokensHelper(interaction.user, wranglePrivTokens=True)


        # Send GraphQL requests.
        assert tokens.Bullet is not None
        latestVsResult = await Network.NintendoRequest.SendGraphQL(
            self.client,
            self.state.Config.NSAVersion,
            tokens.Bullet,
            Network.BuiltGraphQLOperation.LatestVsResults
        )

        try:
            latestBattleId = latestVsResult["data"]["vsResult"]["historyGroups"]["nodes"][0]["historyDetails"]["nodes"][0]["id"]
        except (KeyError, IndexError, TypeError):
            await interaction.followup.send(
                embed = discord.Embed(
                    title = "No battles found."
                )
            )
            return
        
        queryResult = await Network.NintendoRequest.SendGraphQL(
            self.client,
            self.state.Config.NSAVersion,
            tokens.Bullet,
            Network.BuiltGraphQLOperation.VsHistoryDetailQuery,
            {
                "vsResultId": latestBattleId
            }
        )


        # Build the result embed.
        battle = queryResult["data"]["vsHistoryDetail"]

        vsMode = battle["vsMode"]["mode"]
        vsRuleId = battle["vsRule"]["id"]
        stageName = battle["vsStage"]["name"]
        modeName = battle["vsRule"]["name"]
        judgement = battle["judgement"]
        
        vsModeEmoji = self._vsModeToEmoji(vsMode)
        vsRuleEmoji = self._vsRuleToEmoji(vsRuleId)
        crownEmoji = self._iconFromName("Crown")
        spaceEmoji = self._iconFromName("Space")
        meEmoji = self._iconFromName("Me", True)

        inklingKill = self._iconFromName("SquidKill")
        octolingKill = self._iconFromName("OctoKill")
        inklingDeathLose = self._iconFromName("SquidDeathLose")
        inklingDeathWin = self._iconFromName("SquidDeathWin")
        octolingDeathLose = self._iconFromName("OctoDeathLose")
        octolingDeathWin = self._iconFromName("OctoDeathWin")
        winTeamArrow = self._iconFromName("WinTeamArrow")
        loseTeamArrow = self._iconFromName("LoseTeamArrow")
        
        titleStr = f"{vsModeEmoji}{vsRuleEmoji} **{stageName}** - *{modeName}*"

        if judgement == "DRAW":
            await interaction.followup.send(
                embed = discord.Embed(
                    title = titleStr,
                    description = "Battle was aborted due to a disconnect."
                )
            )
            return


        homeTeam = battle["myTeam"]["players"]
        awayTeam = battle["otherTeams"][0]["players"]

        if judgement == "WIN":
            winningTeam = homeTeam
            losingTeam = awayTeam
        else:
            winningTeam = awayTeam
            losingTeam = homeTeam

        titleEmbed = discord.Embed(
            title=titleStr,
            color=0xE7E710
        )

        winnersEmbed = discord.Embed(
            title="**VICTORY**",
            color=0x1CBEAC
        )
        losersEmbed = discord.Embed(
            title="**DEFEAT**",
            color=0xC43A6E
        )

        for winners, team, embed in ((True, winningTeam, winnersEmbed), (False, losingTeam, losersEmbed)):
            for player in team:
                if player["species"] == "INKLING":
                    killIcon = inklingKill
                    deathIcon = inklingDeathWin if winners else inklingDeathLose
                else:
                    killIcon = octolingKill
                    deathIcon = octolingDeathWin if winners else octolingDeathLose
                
                arrow = winTeamArrow if winners else loseTeamArrow
                
                result = player["result"]
                assistCount = result["assist"]

                manualPadding = "" if result["kill"] >= 10 else " "
                assistSuffix = spaceEmoji if assistCount == 0 else f"*({assistCount})*{manualPadding}"
                spacer = meEmoji if player["isMyself"] else spaceEmoji
                crown = crownEmoji if player["crown"] else ""

                embed.add_field(
                    name=f"{spacer}{self._weaponIdToEmoji(player["weapon"]["id"])} **{player["name"]}** *#{player["nameId"]}* {crown}",
                    value=f"{spaceEmoji * 2} {arrow} {killIcon} **{result["kill"]}** {assistSuffix}{spaceEmoji}{deathIcon} **{result["death"]}**{spaceEmoji}{self._subTypeIdToEmoji(player["weapon"]["specialWeapon"]["id"], winners)} **{result["special"]}**{spaceEmoji}*{player["paint"]}p*",
                    inline=False
                )

        titleEmbed.set_image(url=battle["vsStage"]["image"]["url"])
        losersEmbed.set_footer(text=battle["playedTime"])

        winnersEmbed.set_image(url="https://iili.io/qlrBMxf.png")
        losersEmbed.set_image(url="https://iili.io/qlrBMxf.png")

        await interaction.followup.send(embeds=[titleEmbed, winnersEmbed, losersEmbed])

    @battleGroup.command(name="summary", description="Display the results of the last 10 battles.")
    @app_commands.checks.cooldown(1, 10, key=lambda i: i.user.id)
    async def BattleSummary(self, interaction: discord.Interaction):
        MAX_BATTLE_DISPLAY = 10

        await interaction.response.defer()

        # Get and refresh tokens.
        tokens = await self._getAndVerifyTokensHelper(interaction.user, wranglePrivTokens=True)

        # Send GraphQL request.
        assert tokens.Bullet is not None
        queryResult = await Network.NintendoRequest.SendGraphQL(
            self.client,
            self.state.Config.NSAVersion,
            tokens.Bullet,
            Network.BuiltGraphQLOperation.LatestBattleHistoriesQuery
        )


        # Build the battle result embed.
        summaryData = queryResult["data"]["latestBattleHistories"]["summary"]
        battleData = queryResult["data"]["latestBattleHistories"]["historyGroups"]["nodes"][0]["historyDetails"]["nodes"]

        spaceIcon = self._iconFromName("Space")
        disconnectIcon = self._iconFromName("Disconnect")
        winArrow = self._iconFromName("WinArrow")
        loseArrow = self._iconFromName('LoseArrow')
        
        embed = discord.Embed(
            title=f"**Latest Battles [{winArrow} {summaryData['win']} | {loseArrow} {summaryData['lose']}]**",
            color=0x77F07F
        )
        embed.set_thumbnail(url="https://iili.io/qATDdVn.png")

        for battle in battleData[:MAX_BATTLE_DISPLAY]:
            player = battle["player"]
            weaponEmoji = self._weaponIdToEmoji(player["weapon"]["id"])

            vsMode = battle["vsMode"]["mode"]
            vsRuleId = battle["vsRule"]["id"]
            stageName = battle["vsStage"]["name"]

            outcome = battle["judgement"]
            myResult = battle["myTeam"]["result"]

            rankStr = ""
            scoreStr = ""

            dcWarn = disconnectIcon if outcome in ("EXEMPTED_LOSE", "DRAW") else ""


            # Match DNF
            if outcome == "DRAW":
                scoreStr = "Match Aborted"
            
            # Turf War Mode
            elif myResult["paintPoint"] is not None:
                scoreStr = f"**{myResult['paintPoint']}p**"
            
            # Ranked Battle Mode
            else:
                # Private battles don't have a rank.
                rank = battle["udemae"]
                if rank:
                    rankStr = f"*Rank {rank}*"
                if battle["knockout"] == "WIN":
                    scoreStr = "**KNOCKOUT!**"
                else:
                    scoreStr = f"Score: **{myResult['score']}**"

            modeEmoji = self._vsModeToEmoji(vsMode)
            ruleEmoji = self._vsRuleToEmoji(vsRuleId)
            if ruleEmoji == "":
                ruleEmoji = spaceIcon

            name = f"{modeEmoji}{ruleEmoji} {stageName} {" - " if rankStr else ""}{rankStr} {weaponEmoji}{dcWarn}"
            value = f"{spaceIcon}{self._outcomeToEmoji(outcome)} {scoreStr}"

            embed.add_field(name=name, value=value, inline=False)

        battleCount = len(battleData)
        if battleCount > MAX_BATTLE_DISPLAY:
            embed.set_footer(text=f"...{battleCount - MAX_BATTLE_DISPLAY} more battles...")


        await interaction.followup.send(embed=embed)


    @replayGroup.command(name="show", description="Display your uploaded replays and their codes.")
    @app_commands.checks.cooldown(1, 10, key=lambda i: i.user.id)
    async def ShowReplays(self, interaction: discord.Interaction):
        MAX_REPLAYS = 24

        await interaction.response.defer()

        # Get and refresh tokens.
        tokens = await self._getAndVerifyTokensHelper(interaction.user, wranglePrivTokens=True)
        assert tokens.Bullet is not None

        # Send GraphQL endpoints.
        queryResult = await Network.NintendoRequest.SendGraphQL(
            self.client,
            self.state.Config.NSAVersion,
            tokens.Bullet,
            Network.BuiltGraphQLOperation.ReplayQuery
        )

        # Process results and build the returned embed.
        replays = queryResult["data"]["replays"]["nodes"]

        if len(replays) == 0:
            title = "No replays uploaded."
        else:
            title = "**Uploaded Replays**"

        embed = discord.Embed(
            title=title,
            color=0x41A0AE
        )
        embed.set_thumbnail(url="https://iili.io/qlm6DHg.png")

        winArrowEmoji = self._iconFromName("WinArrow")
        loseArrowEmoji = self._iconFromName('LoseArrow')
        spaceEmoji = self._iconFromName("Space")


        for replayDetail in replays[:MAX_REPLAYS]:
            replay = replayDetail["historyDetail"]
            vsMode = replay["vsMode"]["mode"]
            vsRuleId = replay["vsRule"]["id"]
            stageName = replay["vsStage"]["name"]

            modeEmoji = self._vsModeToEmoji(vsMode)
            ruleEmoji = self._vsRuleToEmoji(vsRuleId)

            if ruleEmoji == "":
                ruleEmoji = spaceEmoji

            if replay["judgement"] == "WIN":
                outcomeEmoji = winArrowEmoji
            else:
                outcomeEmoji = loseArrowEmoji

            embed.add_field(
                name = f"{modeEmoji}{ruleEmoji} **{stageName}**",
                value = f"{spaceEmoji}{outcomeEmoji} `{self._apiReplayToHumanReplay(replayDetail["replayCode"])}`",
                inline = False
            )
        
        embed.set_footer(text=f"Displaying {min(len(replays), MAX_REPLAYS)} of {len(replays)} uploaded replays.")


        await interaction.followup.send(embed=embed)

    @replayGroup.command(name="download", description="Download a given replay code to Splatoon 3.")
    @app_commands.checks.cooldown(1, 3, key=lambda i: i.user.id)
    async def DownloadReplay(self, interaction: discord.Interaction, replay_code: str):
        # Check to make sure it's a plausible code before attempting to send it.
        if not self._isValidReplayCode(replay_code):
            await interaction.response.send_message(content="The given replay code was invalid.")
            return
        
        await interaction.response.defer()

        # Get and refresh tokens.
        tokens = await self._getAndVerifyTokensHelper(interaction.user, wranglePrivTokens=True)
        assert tokens.Bullet is not None

        queryResult = await Network.NintendoRequest.SendGraphQL(
            self.client,
            self.state.Config.NSAVersion,
            tokens.Bullet,
            Network.BuiltGraphQLOperation.DownloadSearchReplayQuery,
            {
                "code": self._humanReplayToApiReplay(replay_code)
            }
        )

        replayDetail = queryResult["data"]["replay"]
        if not replayDetail:
            await interaction.followup.send(content="The given replay code was not found on the server.")
            return
        
        winArrowEmoji = self._iconFromName("WinArrow")
        loseArrowEmoji = self._iconFromName('LoseArrow')
        spaceEmoji = self._iconFromName("Space")

        replay = replayDetail["historyDetail"]
        vsMode = replay["vsMode"]["mode"]
        vsRuleId = replay["vsRule"]["id"]
        stageName = replay["vsStage"]["name"]

        modeEmoji = self._vsModeToEmoji(vsMode)
        ruleEmoji = self._vsRuleToEmoji(vsRuleId)

        if replay["judgement"] == "WIN":
            outcomeEmoji = winArrowEmoji
        else:
            outcomeEmoji = loseArrowEmoji

        embed = discord.Embed(
            title="Replay sent to Splatoon 3",
            color=0x41A0AE
        )
        embed.set_thumbnail(url="https://iili.io/qlm6DHg.png")

        embed.add_field(
            name = f"{modeEmoji}{ruleEmoji} **{stageName}**",
            value = f"{spaceEmoji}{outcomeEmoji} `{self._apiReplayToHumanReplay(replayDetail["replayCode"])}`",
            inline = False
        )

        uploader = replay["player"]
        embed.set_footer(text=f"Replay uploaded by {uploader["name"]} #{uploader["nameId"]}")
        

        await interaction.followup.send(embed=embed)


    # Helpers -----------------------------------------------------------------
    def _iconFromName(self, emojiName: str, animated: bool = False) -> str:
        return f"<{"a" if animated else ""}:{emojiName}:{self.state.EmojiTable[emojiName]}>"
    
    def _weaponIdToEmoji(self, weaponId: str) -> str:
        decBytes = base64.b64decode(weaponId)
        decStr = decBytes.decode().replace("-", "")
        
        return self._iconFromName(decStr)
    
    def _subTypeIdToEmoji(self, specialId: str, win: bool = True):
        decBytes = base64.b64decode(specialId)
        decStr = decBytes.decode().replace("-", "")
        suffix = "_Win" if win else "_Lose"

        return self._iconFromName(f"{decStr}{suffix}")

    def _coopModeToEmoji(self, coopMode: str) -> str:
        emojiName = self.coopTypeLookup.get(coopMode, "SalmonRun")
        
        emojiName = f"{emojiName}Coop"
        return self._iconFromName(emojiName)
    
    def _vsModeToEmoji(self, vsMode: str) -> str:
        emojiName = self.matchTypeLookup.get(vsMode, "")

        if emojiName == "":
            return self._iconFromName("Space")
        
        emojiName = f"{emojiName}Battle"
        return self._iconFromName(emojiName)

    def _vsRuleToEmoji(self, rule: str) -> str:
        emojiName = self.rankedTypeLookup.get(rule, "")

        if emojiName == "":
            return emojiName
        
        emojiName = f"{emojiName}Mode"
        return self._iconFromName(emojiName)

    def _outcomeToEmoji(self, outcome: str) -> str:
        emojiName = self.outcomeLookup.get(outcome, "?")

        if emojiName == "?":
            return emojiName
        
        return self._iconFromName(f"{emojiName}Arrow")

    def _isJoinable(self, onlineState: str):
        return onlineState in ("VS_MODE_MATCHING", "COOP_MODE_MATCHING")
    
    def _colorToHex(self, color: dict) -> int:
        r = int(color['r'] * 255) & 0xFF
        g = int(color['g'] * 255) & 0xFF
        b = int(color['b'] * 255) & 0xFF

        return (r << 16) | (g << 8) | b
    
    def _apiReplayToHumanReplay(self, apiReplayCode: str) -> str:
        return "-".join(apiReplayCode[i:i+4] for i in range(0, len(apiReplayCode), 4))
    
    def _humanReplayToApiReplay(self, humanApiCode: str) -> str:
        return humanApiCode.replace('-', '').upper()
    
    def _isValidReplayCode(self, humanApiCode: str) -> bool:
        apiCode = self._humanReplayToApiReplay(humanApiCode)

        return len(apiCode) == 16 and apiCode.isalnum()