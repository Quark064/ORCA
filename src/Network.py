import random
import string
import time

import discord
import httpx
import jwt

from discord import app_commands

from os import urandom
from urllib.parse import urlencode
from base64 import urlsafe_b64encode
from hashlib import sha256
from dataclasses import dataclass
from enum import Enum
from datetime import datetime, timezone

class DiscordRequest:
    @staticmethod
    async def AttemptDeleteDmMsg(user: discord.User | discord.Member, msgId: int) -> bool:
        try:
            dm = await user.create_dm()
            msg = await dm.fetch_message(msgId)
            await msg.delete()

            return True
        
        except Exception:
            return False
    
    @staticmethod
    async def CreatePinnedDmMsg(user: discord.User | discord.Member, msg: str) -> int:
        msgObj = await user.send(msg)
        await msgObj.pin()

        return msgObj.id


class TokenManager:
    DELIMITER = "|"

    class SessionMissingFromStore(app_commands.AppCommandError):
        pass
    
    class SessionExpired(app_commands.AppCommandError):
        pass

    class Token(Enum):
        SESSION_TOKEN = 0
        GAME_WEB_TOKEN = 1
        BULLET_TOKEN = 2

    @dataclass
    class CachedTokens:
        Session: str | None
        GameWeb: str | None
        Bullet: str | None


    @staticmethod
    async def CreateTokenMessage(user: discord.User | discord.Member) -> int:
        return await DiscordRequest.CreatePinnedDmMsg(user, TokenManager.DELIMITER * (len(TokenManager.Token) - 1))
    
    @staticmethod
    async def GetTokens(user: discord.User | discord.Member, tokenMsgId: int) -> TokenManager.CachedTokens:
        dm = await user.create_dm()
        msg = await dm.fetch_message(tokenMsgId)

        tokens = msg.content.split(TokenManager.DELIMITER)
        
        return TokenManager.CachedTokens(
            Session = tokens[TokenManager.Token.SESSION_TOKEN.value],
            GameWeb = tokens[TokenManager.Token.GAME_WEB_TOKEN.value],
            Bullet = tokens[TokenManager.Token.BULLET_TOKEN.value]
        )
    
    @staticmethod
    async def SetTokens(user: discord.User | discord.Member, tokenMsgId: int, tokens: CachedTokens) -> None:
        dm = await user.create_dm()
        msg = await dm.fetch_message(tokenMsgId)

        currTokens = msg.content.split(TokenManager.DELIMITER)
        newTokens = [tokens.Session, tokens.GameWeb, tokens.Bullet]

        for i in TokenManager.Token:
            val = newTokens[i.value]
            if val is not None:
                currTokens[i.value] = val

        mergedResult = TokenManager.DELIMITER.join(currTokens)

        await msg.edit(content=mergedResult)

    @staticmethod
    def IsTokenExpired(token: str):
        payload = jwt.decode(
            token,
            options={"verify_signature": False}
        )

        exp = payload.get("exp")
        if exp is None:
            return False

        return exp < (time.time() + 5)

class NintendoRequest:
    # Generate Login URL ----------
    @dataclass
    class LoginPair:
        URL: str
        Verifier: str

    @staticmethod
    def GenerateLoginPair() -> LoginPair:
        REQUEST_URL = "https://accounts.nintendo.com/connect/1.0.0/authorize"

        authState = urlsafe_b64encode(urandom(36))
        
        authVerifier = urlsafe_b64encode(urandom(32))
        authVerifier = authVerifier.replace(b"=", b"")
        
        authCVHash = sha256()
        authCVHash.update(authVerifier)
        
        authChallenge = urlsafe_b64encode(authCVHash.digest())
        authChallenge = authChallenge.replace(b"=", b"")

        body = {
        	"state":                               authState,
        	"redirect_uri":                        "npf71b963c1b7b6d119://auth",
        	"client_id":                           "71b963c1b7b6d119",
        	"scope":                               "openid user user.birthday user.mii user.screenName",
        	"response_type":                       "session_token_code",
        	"session_token_code_challenge":        authChallenge,
        	"session_token_code_challenge_method": "S256",
        	"theme":                               "login_form"
        }

        return NintendoRequest.LoginPair(
            URL=f"{REQUEST_URL}?{urlencode(body)}",
            Verifier=authVerifier.decode()
        )
    

    # Get Session Token -----------
    class SessionTokenException(app_commands.AppCommandError):
        pass

    @staticmethod
    async def GetSessionToken(
        client: httpx.AsyncClient,
        sessionCode: str,
        authVerifier: str
    ) -> str:
        try:
            REQUEST_URL = "https://accounts.nintendo.com/connect/1.0.0/api/session_token"

            headers = {
                "User-Agent":      NintendoRequest._getRandomUserAgent(),
                "Accept-Language": "en-US",
                "Accept":          "application/json",
                "Accept-Encoding": "gzip"
            }

            body = {
                "client_id":                   "71b963c1b7b6d119",
                "session_token_code":          sessionCode,
                "session_token_code_verifier": authVerifier
            }

            resp = await client.post(REQUEST_URL, headers=headers, data=body)
            jsonResp = resp.json()

            return jsonResp["session_token"]
        except Exception:
            raise NintendoRequest.SessionTokenException()
    

    # Get Access/ID Token ---------
    class ConnectTokenException(app_commands.AppCommandError):
        pass

    @dataclass
    class ConnectTokens:
        ID: str
        Access: str

    @staticmethod
    async def GetConnectTokens(
        client: httpx.AsyncClient,
        sessionToken: str
    ) -> NintendoRequest.ConnectTokens:
        
        try:
            REQUEST_URL = "https://accounts.nintendo.com/connect/1.0.0/api/token"

            headers = {
                "User-Agent":      NintendoRequest._getRandomUserAgent(),
                "Accept":          "application/json",
                "Accept-Encoding": "gzip"
            }

            body = {
                "client_id": "71b963c1b7b6d119",
                "session_token": sessionToken,
                "grant_type": "urn:ietf:params:oauth:grant-type:jwt-bearer-session-token"
            }

            resp = await client.post(REQUEST_URL, headers=headers, json=body)
            jsonResp = resp.json()

            return NintendoRequest.ConnectTokens(
                ID = jsonResp["id_token"],
                Access = jsonResp["access_token"]
            )
        except Exception:
            raise NintendoRequest.ConnectTokenException()
    
    
    # Get User Info ---------------
    class UserInfoException(app_commands.AppCommandError):
        pass

    @dataclass
    class UserInfo:
        ID: str
        Birthday: str
        Gender: str
        Nickname: str
        IconURI: str
        Country: str
        IsChild: bool
        CreatedAt: datetime
        Language: str

    @staticmethod
    async def GetUserInfo(
        client: httpx.AsyncClient,
        accessToken: str
    ) -> NintendoRequest.UserInfo:
        
        try:
            REQUEST_URL = "https://api.accounts.nintendo.com/2.0.0/users/me"

            headers = {
                "User-Agent":      "NASDKAPI; Android",
                "Accept-Language": "en-US",
                "Accept":          "application/json",
                "Authorization":   f"Bearer {accessToken}",
                "Accept-Encoding": "gzip"
            }

            resp = await client.get(REQUEST_URL, headers=headers)
            jsonResp = resp.json()

            return NintendoRequest.UserInfo(
                ID        = jsonResp["id"],
                Birthday  = jsonResp["birthday"],
                Gender    = jsonResp["gender"],
                Nickname  = jsonResp["nickname"],
                IconURI   = jsonResp["iconUri"],
                Country   = jsonResp["country"],
                IsChild   = jsonResp["isChild"],
                CreatedAt = datetime.fromtimestamp(jsonResp["createdAt"], timezone.utc),
                Language  = jsonResp["language"]
            )
        except Exception:
            raise NintendoRequest.UserInfoException()


    # Get Bullet Token
    class BulletTokenException(app_commands.AppCommandError):
        pass

    @staticmethod
    async def GetBulletToken(
        client: httpx.AsyncClient,
        nsaVersion: str,
        gameWebToken: str
    ) -> str:
        try:
            REQUEST_URL = "https://api.lp1.av5ja.srv.nintendo.net/api/bullet_tokens"

            headers = {
                "x-app-ver": nsaVersion,
                "x-gamewebtoken": gameWebToken,
                "accept-language": "en-US",
                "content-type": "application/json",
                "content-length": "0",
                "accept-encoding": "gzip",
                "user-agent": f"com.nintendo.znca/{nsaVersion}(Android/12)"
            }

            resp = await client.post(REQUEST_URL, headers=headers)
            respObj = resp.json()

            return respObj["bulletToken"]
        
        except Exception:
            raise NintendoRequest.BulletTokenException()


    # Send a GraphQL Query
    class GraphQLException(app_commands.AppCommandError):
        pass

    @dataclass
    class GraphQLOperation:
        Name: str
        Hash: str
        Lang: str = "en-US"

    @staticmethod
    async def SendGraphQL(
        client: httpx.AsyncClient,
        nsaVersion: str,
        bulletToken: str,
        query: NintendoRequest.GraphQLOperation,
        queryVars: dict = {}
    ) -> dict:
        
        try:
            REQUEST_URL = "https://api.lp1.av5ja.srv.nintendo.net/api/graphql"

            headers = {
                "x-apollo-operation-id": query.Hash,
                "x-apollo-operation-name": query.Name,
                "accept": "multipart/mixed; deferSpec=20220824, application/json",
                "x-app-ver": nsaVersion,
                "authorization": f"Bearer {bulletToken}",
                "accept-language": query.Lang,
                "user-agent": f"com.nintendo.znca/{nsaVersion}(Android/12)"
            }

            body = {
                "operationName": query.Name,
                "variables": queryVars,
                "extensions": {
                    "persistedQuery": {
                        "version": 1,
                        "sha256Hash": query.Hash
                    }
                }
            }

            resp = await client.post(REQUEST_URL, headers=headers, json=body)
            
            return resp.json()
        
        except Exception:
            raise NintendoRequest.GraphQLException()

    # Private Helpers -------------
    @staticmethod
    def _getRandomUserAgent() -> str:
        brands = {
            "pixel": {
                "devices": ["Pixel 3", "Pixel 4", "Pixel 5", "Pixel 6", "Pixel 6 Pro"],
                "build": lambda d: random.choice([
                    "SP1A.210812.015",
                    "SP1A.210812.016",
                    "SP1A.210812.016.A1",
                    "SP1A.210812.016.B2",
                    "SP1A.210812.016.C2"
                ])
            },
            "samsung": {
                "devices": {
                    "SM-G991B": "G991B",
                    "SM-G996B": "G996B",
                    "SM-G998B": "G998B",
                    "SM-A528B": "A528B"
                },
                "build": lambda d: (
                    f"SP1A.210812.016."
                    f"{brands["samsung"]["devices"][d]}"
                    f"XXU{random.randint(1,4)}"
                    f"CV{random.choice(string.ascii_uppercase)}"
                    f"{random.randint(1,9)}"
                )
            },
            "oneplus": {
                "devices": ["LE2115", "LE2125"],
                "build": lambda d: f"RKQ1.201217.002_{random.randint(11,13)}.C.{random.randint(20,40)}"
            },
            "xiaomi": {
                "devices": ["M2012K11AG", "M2101K6G"],
                "build": lambda d: f"SKQ1.211006.001.V12.5.{random.randint(1,20)}.0"
            },
            "motorola": {
                "devices": ["XT2131-1", "XT2143-1"],
                "build": lambda d: f"SP1A.210812.016.{random.choice(string.ascii_uppercase)}{random.randint(1,5)}"
            }
        }

        brand = random.choice(list(brands.values()))
        devices = brand["devices"]

        device = random.choice(list(devices)) if isinstance(devices, dict) else random.choice(devices)
        build = brand["build"](device)

        return f"Dalvik/2.1.0 (Linux; U; Android 12; {device} Build/{build})"


class TokenSynthRequest:
    class SynthException(app_commands.AppCommandError):
        pass

    class NoBulletAccessException(app_commands.AppCommandError):
        pass

    @dataclass
    class PrivilegedTokens:
        GameWeb: str
        Bullet: str

    @staticmethod
    async def GetPrivilegedTokens(
        client: httpx.AsyncClient,
        sessionToken: str
    ) -> TokenSynthRequest.PrivilegedTokens:
        try:
            HOST_SERVER = "http://localhost:5000/login"

            params = {
                "session_token": sessionToken
            }

            resp = await client.get(HOST_SERVER, params=params)
            respJson = resp.json()

            if resp.is_client_error:
                raise TokenSynthRequest.NoBulletAccessException(respJson["detail"])

            if resp.is_server_error:
                raise TokenSynthRequest.SynthException(respJson["detail"])

            return TokenSynthRequest.PrivilegedTokens(
                GameWeb = respJson["GameWebToken"],
                Bullet = respJson["BulletToken"],
            )
        except TokenSynthRequest.NoBulletAccessException as ex:
            raise ex
        except TokenSynthRequest.SynthException as ex:
            raise ex
        except Exception as ex:
            raise TokenSynthRequest.SynthException(str(ex))


class BuiltGraphQLOperation:
    LatestBattleHistoriesQuery = NintendoRequest.GraphQLOperation(
        Name = "LatestBattleHistoriesQuery",
        Hash = "b24d22fd6cb251c515c2b90044039698aa27bc1fab15801d83014d919cd45780"
    )
        
    LatestVsResults = NintendoRequest.GraphQLOperation(
        Name = "LatestVsResults",
        Hash = "23f3cb83d08f46e36a3eced4bffb538a16cfd6ae21799cc8fb54909fa2962706"
    )

    FriendListQuery = NintendoRequest.GraphQLOperation(
        Name = "FriendListQuery",
        Hash = "ea1297e9bb8e52404f52d89ac821e1d73b726ceef2fd9cc8d6b38ab253428fb3"
    )

    VsHistoryDetailQuery = NintendoRequest.GraphQLOperation(
        Name = "VsHistoryDetailQuery",
        Hash = "94faa2ff992222d11ced55e0f349920a82ac50f414ae33c83d1d1c9d8161c5dd"
    )

    MyOutfitCommonDataEquipmentsQuery = NintendoRequest.GraphQLOperation(
        Name = "myOutfitCommonDataEquipmentsQuery",
        Hash = "45a4c343d973864f7bb9e9efac404182be1d48cf2181619505e9b7cd3b56a6e8"
    )

    ReplayQuery = NintendoRequest.GraphQLOperation(
        Name = "ReplayQuery",
        Hash = "3af48164d1176e8a88fb5321f5fb2daf9dde00b314170f1848a30e1825fc828e"
    )

    DownloadSearchReplayQuery = NintendoRequest.GraphQLOperation(
        Name = "DownloadSearchReplayQuery",
        Hash = "2805ee5182dd44c5114a1e6cfa57b2bcbbe9173c7e52069cc85a518de49c2191"
    )

    PhotoAlbumQuery = NintendoRequest.GraphQLOperation(
        Name = "PhotoAlbumQuery",
        Hash = "903bd1344473a7221867315d0d897ac36604f52064b62a9510a394f2e62fa9c8"
    )