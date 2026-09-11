from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

from ml.constants import BASELINE_MODEL_PATH, PRODUCTION_MODEL_PATH


DEFAULT_INTERNAL_AUTH_KEY = "INTERNAL_AUTH_KEY_LOCAL_VALUE"


@dataclass(frozen=True)
class Config:
    internal_auth_key: str
    production_model_path: Path
    baseline_model_path: Path


def load_config() -> Config:
    return Config(
        internal_auth_key=os.getenv("INTERNAL_AUTH_KEY", DEFAULT_INTERNAL_AUTH_KEY),
        production_model_path=Path(
            os.getenv("ANALYZER_PROD_MODEL_PATH", str(PRODUCTION_MODEL_PATH))
        ),
        baseline_model_path=Path(
            os.getenv("ANALYZER_BASELINE_MODEL_PATH", str(BASELINE_MODEL_PATH))
        ),
    )
