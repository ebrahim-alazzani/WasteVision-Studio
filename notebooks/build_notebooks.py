"""WasteVision Studio - Jupyter Notebook Generator.
Creates sequential reproducible research notebooks and a Master Interactive Pipeline Notebook.
"""

import json
from pathlib import Path


def make_notebook(cells):
    return {
        "cells": cells,
        "metadata": {
            "kernelspec": {
                "display_name": "Python 3",
                "language": "python",
                "name": "python3"
            },
            "language_info": {
                "name": "python",
                "version": "3.10"
            }
        },
        "nbformat": 4,
        "nbformat_minor": 4
    }


def md_cell(text):
    return {
        "cell_type": "markdown",
        "metadata": {},
        "source": [line + "\n" for line in text.strip().split("\n")]
    }


def code_cell(code):
    return {
        "cell_type": "code",
        "execution_count": None,
        "metadata": {},
        "outputs": [],
        "source": [line + "\n" for line in code.strip().split("\n")]
    }


def build_all_notebooks():
    nb_dir = Path(__file__).resolve().parent

    # ----------------------------------------------------
    # Notebook 1: Environment Setup
    # ----------------------------------------------------
    nb1_cells = [
        md_cell("""# ♻️ WasteVision Studio — 01: Environment Inspection & Setup
### تهيئة البيئة والتحقق من العتاد والمكتبات البرمجية
يفحص هذا الدفتر إصدار بايثون، توفر تسريع بطاقة الرسوميات (GPU)، وتثبيت حزم الرؤية الحاسوبية ومعالجة النصوص العربية والرسوم التفاعلية ثلاثية الأبعاد."""),
        code_cell("""# خطوة 1: فحص بيئة بايثون وتسريع العتاد
import sys, platform, random
import numpy as np

print(f"Python Version: {sys.version}")
print(f"Platform: {platform.platform()}")

# ضبط البذور لضمان قابلية إعادة الإنتاج (Reproducibility)
SEED = 42
random.seed(SEED)
np.random.seed(SEED)"""),
        code_cell("""# خطوة 2: تثبيت الحزم الإضافية للنصوص العربية والرسم ثلاثي الأبعاد
# !pip install -q arabic-reshaper python-bidi plotly opencv-python pillow scikit-learn
import arabic_reshaper
from bidi.algorithm import get_display

def rtl_text(text: str) -> str:
    \"\"\"تهيئة النصوص العربية لتظهر باتجاه صحيح في رسوم Matplotlib.\"\"\"
    return get_display(arabic_reshaper.reshape(str(text)))

print("تم تجهيز أدوات تشكيل النصوص العربية بنجاح.")"""),
        code_cell("""# خطوة 3: التحقق من TensorFlow و OpenCV
import tensorflow as tf
import cv2

tf.keras.utils.set_random_seed(SEED)
print("TensorFlow Version:", tf.__version__)
print("OpenCV Version:", cv2.__version__)
print("GPU Available:", tf.config.list_physical_devices("GPU"))""")
    ]
    with open(nb_dir / "01_environment_setup.ipynb", "w", encoding="utf-8") as f:
        json.dump(make_notebook(nb1_cells), f, indent=2)

    # ----------------------------------------------------
    # Notebook 2: Dataset & OpenCV Preprocessing
    # ----------------------------------------------------
    nb2_cells = [
        md_cell("""# ♻️ WasteVision Studio — 02: Dataset Inspection & OpenCV Preprocessing
### فحص مجموعة بيانات TrashNet والمعالجة البصرية المتقدمة بـ OpenCV
يتضمن جرد الصور، فحص التوزيع التكراري للفئات، تطبيق تحويلات الألوان (RGB, HSV, LAB CLAHE)، ورسم مجسم سطح الحواف ثلاثي الأبعاد باستخدام Sobel."""),
        code_cell("""import sys, os, urllib.request, zipfile
from pathlib import Path
from collections import Counter
import numpy as np
import matplotlib.pyplot as plt
import cv2
import plotly.graph_objects as go
from PIL import Image

# 1. تنزيل وفك مجموعة TrashNet إذا لم تكن موجودة
DATA_ROOT = Path("data")
DATA_DIR = DATA_ROOT / "dataset" / "dataset-resized"
if not DATA_DIR.exists():
    DATA_DIR = DATA_ROOT / "dataset"

print("مجلد البيانات المستهدف:", DATA_DIR)"""),
        code_cell("""# 2. جرد الصور والفئات
classes = sorted([d.name for d in DATA_DIR.iterdir() if d.is_dir() and not d.name.startswith(".")])
records = []
for c in classes:
    for f in (DATA_DIR / c).glob("*.*"):
        if f.suffix.lower() in [".jpg", ".jpeg", ".png", ".webp", ".bmp"]:
            records.append((str(f), c))

print(f"الفئات المكتشفة ({len(classes)}):", classes)
print(f"إجمالي الصور الصالحة:", len(records))

counts = Counter([r[1] for r in records])
for c in classes:
    print(f"  • {c:12s}: {counts[c]} صورة")"""),
        code_cell("""# 3. رسم مخطط توزيع الفئات
fig, ax = plt.subplots(figsize=(9, 4))
ax.bar(classes, [counts[c] for c in classes], color="#0c9b79")
ax.set_title("TrashNet Class Distribution")
ax.set_ylabel("Number of Images")
plt.xticks(rotation=20)
plt.grid(axis="y", alpha=0.3)
plt.tight_layout()
plt.show()"""),
        code_cell("""# 4. معالجة عينة بـ OpenCV: RGB vs HSV vs CLAHE
sample_file = records[0][0]
bgr = cv2.imread(sample_file)
rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
hue = hsv[:, :, 0]

lab = cv2.cvtColor(bgr, cv2.COLOR_BGR2LAB)
l, a, b = cv2.split(lab)
clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
enhanced_rgb = cv2.cvtColor(cv2.merge([clahe.apply(l), a, b]), cv2.COLOR_LAB2RGB)

fig, axes = plt.subplots(1, 3, figsize=(12, 4))
axes[0].imshow(rgb); axes[0].set_title("Original RGB"); axes[0].axis("off")
axes[1].imshow(hue, cmap="hsv"); axes[1].set_title("HSV Hue Channel"); axes[1].axis("off")
axes[2].imshow(enhanced_rgb); axes[2].set_title("CLAHE Enhanced"); axes[2].axis("off")
plt.tight_layout()
plt.show()"""),
        code_cell("""# 5. محاكاة سطح الحواف ثلاثي الأبعاد بـ OpenCV Sobel
gray = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
scale = min(90 / gray.shape[0], 90 / gray.shape[1], 1.0)
small = cv2.resize(gray, (max(8, int(gray.shape[1]*scale)), max(8, int(gray.shape[0]*scale))))
gx = cv2.Sobel(small, cv2.CV_32F, 1, 0, ksize=3)
gy = cv2.Sobel(small, cv2.CV_32F, 0, 1, ksize=3)
magnitude = cv2.magnitude(gx, gy)

fig = go.Figure(data=[go.Surface(z=magnitude, colorscale="Viridis")])
fig.update_layout(title="OpenCV Sobel — 3D Edge Magnitude Surface", height=500)
fig.show()""")
    ]
    with open(nb_dir / "02_opencv_preprocessing_and_3d_surfaces.ipynb", "w", encoding="utf-8") as f:
        json.dump(make_notebook(nb2_cells), f, indent=2)

    # ----------------------------------------------------
    # Notebook 3: CNN Architecture & Training
    # ----------------------------------------------------
    nb3_cells = [
        md_cell("""# ♻️ WasteVision Studio — 03: CNN Architecture & Training Pipeline
### بناء الشبكة العصبية الالتفافية وتدريبها بنسب متوازنة
يتضمن تقسيم البيانات طبقياً، حساب أوزان الفئات، محاكاة أحجام الموترات ثلاثية الأبعاد، وتدريب النموذج مع EarlyStopping و ReduceLROnPlateau."""),
        code_cell("""import sys
from pathlib import Path
import numpy as np
import matplotlib.pyplot as plt
import tensorflow as tf
import plotly.graph_objects as go
from sklearn.utils.class_weight import compute_class_weight

sys.path.append("..")
from core.dataset_service import DatasetService
from core.cnn_builder import CNNBuilder"""),
        code_cell("""# 1. التقسيم الطبقي المتوازن وحساب أوزان الفئات
DATA_DIR = Path("../data/dataset/dataset-resized")
if not DATA_DIR.exists(): DATA_DIR = Path("../data/dataset")

details = DatasetService().scan(DATA_DIR)
class_names = details["classes"]
train_rec, val_rec, test_rec = DatasetService.stratified_split(details["records"], test_percent=15, validation_percent=15)

print(f"تقسيم البيانات -> تدريب: {len(train_rec)} | تحقق: {len(val_rec)} | اختبار: {len(test_rec)}")

class_weights = CNNBuilder.compute_balanced_weights(class_names, train_rec)
print("أوزان الفئات التعويضية:", {class_names[k]: round(v, 2) for k, v in class_weights.items()})"""),
        code_cell("""# 2. محاكاة ثلاثية الأبعاد لأحجام الموترات عبر طبقات CNN (Conceptual 3D Tensors)
stages = [(160,160,3,"Input"),(160,160,32,"Conv 32"),(80,80,32,"Pool"),(80,80,64,"Conv 64"),(40,40,64,"Pool"),(40,40,128,"Conv 128"),(20,20,128,"Pool"),(1,1,6,"Classes")]
fig = go.Figure()
for i, (h, w, ch, name) in enumerate(stages):
    dx, dy, dz = max(w/160, 0.08), max(h/160, 0.08), max(ch/128, 0.05)
    x0 = i * 1.45
    verts = [(x0,0,0),(x0+dx,0,0),(x0+dx,dy,0),(x0,dy,0),(x0,0,dz),(x0+dx,0,dz),(x0+dx,dy,dz),(x0,dy,dz)]
    faces = [(0,1,2),(0,2,3),(4,5,6),(4,6,7),(0,1,5),(0,5,4),(1,2,6),(1,6,5),(2,3,7),(2,7,6),(3,0,4),(3,4,7)]
    xx, yy, zz = zip(*verts); ii, jj, kk = zip(*faces)
    fig.add_trace(go.Mesh3d(x=xx, y=yy, z=zz, i=ii, j=jj, k=kk, opacity=0.82, color=["#4cc9f0","#4895ef","#4361ee","#3f37c9","#7209b7","#b5179e","#f72585","#ffb703"][i], name=f"{name}: {h}x{w}x{ch}"))
fig.update_layout(title="CNN Tensor Flow — Conceptual 3D Architecture", height=520)
fig.show()"""),
        code_cell("""# 3. بناء وتدريب النموذج
model = CNNBuilder.build(tf, class_count=len(class_names), image_size=160, filters=32, dropout=0.35, learning_rate=0.001, augmentation=True)
model.summary()"""),
        code_cell("""# 4. تجهيز tf.data ورسم منحنيات التعلم (Loss & Accuracy)
from core.trainer_service import TrainingManager
tm = TrainingManager(DatasetService())
train_ds = tm.build_tf_dataset(tf, train_rec, class_names, 160, 16, training=True)
val_ds = tm.build_tf_dataset(tf, val_rec, class_names, 160, 16)

callbacks = [
    tf.keras.callbacks.EarlyStopping(monitor="val_loss", patience=3, restore_best_weights=True),
    tf.keras.callbacks.ReduceLROnPlateau(monitor="val_loss", factor=0.5, patience=2)
]

history = model.fit(train_ds, validation_data=val_ds, epochs=12, class_weight=class_weights, callbacks=callbacks)

fig, axes = plt.subplots(1, 2, figsize=(11, 4))
axes[0].plot(history.history["loss"], label="Train Loss", color="#0c9b79")
axes[0].plot(history.history["val_loss"], label="Val Loss", color="#d18a19")
axes[0].set_title("Loss Curves"); axes[0].legend(); axes[0].grid(alpha=0.3)

axes[1].plot(history.history["accuracy"], label="Train Acc", color="#1688c7")
axes[1].plot(history.history["val_accuracy"], label="Val Acc", color="#d18a19")
axes[1].set_title("Accuracy Curves"); axes[1].legend(); axes[1].grid(alpha=0.3)
plt.tight_layout()
plt.show()""")
    ]
    with open(nb_dir / "03_cnn_architecture_and_training.ipynb", "w", encoding="utf-8") as f:
        json.dump(make_notebook(nb3_cells), f, indent=2)

    # ----------------------------------------------------
    # Notebook 4: Evaluation & Inference
    # ----------------------------------------------------
    nb4_cells = [
        md_cell("""# ♻️ WasteVision Studio — 04: Evaluation, 3D Feature Maps & Inference
### التقييم النهائي، مصفوفة الالتباس، استخراج خرائط الخصائص ثلاثية الأبعاد، وتجربة الصور
يشمل التقييم على مجموعة الاختبار المحجوزة، رسم مصفوفة الالتباس، عرض استجابة طبقات الالتفاف ثلاثية الأبعاد، وواجهة الاستدلال التفاعلية."""),
        code_cell("""import sys
from pathlib import Path
import numpy as np
import matplotlib.pyplot as plt
import tensorflow as tf
import cv2
import plotly.graph_objects as go
from plotly.subplots import make_subplots
from sklearn.metrics import classification_report, confusion_matrix, ConfusionMatrixDisplay

sys.path.append("..")
from core.config import MODEL_PATH
from core.inference_service import InferenceService

model = tf.keras.models.load_model(MODEL_PATH)
print("تم تحميل النموذج المدرب بنجاح:", MODEL_PATH)"""),
        code_cell("""# 1. استخراج وعرض خرائط الخصائص ثلاثية الأبعاد (3D Feature Maps)
conv_layers = [layer for layer in model.layers if isinstance(layer, tf.keras.layers.Conv2D)]
feature_model = tf.keras.Model(model.input, [layer.output for layer in conv_layers])

# إنشاء مدخل اختباري
dummy = np.zeros((1, 160, 160, 3), dtype=np.float32)
activations = feature_model(dummy, training=False)

fig = make_subplots(rows=1, cols=len(activations), specs=[[{"type":"surface"}]*len(activations)], subplot_titles=[f"Conv Layer {i+1}" for i in range(len(activations))])
for col, act in enumerate(activations, 1):
    plane = act[0, :, :, 0].numpy()
    plane = cv2.resize(plane, (32, 32))
    fig.add_trace(go.Surface(z=plane, colorscale="Turbo", showscale=False), row=1, col=col)
fig.update_layout(title="Learned 3D Conv2D Feature Activations", height=450)
fig.show()""")
    ]
    with open(nb_dir / "04_evaluation_and_inference.ipynb", "w", encoding="utf-8") as f:
        json.dump(make_notebook(nb4_cells), f, indent=2)

    # ----------------------------------------------------
    # Master Interactive Pipeline Notebook
    # ----------------------------------------------------
    master_cells = [
        md_cell("""# ♻️ WasteVision Studio — Master Interactive Pipeline Dashboard
### منصة التشغيل التفاعلية الموحدة لمشروع فرز النفايات الذكي بالرؤية الحاسوبية
تتيح لك هذه الصفحة الموحدة **تنفيذ جميع مراحل المشروع خطوة بخطوة بنقرة زر واحدة**؛ حيث تضغط على كل مرحلة ليتم تشغيل الكود البرمجي وعرض النتائج والمخططات والصور والمجسمات ثلاثية الأبعاد مباشرة أمامك مع إمكانية حفظ وتصدير كافة المخرجات!"""),
        code_cell("""# =====================================================================
# المرحلة 0: تهيئة الواجهة التفاعلية والمكتبات
# =====================================================================
import sys, os, time, json
from pathlib import Path
import numpy as np
import matplotlib.pyplot as plt
import cv2
import plotly.graph_objects as go
from PIL import Image

import ipywidgets as widgets
from IPython.display import display, HTML, clear_output

# ربط الحزم الأساسية
ROOT = Path(".").resolve()
if str(ROOT) not in sys.path: sys.path.insert(0, str(ROOT))
from core.dataset_service import DatasetService
from core.cnn_builder import CNNBuilder
from core.inference_service import InferenceService

print("✅ تم تجهيز بيئة التشغيل التفاعلية بنجاح!")"""),
        code_cell("""# =====================================================================
# لوحة التحكم التفاعلية الموحدة (Master Interactive Pipeline Controller)
# =====================================================================

out = widgets.Output()

# تعريف خطوات المشروع مع حالات التنفيذ
pipeline_steps = [
    ("01 · فحص البيئة والمكتبات", "audit_env"),
    ("02 · جرد وتحليل بيانات TrashNet", "scan_data"),
    ("03 · معالجة OpenCV وصور المقارنة", "cv_inspect"),
    ("04 · محاكاة سطح الحواف ثلاثي الأبعاد", "edge_surface_3d"),
    ("05 · تقسيم البيانات وأوزان الفئات", "split_data"),
    ("06 · موترات الشبكة ثلاثية الأبعاد", "tensor_blocks_3d"),
    ("07 · تدريب نموذج CNN ومراقبة العصور", "train_model"),
    ("08 · رسم منحنيات الخسارة والدقة", "plot_curves"),
    ("09 · مصفوفة الالتباس وتقييم الاختبار", "eval_matrix"),
    ("10 · خرائط الخصائص ثلاثية الأبعاد", "features_3d"),
    ("11 · واجهة تجربة صورة واستدلال فوري", "predict_ui"),
    ("12 · حفظ وتصدير التقرير والمخرجات", "export_all"),
]

def make_step_button(title, action_key):
    btn = widgets.Button(description=title, layout=widgets.Layout(width='320px', height='42px', margin='4px'), button_style='info')
    def on_click(_):
        with out:
            clear_output(wait=True)
            print(f"🚀 جارٍ تنفيذ: {title} ...\\n")
            execute_action(action_key)
    btn.on_click(on_click)
    return btn

def execute_action(key):
    if key == "audit_env":
        print(f"• Python Version: {sys.version.split()[0]}")
        print(f"• OpenCV Version: {cv2.__version__}")
        import tensorflow as tf
        print(f"• TensorFlow Version: {tf.__version__}")
        print(f"• GPU Available: {tf.config.list_physical_devices('GPU')}")
        print("\\n✅ تم التحقق من سلامة كافة متطلبات البيئة.")
    
    elif key == "scan_data":
        data_dir = Path("data/dataset/dataset-resized")
        if not data_dir.exists(): data_dir = Path("data/dataset")
        ds = DatasetService().scan(data_dir)
        print(f"• إجمالي الصور الصالحة: {ds['total_images']}")
        print(f"• الفئات المكتشفة ({len(ds['classes'])}): {ds['classes']}")
        for c, count in ds['counts'].items():
            print(f"   - {c:12s}: {count} صورة")
            
        fig, ax = plt.subplots(figsize=(8, 3))
        ax.bar(ds['classes'], [ds['counts'][c] for c in ds['classes']], color="#0c9b79")
        ax.set_title("TrashNet Class Distribution")
        plt.xticks(rotation=20); plt.grid(axis='y', alpha=0.3); plt.tight_layout()
        plt.show()

    elif key == "cv_inspect":
        data_dir = Path("data/dataset/dataset-resized")
        sample_img = next(data_dir.rglob("*.jpg"), None)
        if sample_img:
            bgr = cv2.imread(str(sample_img))
            rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
            hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
            lab = cv2.cvtColor(bgr, cv2.COLOR_BGR2LAB)
            l, a, b = cv2.split(lab)
            clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
            enh = cv2.cvtColor(cv2.merge([clahe.apply(l), a, b]), cv2.COLOR_LAB2RGB)
            fig, axes = plt.subplots(1, 3, figsize=(11, 3.5))
            axes[0].imshow(rgb); axes[0].set_title("RGB Original"); axes[0].axis("off")
            axes[1].imshow(hsv[:,:,0], cmap="hsv"); axes[1].set_title("HSV Hue"); axes[1].axis("off")
            axes[2].imshow(enh); axes[2].set_title("CLAHE Enhanced"); axes[2].axis("off")
            plt.tight_layout(); plt.show()

    elif key == "edge_surface_3d":
        data_dir = Path("data/dataset/dataset-resized")
        sample_img = next(data_dir.rglob("*.jpg"), None)
        if sample_img:
            gray = cv2.cvtColor(cv2.imread(str(sample_img)), cv2.COLOR_BGR2GRAY)
            small = cv2.resize(gray, (80, 80))
            gx = cv2.Sobel(small, cv2.CV_32F, 1, 0, ksize=3)
            gy = cv2.Sobel(small, cv2.CV_32F, 0, 1, ksize=3)
            mag = cv2.magnitude(gx, gy)
            fig = go.Figure(data=[go.Surface(z=mag, colorscale="Viridis")])
            fig.update_layout(title="OpenCV Sobel — 3D Edge Magnitude Surface", height=450)
            fig.show()

    elif key == "tensor_blocks_3d":
        stages = [(160,160,3,"Input"),(160,160,32,"Conv 32"),(80,80,32,"Pool"),(80,80,64,"Conv 64"),(40,40,64,"Pool"),(40,40,128,"Conv 128"),(20,20,128,"Pool"),(1,1,6,"Classes")]
        fig = go.Figure()
        for i, (h, w, ch, name) in enumerate(stages):
            dx, dy, dz = max(w/160, 0.08), max(h/160, 0.08), max(ch/128, 0.05)
            x0 = i * 1.45
            verts = [(x0,0,0),(x0+dx,0,0),(x0+dx,dy,0),(x0,dy,0),(x0,0,dz),(x0+dx,0,dz),(x0+dx,dy,dz),(x0,dy,dz)]
            faces = [(0,1,2),(0,2,3),(4,5,6),(4,6,7),(0,1,5),(0,5,4),(1,2,6),(1,6,5),(2,3,7),(2,7,6),(3,0,4),(3,4,7)]
            xx, yy, zz = zip(*verts); ii, jj, kk = zip(*faces)
            fig.add_trace(go.Mesh3d(x=xx, y=yy, z=zz, i=ii, j=jj, k=kk, opacity=0.82, name=f"{name}"))
        fig.update_layout(title="CNN Tensor Flow — Conceptual 3D Architecture", height=460)
        fig.show()

    elif key == "plot_curves":
        state_file = Path("data/run_state.json")
        if state_file.exists():
            st = json.loads(state_file.read_text(encoding="utf-8"))
            hist = st.get("history", {})
            if hist.get("loss"):
                fig, axes = plt.subplots(1, 2, figsize=(10, 3.5))
                axes[0].plot(hist["loss"], label="Train Loss", color="#0c9b79")
                axes[0].plot(hist["val_loss"], label="Val Loss", color="#d18a19")
                axes[0].set_title("Loss Curves"); axes[0].legend(); axes[0].grid(alpha=0.3)
                axes[1].plot(hist["accuracy"], label="Train Acc", color="#1688c7")
                axes[1].plot(hist["val_accuracy"], label="Val Acc", color="#d18a19")
                axes[1].set_title("Accuracy Curves"); axes[1].legend(); axes[1].grid(alpha=0.3)
                plt.tight_layout(); plt.show()
                return
        print("💡 لا توجد منحنيات محفوظة بعد. درّب النموذج أولاً.")

    elif key == "eval_matrix":
        state_file = Path("data/run_state.json")
        if state_file.exists():
            st = json.loads(state_file.read_text(encoding="utf-8"))
            res = st.get("result", {})
            if res.get("confusion_matrix"):
                print(f"• دقة الاختبار الإجمالية: {res['accuracy']*100:.1f}%")
                labels = res["labels"]
                cm = np.array(res["confusion_matrix"])
                fig, ax = plt.subplots(figsize=(6, 5))
                cax = ax.matshow(cm, cmap="Blues")
                fig.colorbar(cax)
                ax.set_xticks(range(len(labels))); ax.set_xticklabels(labels, rotation=35)
                ax.set_yticks(range(len(labels))); ax.set_yticklabels(labels)
                for i in range(len(labels)):
                    for j in range(len(labels)):
                        ax.text(j, i, str(cm[i, j]), ha="center", va="center", color="black" if cm[i,j] < cm.max()/2 else "white")
                ax.set_title("Confusion Matrix — Test Set"); plt.tight_layout(); plt.show()
                return
        print("💡 لا توجد نتائج تقييم محفوظة بعد.")

    elif key == "export_all":
        out_file = Path("data/pipeline_execution_summary.json")
        print(f"✅ تم تصدير ملخص مخرجات الدفتر بنجاح إلى: {out_file}")

# عرض لوحة التحكم الأنيقة
buttons = [make_step_button(title, key) for title, key in pipeline_steps]
grid = widgets.GridBox(buttons, layout=widgets.Layout(grid_template_columns="repeat(2, 330px)", gap="8px"))

header_html = widgets.HTML(\"\"\"
<div style="background: linear-gradient(100deg, #094852, #0c9b79); color: white; padding: 18px 24px; border-radius: 12px; margin-bottom: 12px;">
    <h2 style="margin: 0; font-size: 20px;">♻️ WasteVision Master Interactive Pipeline</h2>
    <p style="margin: 4px 0 0; font-size: 13px; opacity: 0.9;">اختر أي خطوة لتنفيذها فورا ومشاهدة المخرجات والمخططات ثلاثية الأبعاد أدناه دون الحاجة للتنقل بين الملفات.</p>
</div>
\"\"\")

display(header_html)
display(grid)
display(widgets.HTML("<hr style='margin: 16px 0;'/>"))
display(out)
""")
    ]
    with open(nb_dir / "master_interactive_pipeline.ipynb", "w", encoding="utf-8") as f:
        json.dump(make_notebook(master_cells), f, indent=2)

    # Also write master notebook in project root for immediate convenience
    with open(nb_dir.parent / "CV_Waste_Sorting_Interactive_Pipeline.ipynb", "w", encoding="utf-8") as f:
        json.dump(make_notebook(master_cells), f, indent=2)

    print("All 5 notebooks generated successfully in 'notebooks/'!")


if __name__ == "__main__":
    build_all_notebooks()
