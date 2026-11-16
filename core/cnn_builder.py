"""CNN architecture definition, layer pipeline, and compilation."""

from __future__ import annotations

from typing import Any
import numpy as np
from sklearn.utils.class_weight import compute_class_weight


class CNNBuilder:
    """Builds and compiles modular Keras CNN models."""

    @staticmethod
    def build(
        tf: Any,
        class_count: int,
        image_size: int = 160,
        filters: int = 32,
        dropout: float = 0.35,
        learning_rate: float = 0.001,
        augmentation: bool = True,
    ) -> Any:
        layers = tf.keras.layers
        model_layers: list[Any] = [tf.keras.Input(shape=(image_size, image_size, 3))]

        if augmentation:
            model_layers.extend([
                layers.RandomFlip("horizontal"),
                layers.RandomRotation(0.05),
                layers.RandomZoom(0.08),
            ])

        model_layers.extend([
            layers.Rescaling(1.0 / 255),
            layers.Conv2D(filters, 3, padding="same", activation="relu"),
            layers.MaxPooling2D(),
            layers.Conv2D(filters * 2, 3, padding="same", activation="relu"),
            layers.MaxPooling2D(),
            layers.Conv2D(filters * 4, 3, padding="same", activation="relu"),
            layers.MaxPooling2D(),
            layers.GlobalAveragePooling2D(),
            layers.Dropout(dropout),
            layers.Dense(64, activation="relu"),
            layers.Dense(class_count, activation="softmax"),
        ])

        model = tf.keras.Sequential(model_layers, name="waste_sorting_cnn")
        model.compile(
            optimizer=tf.keras.optimizers.Adam(learning_rate=learning_rate),
            loss="sparse_categorical_crossentropy",
            metrics=["accuracy"],
        )
        return model

    @staticmethod
    def compute_balanced_weights(class_names: list[str], train_records: list[Any]) -> dict[int, float]:
        label_ids = {lbl: idx for idx, lbl in enumerate(class_names)}
        y = np.asarray([label_ids[lbl] for _, lbl in train_records], dtype=np.int32)
        weights = compute_class_weight(class_weight="balanced", classes=np.arange(len(class_names)), y=y)
        return {idx: float(w) for idx, w in enumerate(weights)}
