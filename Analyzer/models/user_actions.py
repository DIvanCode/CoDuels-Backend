from datetime import datetime
from enum import Enum
from typing import Annotated, Literal, Union
from uuid import UUID
from pydantic import BaseModel, Field, StringConstraints


class UserActionType(str, Enum):
    ChooseLanguage = "ChooseLanguage"
    WriteCode = "WriteCode"
    DeleteCode = "DeleteCode"
    PasteCode = "PasteCode"
    CutCode = "CutCode"
    MoveCursor = "MoveCursor"
    RunSampleTest = "RunSampleTest"
    RunCustomTest = "RunCustomTest"
    SubmitSolution = "SubmitSolution"


class Language(str, Enum):
    Python = "Python"
    Cpp = "Cpp"
    Golang = "Golang"


TaskKey = Annotated[str, StringConstraints(min_length=1, max_length=1)]


class UserActionBase(BaseModel):
    id: int
    event_id: UUID
    sequence_id: int
    timestamp: datetime
    duel_id: int
    task_key: TaskKey
    user_id: int


class ChooseLanguageUserAction(UserActionBase):
    type: Literal[UserActionType.ChooseLanguage]
    language: Language


class WriteCodeUserAction(UserActionBase):
    type: Literal[UserActionType.WriteCode]
    code_length: int
    cursor_line: int


class DeleteCodeUserAction(UserActionBase):
    type: Literal[UserActionType.DeleteCode]
    code_length: int
    cursor_line: int


class PasteCodeUserAction(UserActionBase):
    type: Literal[UserActionType.PasteCode]
    code_length: int
    cursor_line: int
    begin_line: int
    end_line: int
    chars_count: int


class CutCodeUserAction(UserActionBase):
    type: Literal[UserActionType.CutCode]
    code_length: int
    cursor_line: int
    begin_line: int
    end_line: int
    chars_count: int


class MoveCursorUserAction(UserActionBase):
    type: Literal[UserActionType.MoveCursor]
    code_length: int
    cursor_line: int
    previous_cursor_line: int


class RunSampleTestUserAction(UserActionBase):
    type: Literal[UserActionType.RunSampleTest]


class RunCustomTestUserAction(UserActionBase):
    type: Literal[UserActionType.RunCustomTest]


class SubmitSolutionUserAction(UserActionBase):
    type: Literal[UserActionType.SubmitSolution]


UserAction = Annotated[
    Union[
        ChooseLanguageUserAction,
        WriteCodeUserAction,
        DeleteCodeUserAction,
        PasteCodeUserAction,
        CutCodeUserAction,
        MoveCursorUserAction,
        RunSampleTestUserAction,
        RunCustomTestUserAction,
        SubmitSolutionUserAction,
    ],
    Field(discriminator="type"),
]
