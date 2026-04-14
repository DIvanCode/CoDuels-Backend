from fastapi import FastAPI
from models.predict import PredictRequest, PredictResponse

app = FastAPI()


@app.post("/predict", response_model=PredictResponse)
def predict(request: PredictRequest) -> PredictResponse:
    _ = request
    return PredictResponse(score=0.0)
