from pydantic import BaseModel
from models.user_actions import UserAction


class PredictRequest(BaseModel):
    actions: list[UserAction]


class PredictResponse(BaseModel):
    score: float
