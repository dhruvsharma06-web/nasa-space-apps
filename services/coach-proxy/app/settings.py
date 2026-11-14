from pathlib import Path

from pydantic import SecretStr
from pydantic_settings import BaseSettings, SettingsConfigDict

_SERVICE_DIR = Path(__file__).resolve().parent.parent
_DEFAULT_FIXTURES = _SERVICE_DIR.parent.parent / "fixtures"


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=str(_SERVICE_DIR / ".env"),
        env_file_encoding="utf-8",
        extra="ignore",
    )

    GEMINI_API_KEY: SecretStr | None = None
    GEMINI_MODEL: str = ""
    USE_MOCK_AI: bool = True
    COACH_ALLOWED_ORIGIN: str = ""
    FIXTURES_DIR: Path = _DEFAULT_FIXTURES
