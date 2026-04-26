from __future__ import annotations

import json
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import numpy as np
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline

from features.schema import FEATURE_NAMES
from ml.dataset import load_event_dataset
from ml.evaluation import evaluate_binary_classifier
from ml.model_store import save_pickle_model
from ml.trainers import TRAINERS, TrainingConfig


@dataclass(frozen=True)
class TrainingResult:
    model: Pipeline
    model_type: str
    hyperparams: dict[str, object]
    train_metrics: dict[str, Any]
    val_metrics: dict[str, Any]
    train_size: int
    val_size: int


def train_model_from_event_dirs(
    data_dir: str | Path,
    *,
    config: TrainingConfig,
    normal_subdir: str = "normal",
    cheater_subdir: str = "cheater",
) -> TrainingResult:
    x, y = load_event_dataset(
        data_dir,
        normal_subdir=normal_subdir,
        cheater_subdir=cheater_subdir,
    )

    x_train, x_val, y_train, y_val = train_test_split(
        x,
        y,
        test_size=config.val_ratio,
        random_state=config.seed,
        stratify=y,
    )

    trainer = TRAINERS[config.model_type]
    model = trainer.build_model(config)
    model.fit(x_train, y_train)

    train_proba = model.predict_proba(x_train)[:, 1]
    val_proba = model.predict_proba(x_val)[:, 1]

    return TrainingResult(
        model=model,
        model_type=config.model_type,
        hyperparams=trainer.hyperparams(config),
        train_metrics=evaluate_binary_classifier(y_train, train_proba),
        val_metrics=evaluate_binary_classifier(y_val, val_proba),
        train_size=int(len(x_train)),
        val_size=int(len(x_val)),
    )


def save_training_outputs(
    *,
    result: TrainingResult,
    data_path: str | Path,
    config: TrainingConfig,
    model_out_path: str | Path,
    metrics_out_path: str | Path,
) -> None:
    save_pickle_model(result.model, model_out_path)

    payload = {
        "trained_at_utc": datetime.now(timezone.utc).isoformat(),
        "dataset": {
            "path": str(data_path),
            "train_size": result.train_size,
            "val_size": result.val_size,
            "feature_count": len(FEATURE_NAMES),
        },
        "model": {
            "type": result.model_type,
            "hyperparams": result.hyperparams,
        },
        "train_split": {
            "val_ratio": config.val_ratio,
            "seed": config.seed,
        },
        "metrics": {
            "train": result.train_metrics,
            "validation": result.val_metrics,
        },
    }

    metrics_path = Path(metrics_out_path)
    metrics_path.parent.mkdir(parents=True, exist_ok=True)
    metrics_path.write_text(json.dumps(payload, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")


def read_metrics_file(path: str | Path) -> dict[str, Any]:
    return json.loads(Path(path).read_text(encoding="utf-8"))


def train_and_save(
    *,
    data_dir: str | Path,
    config: TrainingConfig,
    model_out_path: str | Path,
    metrics_out_path: str | Path,
    normal_subdir: str = "normal",
    cheater_subdir: str = "cheater",
) -> TrainingResult:
    result = train_model_from_event_dirs(
        data_dir,
        config=config,
        normal_subdir=normal_subdir,
        cheater_subdir=cheater_subdir,
    )
    save_training_outputs(
        result=result,
        data_path=data_dir,
        config=config,
        model_out_path=model_out_path,
        metrics_out_path=metrics_out_path,
    )
    return result
