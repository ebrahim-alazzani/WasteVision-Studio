"""Configuration, paths, and limits for the WasteVision system."""

from __future__ import annotations

import os
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parent.parent
WEB_DIR = ROOT_DIR / "web"
STATIC_DIR = WEB_DIR / "static"
DATA_DIR = Path(os.getenv("WASTE_DATA_DIR", str(ROOT_DIR / "data"))).resolve()
DATA_DIR.mkdir(parents=True, exist_ok=True)

DATASET_DIR = DATA_DIR / "dataset"
MODEL_PATH = DATA_DIR / "waste_sorting_cnn.keras"
MODEL_TMP_PATH = DATA_DIR / "waste_sorting_cnn.pending.keras"
STATE_FILE_PATH = DATA_DIR / "run_state.json"

MAX_ARCHIVE_BYTES = 100 * 1024 * 1024       # 100 MB
MAX_EXTRACTED_BYTES = 500 * 1024 * 1024     # 500 MB
MAX_IMAGE_FILES = 15_000
MAX_PREDICT_IMAGE_BYTES = 20 * 1024 * 1024   # 20 MB

IMAGE_EXTENSIONS = {".jpg", ".jpeg", ".png", ".webp", ".bmp"}
