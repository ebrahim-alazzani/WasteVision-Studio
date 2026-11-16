"""Reusable data, CNN, and evaluation helpers for the waste-sorting project.

Delegates to modular core services while maintaining 100% backward compatibility
with Google Colab notebooks and test scripts.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any, Iterable

from PIL import Image

from core.config import IMAGE_EXTENSIONS
from core.dataset_service import DatasetService
from core.cnn_builder import CNNBuilder
from core.evaluator_service import ModelEvaluator
from core.inference_service import InferenceService
from core.trainer_service import TrainingManager

__all__ = [
    "IMAGE_EXTENSIONS",
    "image_files",
    "find_class_root",
    "scan_dataset",
    "stratified_split",
    "build_tf_dataset",
    "make_class_weights",
    "build_cnn",
    "evaluate_classifier",
    "predict_rgb_image",
]

_default_dataset_service = DatasetService()


def image_files(directory: str | Path) -> list[Path]:
    return DatasetService.image_files(directory)


def find_class_root(directory: str | Path) -> Path:
    return DatasetService.find_class_root(directory)


def scan_dataset(directory: str | Path) -> dict[str, Any]:
    return _default_dataset_service.scan(directory)


def stratified_split(
    records: Iterable[tuple[Path, str]],
    test_percent: int = 15,
    validation_percent: int = 15,
    seed: int = 42,
) -> tuple[list[tuple[Path, str]], list[tuple[Path, str]], list[tuple[Path, str]]]:
    return DatasetService.stratified_split(records, test_percent, validation_percent, seed)


def build_tf_dataset(
    tf: Any,
    records: list[tuple[Path, str]],
    class_names: list[str],
    image_size: int,
    batch_size: int,
    training: bool = False,
    seed: int = 42,
) -> Any:
    return TrainingManager.build_tf_dataset(tf, records, class_names, image_size, batch_size, training, seed)


def make_class_weights(class_names: list[str], records: list[tuple[Path, str]]) -> dict[int, float]:
    return CNNBuilder.compute_balanced_weights(class_names, records)


def build_cnn(
    tf: Any,
    class_count: int,
    image_size: int = 160,
    filters: int = 32,
    dropout: float = 0.35,
    learning_rate: float = 0.001,
    augmentation: bool = True,
) -> Any:
    return CNNBuilder.build(tf, class_count, image_size, filters, dropout, learning_rate, augmentation)


def evaluate_classifier(model: Any, dataset: Any, true_labels: list[str], class_names: list[str]) -> dict[str, Any]:
    return ModelEvaluator.evaluate(model, dataset, true_labels, class_names)


def predict_rgb_image(model: Any, image: Image.Image, class_names: list[str], image_size: int) -> dict[str, Any]:
    return InferenceService.predict(model, image, class_names, image_size)
