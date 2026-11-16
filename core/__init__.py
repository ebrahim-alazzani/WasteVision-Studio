"""Core package for WasteVision Studio architecture."""

from core.config import DATASET_DIR, DATA_DIR, MODEL_PATH, STATIC_DIR, WEB_DIR
from core.dataset_service import DatasetService
from core.cnn_builder import CNNBuilder
from core.evaluator_service import ModelEvaluator
from core.inference_service import InferenceService
from core.trainer_service import TrainingManager, TrainingHyperparameters

__all__ = [
    "DATASET_DIR",
    "DATA_DIR",
    "MODEL_PATH",
    "STATIC_DIR",
    "WEB_DIR",
    "DatasetService",
    "CNNBuilder",
    "ModelEvaluator",
    "InferenceService",
    "TrainingManager",
    "TrainingHyperparameters",
]
