from dataclasses import dataclass
from Database import KeyValDB

# Main
@dataclass
class AppConfig:
    DevGuild: int

@dataclass
class AppState:
    Config: AppConfig
    DB: KeyValDB
