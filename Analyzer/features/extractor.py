from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from statistics import mean, pstdev
from typing import Any

from domain.user_actions import (
    CutCodeUserAction,
    DeleteCodeUserAction,
    MoveCursorUserAction,
    PasteCodeUserAction,
    RunCustomTestUserAction,
    RunSampleTestUserAction,
    SubmitSolutionUserAction,
    UserAction,
    UserActionType,
    WriteCodeUserAction,
)
from features.schema import FEATURE_NAMES


@dataclass(frozen=True)
class FeatureExtractorConfig:
    typing_inactivity_threshold_sec: float = 1.0
    active_interval_inactivity_threshold_sec: float = 15.0
    small_edit_threshold: int = 5
    cursor_jump_threshold_lines: int = 3


@dataclass(frozen=True)
class Interval:
    start: datetime
    end: datetime
    typed_chars: int

    @property
    def duration_sec(self) -> float:
        return max((self.end - self.start).total_seconds(), 0.0)


def extract_features(
    actions: list[UserAction],
    *,
    config: FeatureExtractorConfig = FeatureExtractorConfig(),
    user_rating: float,
) -> dict[str, Any]:
    ordered_actions = sorted(actions, key=lambda a: a.sequence_id)

    typing_intervals = build_typing_intervals(
        ordered_actions,
        inactivity_threshold_sec=config.typing_inactivity_threshold_sec,
    )
    active_intervals = build_typing_intervals(
        ordered_actions,
        inactivity_threshold_sec=config.active_interval_inactivity_threshold_sec,
    )

    interval_speeds = [interval_speed(interval) for interval in typing_intervals]
    active_interval_durations = [interval.duration_sec for interval in active_intervals]

    edit_events = [a for a in ordered_actions if is_edit_event(a)]
    edit_sizes = [edit_size(a) for a in edit_events]

    paste_events = [a for a in ordered_actions if a.type == UserActionType.PasteCode]
    paste_sizes = [paste.chars_count for paste in paste_events]

    typed_chars_total = sum(1 for a in ordered_actions if a.type == UserActionType.WriteCode)
    pasted_chars_total = sum(paste_sizes)
    total_added_chars = typed_chars_total + pasted_chars_total

    features: dict[str, Any] = {
        "typing_speed_avg": avg(interval_speeds),
        "typing_speed_std": std(interval_speeds),
        "active_intervals_count": len(active_intervals),
        "avg_active_interval_duration": avg(active_interval_durations),
        "edits_count": len(edit_events),
        "avg_edit_size": avg(edit_sizes),
        "max_edit_size": max_or_zero(edit_sizes),
        "edit_size_std": std(edit_sizes),
        "small_edit_ratio": safe_ratio(
            sum(1 for size in edit_sizes if size <= config.small_edit_threshold),
            len(edit_sizes),
        ),
        "paste_count": len(paste_events),
        "avg_paste_size": avg(paste_sizes),
        "max_paste_size": max_or_zero(paste_sizes),
        "paste_ratio": safe_ratio(pasted_chars_total, total_added_chars),
        "edits_after_paste_ratio": edits_after_paste_ratio(ordered_actions, paste_events),
        "cursor_jump_count": cursor_jump_count(
            ordered_actions, threshold_lines=config.cursor_jump_threshold_lines
        ),
        "typed_chars_ratio": safe_ratio(typed_chars_total, total_added_chars),
        "typed_chars_total": typed_chars_total,
        "runs_sample_tests_count": sum(
            1 for a in ordered_actions if a.type == UserActionType.RunSampleTest
        ),
        "runs_custom_tests_count": sum(
            1 for a in ordered_actions if a.type == UserActionType.RunCustomTest
        ),
        "submit_count": sum(1 for a in ordered_actions if a.type == UserActionType.SubmitSolution),
        "user_rating": user_rating,
    }
    return features


def extract_feature_vector(
    actions: list[UserAction],
    *,
    config: FeatureExtractorConfig = FeatureExtractorConfig(),
    user_rating: float,
) -> list[Any]:
    features = extract_features(
        actions,
        config=config,
        user_rating=user_rating,
    )
    return [features[name] for name in FEATURE_NAMES]


def build_typing_intervals(
    actions: list[UserAction],
    *,
    inactivity_threshold_sec: float,
) -> list[Interval]:
    intervals: list[Interval] = []

    current_start: datetime | None = None
    current_end: datetime | None = None
    current_typed_chars = 0

    for action in actions:
        if not is_typing_activity_event(action):
            if current_start is not None and current_end is not None:
                intervals.append(
                    Interval(start=current_start, end=current_end, typed_chars=current_typed_chars)
                )
            current_start = None
            current_end = None
            current_typed_chars = 0
            continue

        if current_end is None:
            current_start = action.timestamp
            current_end = action.timestamp
            current_typed_chars = 1 if action.type == UserActionType.WriteCode else 0
            continue

        delta_sec = (action.timestamp - current_end).total_seconds()
        if delta_sec > inactivity_threshold_sec:
            intervals.append(Interval(start=current_start, end=current_end, typed_chars=current_typed_chars))
            current_start = action.timestamp
            current_end = action.timestamp
            current_typed_chars = 1 if action.type == UserActionType.WriteCode else 0
            continue

        current_end = action.timestamp
        if action.type == UserActionType.WriteCode:
            current_typed_chars += 1

    if current_start is not None and current_end is not None:
        intervals.append(Interval(start=current_start, end=current_end, typed_chars=current_typed_chars))

    return intervals


def is_typing_activity_event(action: UserAction) -> bool:
    return action.type in {UserActionType.WriteCode, UserActionType.DeleteCode}


def is_edit_event(action: UserAction) -> bool:
    return action.type in {
        UserActionType.WriteCode,
        UserActionType.DeleteCode,
        UserActionType.PasteCode,
        UserActionType.CutCode,
    }


def edit_size(action: UserAction) -> int:
    if action.type == UserActionType.WriteCode or action.type == UserActionType.DeleteCode:
        return 1
    if action.type == UserActionType.PasteCode or action.type == UserActionType.CutCode:
        return abs(action.chars_count)
    return 0


def interval_speed(interval: Interval) -> float:
    duration = interval.duration_sec
    if duration <= 0:
        return float(interval.typed_chars)
    return interval.typed_chars / duration


def cursor_jump_count(actions: list[UserAction], *, threshold_lines: int) -> int:
    tracked_actions = [a for a in actions if has_cursor_line(a)]
    if len(tracked_actions) < 2:
        return 0

    jumps = 0
    prev_line = cursor_line(tracked_actions[0])
    for action in tracked_actions[1:]:
        line = cursor_line(action)
        if prev_line is not None and line is not None and abs(line - prev_line) > threshold_lines:
            jumps += 1
        prev_line = line
    return jumps


def has_cursor_line(action: UserAction) -> bool:
    return action.type in [UserActionType.WriteCode,
                           UserActionType.DeleteCode,
                           UserActionType.PasteCode,
                           UserActionType.CutCode,
                           UserActionType.MoveCursor]


def cursor_line(action: UserAction) -> int | None:
    if has_cursor_line(action):
        return action.cursor_line
    return None


def edits_after_paste_ratio(
    actions: list[UserAction],
    paste_events: list[PasteCodeUserAction],
) -> float:
    if not paste_events:
        return 0.0

    edited_pastes = 0

    for paste in paste_events:
        start_idx = next((idx for idx, action in enumerate(actions) if action is paste), None)
        if start_idx is None:
            continue

        was_edited = False
        for action in actions[start_idx + 1 :]:
            if not is_edit_event(action):
                continue
            if touches_paste_block(action, paste):
                was_edited = True
                break

        if was_edited:
            edited_pastes += 1

    return safe_ratio(edited_pastes, len(paste_events))


def touches_paste_block(action: UserAction, paste: PasteCodeUserAction) -> bool:
    if action.type == UserActionType.WriteCode or action.type == UserActionType.DeleteCode:
        return paste.begin_line <= action.cursor_line <= paste.end_line

    if action.type == UserActionType.PasteCode or action.type == UserActionType.CutCode:
        return not (action.end_line < paste.begin_line or action.begin_line > paste.end_line)

    return False


def avg(values: list[float] | list[int]) -> float:
    if not values:
        return 0.0
    return float(mean(values))


def std(values: list[float] | list[int]) -> float:
    if len(values) < 2:
        return 0.0
    return float(pstdev(values))


def safe_ratio(numerator: int | float, denominator: int | float) -> float:
    if denominator == 0:
        return 0.0
    return float(numerator) / float(denominator)


def max_or_zero(values: list[int]) -> int:
    if not values:
        return 0
    return max(values)
