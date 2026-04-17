from __future__ import annotations

import pickle
from pathlib import Path
from typing import Any


def save_pickle_model(model: Any, path: str | Path) -> None:
    model_path = Path(path)
    model_path.parent.mkdir(parents=True, exist_ok=True)
    with model_path.open("wb") as f:
        pickle.dump(model, f)


def load_pickle_model(path: str | Path) -> Any:
    with Path(path).open("rb") as f:
        return pickle.load(f)
