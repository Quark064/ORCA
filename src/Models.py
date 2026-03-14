from dataclasses import dataclass
from Database import KeyValDB

# Main
@dataclass
class AppConfig:
    NSAVersion: str
    DevGuild: int
    TokenServiceURL: str

@dataclass
class AppState:
    Config: AppConfig
    DB: KeyValDB
    EmojiTable: dict
