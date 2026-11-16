"""Background training orchestrator, state management, and callbacks."""

from __future__ import annotations

import json
import os
import threading
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image
from pydantic import BaseModel, Field

from core.config import DATASET_DIR, MODEL_PATH, MODEL_TMP_PATH, STATE_FILE_PATH
from core.dataset_service import DatasetService
from core.cnn_builder import CNNBuilder
from core.evaluator_service import ModelEvaluator


class TrainingHyperparameters(BaseModel):
    epochs: int = Field(default=12, ge=1, le=100)
    image_size: int = Field(default=160, ge=64, le=224)
    batch_size: int = Field(default=16, ge=4, le=64)
    test_percent: int = Field(default=15, ge=10, le=30)
    validation_percent: int = Field(default=15, ge=10, le=30)
    filters: int = Field(default=32, ge=16, le=64)
    dropout: float = Field(default=0.35, ge=0.0, le=0.7)
    learning_rate: float = Field(default=0.001, gt=0.00001, le=0.01)
    augmentation: bool = True


class TrainingManager:
    """Manages thread-safe model training lifecycle and state persistence."""

    def __init__(self, dataset_service: DatasetService):
        self.dataset_service = dataset_service
        self.state_lock = threading.RLock()
        self.job_lock = threading.Lock()
        self.worker: threading.Thread | None = None
        self.trained_model: Any = None
        self.evaluation_records: list[tuple[Path, str]] = []
        self._tf: Any = None

        saved = self._read_saved_state()
        self.state: dict[str, Any] = {
            "status": saved.get("status", "idle"),
            "current_epoch": saved.get("current_epoch", 0),
            "total_epochs": saved.get("total_epochs", 0),
            "history": saved.get("history") or {"loss": [], "val_loss": [], "accuracy": [], "val_accuracy": []},
            "error": saved.get("error"),
            "result": saved.get("result"),
            "started_at": saved.get("started_at"),
            "updated_at": saved.get("updated_at"),
        }

    def get_tensorflow(self) -> Any:
        if self._tf is None:
            import tensorflow as tf
            self._tf = tf
        return self._tf

    def _read_saved_state(self) -> dict[str, Any]:
        if not STATE_FILE_PATH.exists():
            return {}
        try:
            return json.loads(STATE_FILE_PATH.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            return {}

    def persist_state(self) -> None:
        with self.state_lock:
            payload = {k: v for k, v in self.state.items() if k != "internal_error"}
            temp = STATE_FILE_PATH.with_suffix(".pending.json")
            temp.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
            os.replace(temp, STATE_FILE_PATH)

    def get_progress(self) -> dict[str, Any]:
        with self.state_lock:
            total = self.state.get("total_epochs")
            res = self.state.get("result") or {}
            if not total and res.get("epochs_completed"):
                total = res["epochs_completed"]
            curr = self.state.get("current_epoch") or total or 0
            return {
                "status": self.state.get("status"),
                "current_epoch": curr,
                "total_epochs": total or curr,
                "history": self.state.get("history", {}),
                "error": self.state.get("error"),
                "updated_at": self.state.get("updated_at"),
            }

    def reset_state(self) -> None:
        with self.state_lock:
            self.state.update(
                status="idle",
                current_epoch=0,
                total_epochs=0,
                history={"loss": [], "val_loss": [], "accuracy": [], "val_accuracy": []},
                error=None,
                result=None,
                started_at=None,
                updated_at=None,
            )
            self.trained_model = None
            self.evaluation_records = []
            MODEL_PATH.unlink(missing_ok=True)
            MODEL_TMP_PATH.unlink(missing_ok=True)
            self.persist_state()

    def start_training(self, params: TrainingHyperparameters) -> dict[str, str]:
        if not DATASET_DIR.exists():
            raise ValueError("ارفع ملف مجموعة الصور أولًا.")
        if params.test_percent + params.validation_percent > 50:
            raise ValueError("مجموع نسب التحقق والاختبار يجب ألا يتجاوز 50%.")
        if not self.job_lock.acquire(blocking=False):
            raise RuntimeError("هناك تدريب جارٍ بالفعل.")

        with self.state_lock:
            self.state.update(
                status="running",
                current_epoch=0,
                total_epochs=params.epochs,
                history={"loss": [], "val_loss": [], "accuracy": [], "val_accuracy": []},
                result=None,
                error=None,
                started_at=datetime.now(timezone.utc).isoformat(),
                updated_at=datetime.now(timezone.utc).isoformat(),
            )
            self.persist_state()

        self.worker = threading.Thread(target=self._run_training_job, args=(params,), daemon=True)
        self.worker.start()
        return {"status": "started", "message": "بدأ التدريب في الخلفية."}

    def _run_training_job(self, params: TrainingHyperparameters) -> None:
        try:
            tf = self.get_tensorflow()
            details = self.dataset_service.scan(DATASET_DIR)
            classes = details["classes"]

            train_rec, val_rec, test_rec = DatasetService.stratified_split(
                details["records"], params.test_percent, params.validation_percent
            )

            train_ds = self.build_tf_dataset(tf, train_rec, classes, params.image_size, params.batch_size, training=True)
            val_ds = self.build_tf_dataset(tf, val_rec, classes, params.image_size, params.batch_size)
            test_ds = self.build_tf_dataset(tf, test_rec, classes, params.image_size, params.batch_size)

            model = CNNBuilder.build(
                tf,
                class_count=len(classes),
                image_size=params.image_size,
                filters=params.filters,
                dropout=params.dropout,
                learning_rate=params.learning_rate,
                augmentation=params.augmentation,
            )

            weights = CNNBuilder.compute_balanced_weights(classes, train_rec)

            mgr = self

            class ProgressCallback(tf.keras.callbacks.Callback):
                def on_epoch_end(self, epoch, logs=None):
                    logs = logs or {}
                    with mgr.state_lock:
                        mgr.state["current_epoch"] = epoch + 1
                        mgr.state["updated_at"] = datetime.now(timezone.utc).isoformat()
                        for key in ("loss", "val_loss", "accuracy", "val_accuracy"):
                            if key in logs:
                                mgr.state["history"].setdefault(key, []).append(float(logs[key]))
                    mgr.persist_state()

            callbacks = [
                ProgressCallback(),
                tf.keras.callbacks.EarlyStopping(monitor="val_loss", patience=3, restore_best_weights=True),
                tf.keras.callbacks.ReduceLROnPlateau(monitor="val_loss", factor=0.5, patience=2, min_lr=1e-6),
            ]

            history = model.fit(
                train_ds,
                validation_data=val_ds,
                epochs=params.epochs,
                class_weight=weights,
                callbacks=callbacks,
                verbose=0,
            )

            test_labels = [lbl for _, lbl in test_rec]
            eval_report = ModelEvaluator.evaluate(model, test_ds, test_labels, classes)

            model.save(MODEL_TMP_PATH)
            os.replace(MODEL_TMP_PATH, MODEL_PATH)

            result = {
                **eval_report,
                "architecture": "CNN · Conv2D + MaxPooling + GlobalAveragePooling + Dense",
                "epochs_completed": len(history.history.get("loss", [])),
                "train_images": len(train_rec),
                "validation_images": len(val_rec),
                "test_images": len(test_rec),
                "image_size": params.image_size,
                "trained_at": datetime.now(timezone.utc).isoformat(),
                "settings": params.model_dump(),
            }

            with self.state_lock:
                self.trained_model = model
                self.evaluation_records = test_rec
                self.state.update(
                    status="completed",
                    result=result,
                    error=None,
                    updated_at=datetime.now(timezone.utc).isoformat(),
                )
                self.persist_state()

        except Exception as exc:
            with self.state_lock:
                self.state.update(
                    status="failed",
                    error=f"{type(exc).__name__}: {exc}",
                    updated_at=datetime.now(timezone.utc).isoformat(),
                )
                self.persist_state()
        finally:
            if self.job_lock.locked():
                self.job_lock.release()

    @staticmethod
    def build_tf_dataset(
        tf: Any,
        records: list[tuple[Path, str]],
        class_names: list[str],
        image_size: int,
        batch_size: int,
        training: bool = False,
        seed: int = 42,
    ) -> Any:
        label_ids = {lbl: idx for idx, lbl in enumerate(class_names)}
        paths = np.asarray([str(p) for p, _ in records])
        labels = np.asarray([label_ids[lbl] for _, lbl in records], dtype=np.int32)
        dataset = tf.data.Dataset.from_tensor_slices((paths, labels))
        if training:
            dataset = dataset.shuffle(len(records), seed=seed, reshuffle_each_iteration=True)

        def decode(path, label):
            def read_rgb(raw_path):
                filename = raw_path.decode("utf-8")
                with Image.open(filename) as src:
                    rgb = src.convert("RGB").resize((image_size, image_size), Image.Resampling.BILINEAR)
                    return np.asarray(rgb, dtype=np.float32)

            image = tf.numpy_function(read_rgb, [path], Tout=tf.float32)
            image.set_shape([image_size, image_size, 3])
            return image, label

        opts = tf.data.Options()
        opts.experimental_deterministic = not training
        return (
            dataset.with_options(opts)
            .map(decode, num_parallel_calls=tf.data.AUTOTUNE)
            .batch(batch_size)
            .prefetch(tf.data.AUTOTUNE)
        )
