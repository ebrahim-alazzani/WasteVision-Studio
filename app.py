"""WasteVision Studio - Clean Architecture FastAPI Web Service.

Exposes REST endpoints that delegate to single-responsibility services.
Frictionless local access without hardcoded secrets.
"""

from __future__ import annotations

import io
import os
from pathlib import Path
from typing import Any

from fastapi import Depends, FastAPI, File, HTTPException, Request, UploadFile
from fastapi.responses import FileResponse
from fastapi.security import HTTPBasic, HTTPBasicCredentials
from PIL import Image, UnidentifiedImageError
from pydantic import BaseModel
from starlette.staticfiles import StaticFiles

from core.config import IMAGE_EXTENSIONS, MAX_PREDICT_IMAGE_BYTES, MODEL_PATH, STATIC_DIR, WEB_DIR
from core.dataset_service import DatasetService
from core.inference_service import InferenceService
from core.trainer_service import TrainingHyperparameters, TrainingManager

# Dependency Injection / Service Instantiation
dataset_service = DatasetService()
training_manager = TrainingManager(dataset_service)

security = HTTPBasic(auto_error=False)


def require_login(request: Request, credentials: HTTPBasicCredentials | None = Depends(security)) -> str:
    """Permits local development without login, requires credentials when explicitly set."""
    expected_user = os.getenv("WASTE_WEB_USERNAME", "")
    expected_password = os.getenv("WASTE_WEB_PASSWORD", "")
    if not expected_user or not expected_password:
        client_host = request.client.host if request.client else ""
        if client_host in {"127.0.0.1", "::1", "localhost", "testclient"}:
            return "local"
        raise HTTPException(status_code=503, detail="اضبط WASTE_WEB_USERNAME وWASTE_WEB_PASSWORD قبل إتاحة الواجهة عبر الشبكة.")
    if credentials is None:
        raise HTTPException(status_code=401, detail="سجّل الدخول للمتابعة.", headers={"WWW-Authenticate": "Basic"})
    import secrets
    if not (secrets.compare_digest(credentials.username, expected_user) and secrets.compare_digest(credentials.password, expected_password)):
        raise HTTPException(status_code=401, detail="بيانات الدخول غير صحيحة.", headers={"WWW-Authenticate": "Basic"})
    return credentials.username


app = FastAPI(title="مختبر فرز النفايات بالصور", version="2.0", docs_url=None, redoc_url=None)
app.mount("/assets", StaticFiles(directory=str(STATIC_DIR)), name="waste-assets")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "service": "waste-sorting-web"}


@app.get("/", include_in_schema=False, dependencies=[Depends(require_login)])
def home() -> FileResponse:
    return FileResponse(WEB_DIR / "index.html", media_type="text/html; charset=utf-8")


@app.get("/api/state", dependencies=[Depends(require_login)])
def get_state() -> dict[str, Any]:
    with training_manager.state_lock:
        return {
            "dataset": dataset_service.get_summary(),
            "training": training_manager.get_progress(),
            "result": training_manager.state.get("result"),
            "samples": dataset_service.generate_gallery_thumbnails(training_manager.evaluation_records),
            "model_available": MODEL_PATH.exists(),
        }


@app.post("/api/dataset/upload", dependencies=[Depends(require_login)])
async def upload_dataset(file: UploadFile = File(...)) -> dict[str, Any]:
    if Path(file.filename or "").suffix.lower() != ".zip":
        raise HTTPException(status_code=415, detail="ارفع ملف ZIP يحتوي مجلدًا لكل فئة.")
    payload = await file.read()
    if not training_manager.job_lock.acquire(blocking=False):
        raise HTTPException(status_code=409, detail="لا يمكن استبدال البيانات أثناء التدريب.")
    try:
        summary = dataset_service.unpack_zip_archive(payload)
        training_manager.reset_state()
        return {"dataset": summary, "samples": dataset_service.generate_gallery_thumbnails()}
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    finally:
        if training_manager.job_lock.locked():
            training_manager.job_lock.release()


class ImportFolderRequest(BaseModel):
    folder_path: str


@app.post("/api/dataset/import-folder", dependencies=[Depends(require_login)])
def import_local_folder(req: ImportFolderRequest) -> dict[str, Any]:
    if not training_manager.job_lock.acquire(blocking=False):
        raise HTTPException(status_code=409, detail="لا يمكن استبدال البيانات أثناء التدريب.")
    try:
        summary = dataset_service.import_local_folder(req.folder_path)
        training_manager.reset_state()
        return {"dataset": summary, "samples": dataset_service.generate_gallery_thumbnails()}
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    finally:
        if training_manager.job_lock.locked():
            training_manager.job_lock.release()


@app.post("/api/dataset/refresh", dependencies=[Depends(require_login)])
def refresh_dataset() -> dict[str, Any]:
    dataset_service.invalidate_cache()
    summary = dataset_service.get_summary()
    return {"dataset": summary, "samples": dataset_service.generate_gallery_thumbnails(training_manager.evaluation_records)}


@app.post("/api/train", dependencies=[Depends(require_login)])
def start_training(settings: TrainingHyperparameters) -> dict[str, str]:
    try:
        return training_manager.start_training(settings)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=409, detail=str(exc)) from exc


@app.post("/api/train/reset", dependencies=[Depends(require_login)])
def reset_training() -> dict[str, str]:
    """Allows resetting training lock and state cleanly."""
    if training_manager.job_lock.locked():
        try:
            training_manager.job_lock.release()
        except RuntimeError:
            pass
    with training_manager.state_lock:
        training_manager.state["status"] = "idle"
        training_manager.persist_state()
    return {"status": "ok", "message": "تمت إعادة ضبط حالة التدريب."}



@app.get("/api/progress", dependencies=[Depends(require_login)])
def progress() -> dict[str, Any]:
    return training_manager.get_progress()


@app.post("/api/predict", dependencies=[Depends(require_login)])
async def predict(file: UploadFile = File(...)) -> dict[str, Any]:
    if Path(file.filename or "").suffix.lower() not in IMAGE_EXTENSIONS:
        raise HTTPException(status_code=415, detail="ارفع صورة JPG أو PNG أو WEBP أو BMP.")
    payload = await file.read(MAX_PREDICT_IMAGE_BYTES + 1)
    if not payload or len(payload) > MAX_PREDICT_IMAGE_BYTES:
        raise HTTPException(status_code=413, detail="حجم الصورة يجب ألا يتجاوز 20 ميغابايت.")
    try:
        image = Image.open(io.BytesIO(payload)).convert("RGB")
    except (UnidentifiedImageError, OSError) as exc:
        raise HTTPException(status_code=400, detail="تعذرت قراءة الصورة المرفوعة.") from exc

    if training_manager.trained_model is None and MODEL_PATH.exists():
        tf = training_manager.get_tensorflow()
        training_manager.trained_model = tf.keras.models.load_model(MODEL_PATH)

    result = training_manager.state.get("result")
    if training_manager.trained_model is None or not result:
        raise HTTPException(status_code=409, detail="درّب نموذجًا أولًا ثم ارفع صورة للتوقع.")

    return InferenceService.predict(training_manager.trained_model, image, result["labels"], int(result["image_size"]))


class CameraFrameRequest(BaseModel):
    image_base64: str


@app.post("/api/camera/classify-frame", dependencies=[Depends(require_login)])
def classify_camera_frame(req: CameraFrameRequest) -> dict[str, Any]:
    raw_b64 = req.image_base64
    if "," in raw_b64:
        raw_b64 = raw_b64.split(",", 1)[1]
    import base64
    try:
        data = base64.b64decode(raw_b64)
        image = Image.open(io.BytesIO(data)).convert("RGB")
    except Exception as exc:
        raise HTTPException(status_code=400, detail="إطار الصورة غير صالح.") from exc

    if training_manager.trained_model is None and MODEL_PATH.exists():
        tf = training_manager.get_tensorflow()
        training_manager.trained_model = tf.keras.models.load_model(MODEL_PATH)

    result = training_manager.state.get("result")
    if training_manager.trained_model is None or not result:
        raise HTTPException(status_code=409, detail="درّب نموذجًا أولًا ثم شغّل كاميرا الفرز.")

    return InferenceService.predict(training_manager.trained_model, image, result["labels"], int(result["image_size"]))



@app.get("/api/sample-image/{sample_index}", dependencies=[Depends(require_login)])
def sample_image(sample_index: int) -> FileResponse:
    samples = dataset_service.generate_gallery_thumbnails(training_manager.evaluation_records)
    if sample_index < 0 or sample_index >= len(samples):
        raise HTTPException(status_code=404, detail="الصورة غير موجودة.")
    label = samples[sample_index]["label"]
    cache = dataset_service._cache or dataset_service.scan(dataset_service.dataset_dir)
    chosen = next((p for p, c in (training_manager.evaluation_records or cache["records"]) if c == label), None)
    if chosen is None:
        raise HTTPException(status_code=404, detail="تعذرت قراءة صورة العينة.")
    return FileResponse(chosen)
