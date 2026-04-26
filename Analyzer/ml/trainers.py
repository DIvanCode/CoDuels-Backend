from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Literal

from sklearn.ensemble import RandomForestClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import Pipeline, make_pipeline
from sklearn.preprocessing import StandardScaler

ModelType = Literal["logistic_regression", "random_forest"]


@dataclass(frozen=True)
class TrainingConfig:
    model_type: ModelType
    val_ratio: float = 0.2
    seed: int = 42
    iterations: int = 3000
    l2: float = 1e-3
    random_forest_trees: int = 300
    random_forest_max_depth: int | None = None


class BaseTrainer(ABC):
    model_type: ModelType

    @abstractmethod
    def build_model(self, config: TrainingConfig) -> Pipeline:
        raise NotImplementedError

    @abstractmethod
    def hyperparams(self, config: TrainingConfig) -> dict[str, object]:
        raise NotImplementedError


class LogisticRegressionTrainer(BaseTrainer):
    model_type: ModelType = "logistic_regression"

    def build_model(self, config: TrainingConfig) -> Pipeline:
        c = 1.0 / config.l2 if config.l2 > 0 else 1e12
        return make_pipeline(
            StandardScaler(),
            LogisticRegression(
                max_iter=config.iterations,
                C=c,
                solver="lbfgs",
                random_state=config.seed,
            ),
        )

    def hyperparams(self, config: TrainingConfig) -> dict[str, object]:
        return {
            "iterations": config.iterations,
            "l2": config.l2,
        }


class RandomForestTrainer(BaseTrainer):
    model_type: ModelType = "random_forest"

    def build_model(self, config: TrainingConfig) -> Pipeline:
        return make_pipeline(
            RandomForestClassifier(
                n_estimators=config.random_forest_trees,
                max_depth=config.random_forest_max_depth,
                random_state=config.seed,
                n_jobs=-1,
            )
        )

    def hyperparams(self, config: TrainingConfig) -> dict[str, object]:
        return {
            "trees": config.random_forest_trees,
            "max_depth": config.random_forest_max_depth,
        }


TRAINERS: dict[ModelType, BaseTrainer] = {
    "logistic_regression": LogisticRegressionTrainer(),
    "random_forest": RandomForestTrainer(),
}
