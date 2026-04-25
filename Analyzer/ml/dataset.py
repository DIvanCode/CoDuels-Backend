from __future__ import annotations

import json
from pathlib import Path

import numpy as np
import pandas as pd
from pydantic import TypeAdapter

from domain.user_actions import UserAction
from features.extractor import extract_features
from features.schema import FEATURE_NAMES

ACTION_LIST_ADAPTER = TypeAdapter(list[UserAction])


class DatasetError(ValueError):
    pass


def load_event_dataset(
    data_dir: str | Path,
    *,
    normal_subdir: str = "normal",
    cheater_subdir: str = "cheater",
) -> tuple[pd.DataFrame, np.ndarray]:
    base_dir = Path(data_dir)
    normal_dir = base_dir / normal_subdir
    cheater_dir = base_dir / cheater_subdir

    rows: list[dict[str, float]] = []
    labels: list[float] = []

    append_samples_from_dir(rows, labels, normal_dir, label=0.0)
    append_samples_from_dir(rows, labels, cheater_dir, label=1.0)

    if not rows:
        raise DatasetError(f"Dataset is empty in {base_dir}")

    x = pd.DataFrame(rows, columns=list(FEATURE_NAMES))
    y = np.array(labels, dtype=np.float64)

    unique_labels = np.unique(y)
    if unique_labels.size < 2:
        raise DatasetError("Dataset must contain both classes: normal(0) and cheater(1).")

    return x, y


def append_samples_from_dir(
    rows: list[dict[str, float]],
    labels: list[float],
    directory: Path,
    *,
    label: float,
) -> None:
    if not directory.exists():
        raise DatasetError(f"Directory does not exist: {directory}")

    for path in sorted(directory.rglob("*.json")):
        payload = json.loads(path.read_text(encoding="utf-8"))
        actions = ACTION_LIST_ADAPTER.validate_python(payload["actions"])
        user_rating = float(payload["user_rating"])

        features = extract_features(actions, user_rating=user_rating)
        row = {name: float(features[name]) for name in FEATURE_NAMES}
        rows.append(row)
        labels.append(label)
