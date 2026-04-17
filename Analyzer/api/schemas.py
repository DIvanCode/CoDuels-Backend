from pydantic import BaseModel
from domain.user_actions import UserAction


class PredictRequest(BaseModel):
    actions: list[UserAction]
    user_rating: float


class PredictResponse(BaseModel):
    score: float
