from __future__ import annotations

from contextlib import asynccontextmanager
from pathlib import Path
from time import perf_counter

import pandas as pd
from fastapi import Depends, FastAPI, Request
from prometheus_client import Gauge, Histogram, start_http_server

from api.auth import require_internal_auth
from api.schemas import PredictRequest, PredictResponse
from config import load_config
from features.extractor import extract_features
from features.schema import FEATURE_NAMES
from ml.model_store import load_pickle_model

@asynccontextmanager
async def lifespan(_: FastAPI):
    server, _ = start_http_server(8001, addr="0.0.0.0")
    try:
        yield
    finally:
        server.shutdown()
        server.server_close()


app = FastAPI(lifespan=lifespan)
config = load_config()

http_duration = Histogram(
    "http_server_request_duration_seconds",
    "Duration of application HTTP requests",
    ["http_request_method", "http_route", "http_response_status_code"],
)
model_loaded = Gauge(
    "coduels_analyzer_model_loaded",
    "Loaded Analyzer model role",
    ["role"],
)


@app.middleware("http")
async def observe_http(request: Request, call_next):
    start = perf_counter()
    status = 500
    try:
        response = await call_next(request)
        status = response.status_code
        return response
    finally:
        route = request.scope.get("route")
        route_name = getattr(route, "path", "unmatched")
        method = request.method if request.method in {
            "GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "HEAD"
        } else "OTHER"
        http_duration.labels(method, route_name, str(status)).observe(perf_counter() - start)


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

for role, _ in models:
    model_loaded.labels(role).set(1 if role == loaded_model_role else 0)


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
