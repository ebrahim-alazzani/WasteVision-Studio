"""Single image preprocessing and CNN inference."""

from __future__ import annotations

from typing import Any
import numpy as np
from PIL import Image


class InferenceService:
    """Handles image preprocessing and model inference."""

    @staticmethod
    def predict(model: Any, image: Image.Image, class_names: list[str], image_size: int = 160) -> dict[str, Any]:
        rgb = image.convert("RGB").resize((image_size, image_size), Image.Resampling.LANCZOS)
        pixels = np.asarray(rgb, dtype=np.float32)[None, ...]
        scores = model.predict(pixels, verbose=0)[0]
        ranked = sorted(
            ({"label": label, "probability": float(scores[index])}
             for index, label in enumerate(class_names)),
            key=lambda item: item["probability"], reverse=True,
        )
        return {
            "prediction": ranked[0]["label"],
            "confidence": ranked[0]["probability"],
            "probabilities": ranked,
        }
