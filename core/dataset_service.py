"""Dataset processing, folder scanning, archive unpacking, and split operations."""

from __future__ import annotations

import base64
import io
import os
import shutil
import stat
import tempfile
import zipfile
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any, Iterable

import numpy as np
from PIL import Image, UnidentifiedImageError

from core.config import DATASET_DIR, DATA_DIR, IMAGE_EXTENSIONS, MAX_ARCHIVE_BYTES, MAX_EXTRACTED_BYTES, MAX_IMAGE_FILES


class DatasetService:
    """Encapsulates dataset ingestion, validation, and preparation."""

    def __init__(self, dataset_dir: Path = DATASET_DIR):
        self.dataset_dir = dataset_dir
        self._cache: dict[str, Any] | None = None

    def invalidate_cache(self) -> None:
        self._cache = None

    def get_summary(self) -> dict[str, Any] | None:
        if not self.dataset_dir.exists():
            return None
        if self._cache is None:
            try:
                self._cache = self.scan(self.dataset_dir)
            except (ValueError, OSError):
                return None
        return {k: v for k, v in self._cache.items() if k != "records"}

    @staticmethod
    def image_files(directory: str | Path) -> list[Path]:
        root = Path(directory)
        return sorted(p for p in root.rglob("*") if p.is_file() and p.suffix.lower() in IMAGE_EXTENSIONS)

    @classmethod
    def find_class_root(cls, directory: str | Path) -> Path:
        root = Path(directory).resolve()
        candidates = [root, *(p for p in root.rglob("*") if p.is_dir())]
        ranked: list[tuple[int, int, Path]] = []
        for candidate in candidates:
            class_dirs = [c for c in candidate.iterdir() if c.is_dir()]
            usable = [c for c in class_dirs if any(p.is_file() and p.suffix.lower() in IMAGE_EXTENSIONS for p in c.rglob("*"))]
            if len(usable) >= 2:
                ranked.append((len(usable), -len(candidate.parts), candidate))
        if not ranked:
            raise ValueError("لم أجد مجلدين للفئات على الأقل، وبداخل كل مجلد صور صالحة.")
        return max(ranked, key=lambda item: (item[0], item[1]))[2]

    def scan(self, directory: str | Path) -> dict[str, Any]:
        class_root = self.find_class_root(directory)
        classes = sorted(c.name for c in class_root.iterdir() if c.is_dir())
        counts: dict[str, int] = {}
        records: list[tuple[Path, str]] = []
        unreadable = 0

        for label in classes:
            paths = self.image_files(class_root / label)
            counts[label] = 0
            for path in paths:
                try:
                    with Image.open(path) as img:
                        img.verify()
                    records.append((path, label))
                    counts[label] += 1
                except (UnidentifiedImageError, OSError, ValueError):
                    unreadable += 1

        if len(classes) < 2:
            raise ValueError("يلزم وجود فئتين مصورتين على الأقل.")
        if any(counts[lbl] < 3 for lbl in classes):
            raise ValueError("تحتاج كل فئة إلى ثلاث صور صالحة على الأقل للتقسيم والتقييم.")
        if not records:
            raise ValueError("لم أعثر على صور صالحة داخل مجلدات الفئات.")

        summary = {
            "root": str(class_root),
            "classes": classes,
            "counts": counts,
            "total_images": len(records),
            "unreadable_images": unreadable,
            "records": records,
        }
        self._cache = summary
        return summary

    @staticmethod
    def stratified_split(
        records: Iterable[tuple[Path, str]],
        test_percent: int = 15,
        validation_percent: int = 15,
        seed: int = 42,
    ) -> tuple[list[tuple[Path, str]], list[tuple[Path, str]], list[tuple[Path, str]]]:
        rows = list(records)
        labels = sorted({label for _, label in rows})
        rng = np.random.default_rng(seed)
        train: list[tuple[Path, str]] = []
        val: list[tuple[Path, str]] = []
        test: list[tuple[Path, str]] = []

        for label in labels:
            examples = [r for r in rows if r[1] == label]
            if len(examples) < 3:
                raise ValueError(f"تحتاج الفئة {label} إلى ثلاث صور على الأقل.")
            shuffled = [examples[idx] for idx in rng.permutation(len(examples))]
            test_count = max(1, round(len(shuffled) * test_percent / 100.0))
            val_count = max(1, round(len(shuffled) * validation_percent / 100.0))
            if test_count + val_count >= len(shuffled):
                raise ValueError(f"ارفع صورًا أكثر للفئة {label} أو قلل نسب التحقق والاختبار.")
            test.extend(shuffled[:test_count])
            val.extend(shuffled[test_count:test_count + val_count])
            train.extend(shuffled[test_count + val_count:])

        rng.shuffle(train)
        rng.shuffle(val)
        rng.shuffle(test)
        return train, val, test

    def unpack_zip_archive(self, zip_bytes: bytes) -> dict[str, Any]:
        if len(zip_bytes) > MAX_ARCHIVE_BYTES:
            raise ValueError("حجم ملف ZIP يتجاوز الحد المسموح (100 ميغابايت).")

        stage = Path(tempfile.mkdtemp(prefix="waste_stage_", dir=DATA_DIR))
        try:
            with zipfile.ZipFile(io.BytesIO(zip_bytes)) as archive:
                files = [item for item in archive.infolist() if not item.is_dir()]
                if len(files) > MAX_IMAGE_FILES * 3:
                    raise ValueError("عدد الملفات في ZIP يتجاوز الحد المسموح.")
                if sum(item.file_size for item in files) > MAX_EXTRACTED_BYTES:
                    raise ValueError("حجم الملفات بعد فك الضغط يتجاوز 500 ميغابايت.")

                image_count = 0
                for item in files:
                    norm = item.filename.replace("\\", "/")
                    parts = PurePosixPath(norm).parts
                    win_p = PureWindowsPath(item.filename)
                    if not parts or ".." in parts or PurePosixPath(norm).is_absolute() or win_p.is_absolute() or win_p.drive:
                        raise ValueError("يحتوي ZIP على مسار غير آمن.")
                    if stat.S_ISLNK((item.external_attr >> 16) & 0xFFFF):
                        raise ValueError("الروابط الرمزية غير مسموحة داخل ZIP.")
                    if Path(parts[-1]).suffix.lower() not in IMAGE_EXTENSIONS:
                        continue

                    image_count += 1
                    target = stage.joinpath(*parts)
                    target.parent.mkdir(parents=True, exist_ok=True)
                    with archive.open(item) as src, target.open("wb") as dst:
                        shutil.copyfileobj(src, dst, length=1024 * 1024)

                    try:
                        with Image.open(target) as img:
                            if img.width * img.height > 50_000_000:
                                target.unlink(missing_ok=True)
                                continue
                            img.verify()
                    except (UnidentifiedImageError, OSError, Image.DecompressionBombError):
                        target.unlink(missing_ok=True)

                if image_count > MAX_IMAGE_FILES:
                    raise ValueError(f"تجاوز عدد الصور الحد الأقصى ({MAX_IMAGE_FILES}).")

            details = self.scan(stage)
            if len(details["records"]) < 6:
                raise ValueError("لم أعثر على عدد كافٍ من الصور الصالحة داخل ZIP.")

            self._swap_dataset_directory(stage)
            return self.get_summary() or {}
        finally:
            if stage.exists():
                shutil.rmtree(stage, ignore_errors=True)

    def import_local_folder(self, folder_path: str | Path) -> dict[str, Any]:
        source = Path(folder_path).resolve()
        if not source.exists() or not source.is_dir():
            raise ValueError("المجلد المحدد غير موجود.")
        class_root = self.find_class_root(source)
        details = self.scan(class_root)
        if len(details["records"]) < 6:
            raise ValueError("لم يتم العثور على صور صالحة كافية في المجلد.")

        stage = Path(tempfile.mkdtemp(prefix="waste_import_", dir=DATA_DIR))
        try:
            shutil.copytree(class_root, stage / "dataset")
            self._swap_dataset_directory(stage / "dataset")
            return self.get_summary() or {}
        finally:
            if stage.exists():
                shutil.rmtree(stage, ignore_errors=True)

    def _swap_dataset_directory(self, new_dir: Path) -> None:
        prev = DATA_DIR / "dataset_previous"
        if prev.exists():
            shutil.rmtree(prev)
        if self.dataset_dir.exists():
            self.dataset_dir.rename(prev)
        try:
            new_dir.rename(self.dataset_dir)
        except Exception:
            if prev.exists() and not self.dataset_dir.exists():
                prev.rename(self.dataset_dir)
            raise
        if prev.exists():
            shutil.rmtree(prev)
        self.invalidate_cache()

    def generate_gallery_thumbnails(self, evaluation_records: list[tuple[Path, str]] | None = None) -> list[dict[str, Any]]:
        summary = self.get_summary()
        if not summary:
            return []
        records = evaluation_records or (self._cache.get("records") if self._cache else [])
        preferred: dict[str, Path] = {}
        for path, label in records:
            if label not in preferred:
                preferred[label] = path

        samples: list[dict[str, Any]] = []
        for label in summary["classes"]:
            sample = preferred.get(label)
            if not sample and self._cache:
                sample = next((p for p, name in self._cache["records"] if name == label), None)
            if not sample:
                continue
            try:
                with Image.open(sample) as img:
                    img.thumbnail((240, 180), Image.Resampling.LANCZOS)
                    buf = io.BytesIO()
                    img.convert("RGB").save(buf, format="JPEG", quality=72, optimize=True)
                samples.append({
                    "label": label,
                    "data_url": "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode("ascii"),
                    "is_test_sample": bool(evaluation_records),
                })
            except (OSError, UnidentifiedImageError):
                continue
        return samples
