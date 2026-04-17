from __future__ import annotations

import os
from pathlib import Path

import pandas as pd
from fastapi import FastAPI

from api.schemas import PredictRequest, PredictResponse
from features.extractor import extract_features
from features.schema import FEATURE_NAMES
from ml.constants import BASELINE_MODEL_PATH, PRODUCTION_MODEL_PATH
from ml.model_store import load_pickle_model

app = FastAPI()

LEGACY_MODEL_ENV = os.getenv("ANALYZER_MODEL_PATH")
PRODUCTION_MODEL_ENV = os.getenv("ANALYZER_PROD_MODEL_PATH")
BASELINE_MODEL_ENV = os.getenv("ANALYZER_BASELINE_MODEL_PATH")

legacy_model_file = Path(LEGACY_MODEL_ENV) if LEGACY_MODEL_ENV else None
production_model_file = Path(PRODUCTION_MODEL_ENV or str(PRODUCTION_MODEL_PATH))
baseline_model_file = Path(BASELINE_MODEL_ENV or str(BASELINE_MODEL_PATH))

candidate_models: list[tuple[str, Path]] = []
if legacy_model_file is not None:
    candidate_models.append(("legacy_override", legacy_model_file))
candidate_models.append(("production", production_model_file))
candidate_models.append(("baseline_fallback", baseline_model_file))

selected_model_path: Path | None = None
loaded_model_role = ""
for role, path in candidate_models:
    if path.exists():
        inference_model = load_pickle_model(path)
        loaded_model_role = role
        selected_model_path = path
        break
else:
    expected_paths = ", ".join(str(path) for _, path in candidate_models)
    raise RuntimeError(f"No model file found at startup. Checked: {expected_paths}")


@app.post("/predict", response_model=PredictResponse)
def predict(request: PredictRequest) -> PredictResponse:
    features = extract_features(
        request.actions,
        user_rating=request.user_rating,
    )
    row = pd.DataFrame([{name: float(features[name]) for name in FEATURE_NAMES}])
    score = float(inference_model.predict_proba(row)[0][1])
    return PredictResponse(score=score)


@app.get("/health")
def health() -> dict[str, str]:
    return {
        "status": "ok",
        "model_role": loaded_model_role,
        "model_path": str(selected_model_path),
    }
