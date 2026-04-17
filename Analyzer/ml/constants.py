from __future__ import annotations

from pathlib import Path

BASELINE_MODEL_NAME = "baseline_logistic_regression"
PRODUCTION_MODEL_NAME = "production_random_forest"

ARTIFACTS_DIR = Path("artifacts")
BASELINE_MODEL_PATH = ARTIFACTS_DIR / "baseline_logreg.pkl"
BASELINE_METRICS_PATH = ARTIFACTS_DIR / "baseline_logreg.metrics.json"
PRODUCTION_MODEL_PATH = ARTIFACTS_DIR / "prod_random_forest.pkl"
PRODUCTION_METRICS_PATH = ARTIFACTS_DIR / "prod_random_forest.metrics.json"

DEFAULT_DATASET_DIR = Path("data") / "train"
