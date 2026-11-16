"""WasteVision Studio - Real-time OpenCV Live Camera Sorting Stream.

Captures frames from webcam (cv2.VideoCapture), feeds frames to the trained CNN model,
and displays real-time augmented bounding box, label, confidence, and eco-guidance.
"""

from __future__ import annotations

import os
import sys
import time
from pathlib import Path

# Add project root to sys.path
ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

import cv2
import numpy as np
from PIL import Image

from core.config import MODEL_PATH
from core.inference_service import InferenceService


AR_LABELS = {
    "cardboard": "Cardboard (كرتون)",
    "glass": "Glass (زجاج)",
    "metal": "Metal (معدن)",
    "paper": "Paper (ورق)",
    "plastic": "Plastic (بلاستيك)",
    "trash": "Trash (نفايات عامة)"
}

COLORS = {
    "cardboard": (40, 180, 100),
    "glass": (220, 180, 30),
    "metal": (200, 130, 20),
    "paper": (180, 220, 50),
    "plastic": (20, 160, 255),
    "trash": (60, 60, 220)
}


def run_live_camera(camera_index: int = 0):
    print("=" * 60)
    print(" WasteVision Studio · كاميرا الفرز الذكي المباشر (OpenCV)")
    print("=" * 60)

    if not MODEL_PATH.exists():
        print(f"[!] خطأ: ملف النموذج غير موجود في {MODEL_PATH}")
        print("    يرجى تدريب النموذج أولاً من واجهة الويب أو تطبيق سطح المكتب.")
        return

    print("[*] جارٍ تحميل نموذج TensorFlow المدرب…")
    import tensorflow as tf
    model = tf.keras.models.load_model(MODEL_PATH)
    print("[✓] تم تحميل النموذج بنجاح.")

    # Load classes and run_state
    import json
    run_state_file = ROOT / "data" / "run_state.json"
    class_names = ["cardboard", "glass", "metal", "paper", "plastic", "trash"]
    image_size = 160

    if run_state_file.exists():
        try:
            with open(run_state_file, "r", encoding="utf-8") as f:
                state = json.load(f)
                if "result" in state and "labels" in state["result"]:
                    class_names = state["result"]["labels"]
                if "result" in state and "image_size" in state["result"]:
                    image_size = int(state["result"]["image_size"])
        except Exception:
            pass

    print(f"[*] الفئات المعتمدة: {class_names}")
    print(f"[*] جارٍ فتح الكاميرا (index={camera_index})…")
    cap = cv2.VideoCapture(camera_index, cv2.CAP_DSHOW if sys.platform == "win32" else cv2.CAP_ANY)
    if not cap.isOpened():
        print(f"[!] تعذر فتح الكاميرا ذات المؤشر {camera_index}. تجربة المؤشر التلقائي…")
        cap = cv2.VideoCapture(camera_index)
        if not cap.isOpened():
            print("[!] خطأ: لم يتم العثور على كاميرا متصلة بالجهاز.")
            return

    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)

    print("\n[✓] الكاميرا قيد التشغيل!")
    print("    - ضع النفاية داخل المربع المركزي للتعرف الفوري.")
    print("    - اضغط 'q' أو Esc للخروج.")
    print("    - اضغط 's' لحفظ لقطة.\n")

    prev_time = time.time()
    last_pred_time = 0
    current_pred = "جارٍ الفحص..."
    current_conf = 0.0
    current_label_key = ""

    while True:
        ret, frame = cap.read()
        if not ret:
            print("[!] تعذر التقاط إطار من الكاميرا.")
            break

        h, w, _ = frame.shape
        now = time.time()
        fps = 1.0 / max(1e-4, (now - prev_time))
        prev_time = now

        # Define Center ROI for object classification
        roi_size = min(h, w) // 2
        x1 = (w - roi_size) // 2
        y1 = (h - roi_size) // 2
        x2 = x1 + roi_size
        y2 = y1 + roi_size

        # Run inference every 200ms to maintain smooth 30+ FPS
        if now - last_pred_time > 0.2:
            last_pred_time = now
            roi = frame[y1:y2, x1:x2]
            rgb_roi = cv2.cvtColor(roi, cv2.COLOR_BGR2RGB)
            pil_img = Image.fromarray(rgb_roi)
            result = InferenceService.predict(model, pil_img, class_names, image_size=image_size)
            current_label_key = result["prediction"]
            current_pred = AR_LABELS.get(current_label_key, current_label_key)
            current_conf = result["confidence"]

        # Draw HUD
        box_color = COLORS.get(current_label_key, (0, 229, 163))

        # Semi-transparent dark overlay outside ROI
        overlay = frame.copy()
        cv2.rectangle(overlay, (0, 0), (w, y1), (15, 20, 25), -1)
        cv2.rectangle(overlay, (0, y2), (w, h), (15, 20, 25), -1)
        cv2.rectangle(overlay, (0, y1), (x1, y2), (15, 20, 25), -1)
        cv2.rectangle(overlay, (x2, y1), (w, y2), (15, 20, 25), -1)
        cv2.addWeighted(overlay, 0.45, frame, 0.55, 0, frame)

        # Draw Target Box
        cv2.rectangle(frame, (x1, y1), (x2, y2), box_color, 2)
        corner_len = 24
        # Corner lines for tech HUD look
        cv2.line(frame, (x1, y1), (x1 + corner_len, y1), box_color, 4)
        cv2.line(frame, (x1, y1), (x1, y1 + corner_len), box_color, 4)
        cv2.line(frame, (x2, y1), (x2 - corner_len, y1), box_color, 4)
        cv2.line(frame, (x2, y1), (x2, y1 + corner_len), box_color, 4)
        cv2.line(frame, (x1, y2), (x1 + corner_len, y2), box_color, 4)
        cv2.line(frame, (x1, y2), (x1, y2 - corner_len), box_color, 4)
        cv2.line(frame, (x2, y2), (x2 - corner_len, y2), box_color, 4)
        cv2.line(frame, (x2, y2), (x2, y2 - corner_len), box_color, 4)

        # Header Badge
        cv2.rectangle(frame, (0, 0), (w, 44), (20, 25, 30), -1)
        cv2.putText(frame, "WasteVision Studio - Live AI Waste Sorting", (16, 28),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, (230, 240, 245), 2)
        cv2.putText(frame, f"FPS: {fps:4.1f}", (w - 110, 28),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 229, 163), 2)

        # Prediction Banner
        banner_y = y2 + 16
        if banner_y + 60 < h:
            cv2.rectangle(frame, (x1 - 20, banner_y), (x2 + 20, banner_y + 50), (25, 30, 35), -1)
            cv2.rectangle(frame, (x1 - 20, banner_y), (x2 + 20, banner_y + 50), box_color, 1)
            info_text = f"{current_pred} - {current_conf * 100:.1f}%"
            cv2.putText(frame, info_text, (x1 - 10, banner_y + 32),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.65, box_color, 2)

        cv2.imshow("WasteVision AI - Live Camera Sorting", frame)

        key = cv2.waitKey(1) & 0xFF
        if key in (27, ord('q')):
            break
        elif key == ord('s'):
            snap_path = ROOT / "data" / f"snapshot_{int(time.time())}.jpg"
            cv2.imwrite(str(snap_path), frame)
            print(f"[✓] تم حفظ اللقطة في: {snap_path}")

    cap.release()
    cv2.destroyAllWindows()
    print("[*] تم إغلاق الكاميرا.")


if __name__ == "__main__":
    idx = int(sys.argv[1]) if len(sys.argv) > 1 else 0
    run_live_camera(idx)
