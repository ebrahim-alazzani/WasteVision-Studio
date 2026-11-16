"""Model evaluation, test metrics, confusion matrix, and classification reporting."""

from __future__ import annotations

from typing import Any
import numpy as np
from sklearn.metrics import accuracy_score, classification_report, confusion_matrix


class ModelEvaluator:
    """Evaluates classifier on a test dataset and generates structured reports."""

    @staticmethod
    def evaluate(model: Any, dataset: Any, true_labels: list[str], class_names: list[str]) -> dict[str, Any]:
        probabilities = model.predict(dataset, verbose=0)
        predicted_ids = probabilities.argmax(axis=1)
        true_ids = np.asarray([class_names.index(label) for label in true_labels], dtype=np.int32)
        matrix = confusion_matrix(true_ids, predicted_ids, labels=np.arange(len(class_names)))
        report = classification_report(
            true_ids, predicted_ids, labels=np.arange(len(class_names)),
            target_names=class_names, output_dict=True, zero_division=0,
        )
        return {
            "accuracy": float(accuracy_score(true_ids, predicted_ids)),
            "labels": class_names,
            "confusion_matrix": matrix.astype(int).tolist(),
            "per_class": [
                {
                    "label": label,
                    "precision": float(report[label]["precision"]),
                    "recall": float(report[label]["recall"]),
                    "f1": float(report[label]["f1-score"]),
                    "support": int(report[label]["support"]),
                }
                for label in class_names
            ],
        }
