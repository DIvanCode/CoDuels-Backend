from __future__ import annotations

from pathlib import Path

import pandas as pd
from fastapi import Depends, FastAPI

from api.auth import require_internal_auth
from api.schemas import PredictRequest, PredictResponse
from config import load_config
from features.extractor import extract_features
from features.schema import FEATURE_NAMES
from ml.model_store import load_pickle_model

app = FastAPI()
config = load_config()

models: list[tuple[str, Path]] = [
    ("production", config.production_model_path),
    ("baseline", config.baseline_model_path),
]

selected_model_path: Path | None = None
loaded_model_role = ""
for role, path in models:
    if path.exists():
        inference_model = load_pickle_model(path)
        loaded_model_role = role
        selected_model_path = path
        break
else:
    expected_paths = ", ".join(str(path) for _, path in models)
    raise RuntimeError(f"No model file found at startup. Checked: {expected_paths}")


@app.post(
    "/predict",
    response_model=PredictResponse,
    dependencies=[Depends(require_internal_auth(config.internal_auth_key))],
)
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
