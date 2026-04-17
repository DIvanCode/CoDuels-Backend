from __future__ import annotations

from typing import Any

import numpy as np
from sklearn.metrics import (
    accuracy_score,
    brier_score_loss,
    confusion_matrix,
    f1_score,
    log_loss,
    precision_score,
    recall_score,
    roc_auc_score,
)


def evaluate_binary_classifier(
    y_true: np.ndarray,
    y_proba: np.ndarray,
    *,
    threshold: float = 0.5,
) -> dict[str, Any]:
    y_pred = (y_proba >= threshold).astype(int)

    tn, fp, fn, tp = confusion_matrix(y_true, y_pred, labels=[0.0, 1.0]).ravel()
    fpr = float(fp) / float(fp + tn) if (fp + tn) > 0 else 0.0

    metrics: dict[str, Any] = {
        "log_loss": float(log_loss(y_true, y_proba, labels=[0.0, 1.0])),
        "brier_score": float(brier_score_loss(y_true, y_proba)),
        "accuracy": float(accuracy_score(y_true, y_pred)),
        "precision": float(precision_score(y_true, y_pred, zero_division=0)),
        "recall": float(recall_score(y_true, y_pred, zero_division=0)),
        "f1": float(f1_score(y_true, y_pred, zero_division=0)),
        "false_positive_rate": fpr,
        "threshold": threshold,
        "tp": int(tp),
        "tn": int(tn),
        "fp": int(fp),
        "fn": int(fn),
    }

    try:
        metrics["roc_auc"] = float(roc_auc_score(y_true, y_proba))
    except ValueError:
        metrics["roc_auc"] = None

    return metrics


def format_metrics_for_logs(prefix: str, metrics: dict[str, Any]) -> list[str]:
    ordered_keys = [
        "log_loss",
        "brier_score",
        "roc_auc",
        "accuracy",
        "precision",
        "recall",
        "f1",
        "false_positive_rate",
        "tp",
        "tn",
        "fp",
        "fn",
        "threshold",
    ]

    lines: list[str] = []
    for key in ordered_keys:
        value = metrics.get(key)
        if isinstance(value, float):
            lines.append(f"{prefix}_{key}={value:.6f}")
        else:
            lines.append(f"{prefix}_{key}={value}")
    return lines
