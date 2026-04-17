from __future__ import annotations

import argparse

from ml.constants import (
    BASELINE_METRICS_PATH,
    BASELINE_MODEL_PATH,
    PRODUCTION_METRICS_PATH,
    PRODUCTION_MODEL_PATH,
)
from ml.evaluation import format_metrics_for_logs
from ml.training import train_and_save
from ml.trainers import TrainingConfig


def _print_result(name: str, result, model_path: str, metrics_path: str) -> None:
    print(f"==== {name} ====")
    print(f"model_type={result.model_type}")
    print(f"trained_on={result.train_size}")
    print(f"validated_on={result.val_size}")
    print(f"model_path={model_path}")
    print(f"metrics_path={metrics_path}")
    for line in format_metrics_for_logs(f"{name}_train", result.train_metrics):
        print(line)
    for line in format_metrics_for_logs(f"{name}_val", result.val_metrics):
        print(line)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Train baseline and production anti-cheat models."
    )
    parser.add_argument("--data-dir", default="data/train", help="Dataset root with normal/ and cheater/.")
    parser.add_argument("--normal-subdir", default="normal", help="Subdirectory for normal users.")
    parser.add_argument("--cheater-subdir", default="cheater", help="Subdirectory for cheater users.")
    parser.add_argument("--baseline-out", default=str(BASELINE_MODEL_PATH), help="Baseline model output path.")
    parser.add_argument(
        "--baseline-metrics-out",
        default=str(BASELINE_METRICS_PATH),
        help="Baseline metrics output path.",
    )
    parser.add_argument("--prod-out", default=str(PRODUCTION_MODEL_PATH), help="Production model output path.")
    parser.add_argument(
        "--prod-metrics-out",
        default=str(PRODUCTION_METRICS_PATH),
        help="Production metrics output path.",
    )
    args = parser.parse_args()

    baseline_config = TrainingConfig(
        model_type="logistic_regression",
        val_ratio=0.2,
        seed=42,
        iterations=3000,
        l2=1e-3,
    )
    production_config = TrainingConfig(
        model_type="random_forest",
        val_ratio=0.2,
        seed=42,
        random_forest_trees=300,
        random_forest_max_depth=None,
    )

    baseline_result = train_and_save(
        data_dir=args.data_dir,
        config=baseline_config,
        model_out_path=args.baseline_out,
        metrics_out_path=args.baseline_metrics_out,
        normal_subdir=args.normal_subdir,
        cheater_subdir=args.cheater_subdir,
    )
    prod_result = train_and_save(
        data_dir=args.data_dir,
        config=production_config,
        model_out_path=args.prod_out,
        metrics_out_path=args.prod_metrics_out,
        normal_subdir=args.normal_subdir,
        cheater_subdir=args.cheater_subdir,
    )

    _print_result("baseline", baseline_result, args.baseline_out, args.baseline_metrics_out)
    _print_result("production", prod_result, args.prod_out, args.prod_metrics_out)


if __name__ == "__main__":
    main()
