"""YAMNet 521 점수 기반 gunshot 보강기(binary) 학습 + ONNX export."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Iterable

import librosa
import numpy as np
import onnxruntime as ort
import tensorflow as tf
import tf2onnx
from sklearn.metrics import f1_score, precision_score, recall_score
from sklearn.model_selection import train_test_split

YAMNET_ONNX_PATH = Path(__file__).resolve().parents[2] / "SoundVisualizer" / "AIModel" / "yamnet.onnx"
DEFAULT_EXPORT_ONNX = Path(__file__).resolve().parents[2] / "SoundVisualizer" / "AIModel" / "gunshot_booster.onnx"
SAMPLE_RATE = 16_000
FFT_SIZE = 512
WINDOW_LENGTH = 400
HOP_LENGTH = 160
TIME_FRAMES = 96
MEL_BINS = 64
MEL_FMIN = 125.0
MEL_FMAX = 7500.0
LOG_EPS = 0.001

TARGET_CLASSES_DEFAULT = (
    "Gunshot, gunfire",
    "Machine gun",
    "Fusillade",
    "Artillery fire",
    "Cap gun",
)


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--data-dir", type=Path, default=Path("../../data_preprocessed"))
    p.add_argument("--fallback-data-dir", type=Path, default=Path("../../data"))
    p.add_argument("--epochs", type=int, default=60)
    p.add_argument("--batch-size", type=int, default=8)
    p.add_argument("--val-ratio", type=float, default=0.25)
    p.add_argument("--seed", type=int, default=42)
    p.add_argument(
        "--target-class",
        action="append",
        dest="target_classes",
        default=list(TARGET_CLASSES_DEFAULT),
        help="양성(총성)으로 취급할 클래스명. 여러 번 지정 가능.",
    )
    p.add_argument("--out-dir", type=Path, default=Path("out_gunshot"))
    p.add_argument("--export-onnx", type=Path, default=DEFAULT_EXPORT_ONNX)
    return p.parse_args()


def class_from_filename(name: str) -> str:
    stem = Path(name).stem
    candidates = [idx for idx in (stem.find("_"), stem.find("-")) if idx != -1]
    cut = min(candidates) if candidates else len(stem)
    return stem[:cut].strip()


def compute_logmel_like_runtime(waveform_16k: np.ndarray) -> np.ndarray:
    if waveform_16k.dtype != np.float32:
        waveform_16k = waveform_16k.astype(np.float32)
    if waveform_16k.ndim != 1:
        waveform_16k = waveform_16k.reshape(-1)

    need = WINDOW_LENGTH + HOP_LENGTH * (TIME_FRAMES - 1)
    if waveform_16k.shape[0] >= need:
        x = waveform_16k[-need:]
    else:
        x = np.zeros((need,), dtype=np.float32)
        x[-waveform_16k.shape[0] :] = waveform_16k

    hann = np.hanning(WINDOW_LENGTH).astype(np.float32)
    power = np.zeros((FFT_SIZE // 2 + 1, TIME_FRAMES), dtype=np.float32)
    for t in range(TIME_FRAMES):
        start = t * HOP_LENGTH
        frame = x[start : start + WINDOW_LENGTH] * hann
        fft_in = np.zeros((FFT_SIZE,), dtype=np.float32)
        fft_in[:WINDOW_LENGTH] = frame
        spec = np.fft.rfft(fft_in)
        power[:, t] = (np.abs(spec) ** 2).astype(np.float32)

    mel_fb = librosa.filters.mel(
        sr=SAMPLE_RATE,
        n_fft=FFT_SIZE,
        n_mels=MEL_BINS,
        fmin=MEL_FMIN,
        fmax=MEL_FMAX,
        htk=True,
        norm=None,
    )
    mel = mel_fb @ power
    logmel = np.log(mel + LOG_EPS).T.astype(np.float32)
    return logmel


def run_yamnet_onnx_scores(session: ort.InferenceSession, waveform_16k: np.ndarray) -> np.ndarray:
    logmel = compute_logmel_like_runtime(waveform_16k)
    inp = logmel.reshape(1, 1, TIME_FRAMES, MEL_BINS)
    in_name = session.get_inputs()[0].name
    out_name = session.get_outputs()[0].name
    out = session.run([out_name], {in_name: inp})[0]
    return np.asarray(out).reshape(-1).astype(np.float32)


def iter_labeled_wavs(data_dir: Path, target_classes: set[str]) -> Iterable[tuple[Path, int, str]]:
    for path in sorted(data_dir.glob("*.wav")):
        cls = class_from_filename(path.name)
        y = 1 if cls in target_classes else 0
        yield path, y, cls


def make_model() -> tf.keras.Model:
    model = tf.keras.Sequential(
        [
            tf.keras.layers.Input(shape=(521,), name="yamnet_scores"),
            tf.keras.layers.Dense(128, activation="relu"),
            tf.keras.layers.Dropout(0.2),
            tf.keras.layers.Dense(64, activation="relu"),
            tf.keras.layers.Dense(1, activation="sigmoid", name="gunshot_score"),
        ]
    )
    model.compile(
        optimizer=tf.keras.optimizers.Adam(learning_rate=8e-4),
        loss="binary_crossentropy",
        metrics=[tf.keras.metrics.AUC(name="auc"), tf.keras.metrics.BinaryAccuracy(name="acc")],
    )
    return model


def choose_threshold(y_true: np.ndarray, y_score: np.ndarray) -> tuple[float, float]:
    best_th = 0.5
    best_f1 = -1.0
    for th in np.linspace(0.2, 0.8, 61):
        y_pred = (y_score >= th).astype(np.int64)
        f1 = f1_score(y_true, y_pred, zero_division=0)
        if f1 > best_f1:
            best_f1 = float(f1)
            best_th = float(th)
    return best_th, best_f1


def main() -> int:
    args = parse_args()
    tf.keras.utils.set_random_seed(args.seed)

    target_classes = {s.strip() for s in args.target_classes if s and s.strip()}
    data_dir = args.data_dir.resolve()
    if not data_dir.is_dir():
        data_dir = args.fallback_data_dir.resolve()
    if not data_dir.is_dir():
        raise FileNotFoundError(f"데이터 폴더가 없습니다: {args.data_dir} / {args.fallback_data_dir}")

    if not YAMNET_ONNX_PATH.exists():
        raise FileNotFoundError(f"YAMNet ONNX 파일이 없습니다: {YAMNET_ONNX_PATH}")

    yamnet = ort.InferenceSession(str(YAMNET_ONNX_PATH))

    features: list[np.ndarray] = []
    labels: list[int] = []
    rows: list[dict[str, str | int]] = []

    for wav_path, label, cls in iter_labeled_wavs(data_dir, target_classes):
        y, _ = librosa.load(str(wav_path), sr=SAMPLE_RATE, mono=True, dtype=np.float32)
        score521 = run_yamnet_onnx_scores(yamnet, y)
        features.append(score521)
        labels.append(label)
        rows.append({"file": wav_path.name, "class_prefix": cls, "label": label})

    if len(features) < 12:
        raise RuntimeError(f"학습 샘플이 너무 적습니다: {len(features)}")

    x = np.stack(features, axis=0)
    y = np.array(labels, dtype=np.int64)
    pos = int(np.sum(y == 1))
    neg = int(np.sum(y == 0))
    if pos == 0 or neg == 0:
        raise RuntimeError(f"양/음성 클래스가 모두 필요합니다. positive={pos}, negative={neg}")

    x_train, x_val, y_train, y_val = train_test_split(
        x,
        y,
        test_size=args.val_ratio,
        random_state=args.seed,
        stratify=y,
    )

    class_counts = np.bincount(y_train, minlength=2).astype(np.float32)
    class_weights = (np.sum(class_counts) / (2.0 * np.maximum(class_counts, 1.0))).astype(np.float32)
    class_weight = {0: float(class_weights[0]), 1: float(class_weights[1])}

    model = make_model()
    hist = model.fit(
        x_train,
        y_train,
        validation_data=(x_val, y_val),
        epochs=args.epochs,
        batch_size=args.batch_size,
        verbose=2,
        class_weight=class_weight,
        callbacks=[
            tf.keras.callbacks.EarlyStopping(
                monitor="val_auc", mode="max", patience=8, restore_best_weights=True
            )
        ],
    )

    y_score = model.predict(x_val, verbose=0).reshape(-1)
    best_th, best_f1 = choose_threshold(y_val, y_score)
    y_pred = (y_score >= best_th).astype(np.int64)
    precision = precision_score(y_val, y_pred, zero_division=0)
    recall = recall_score(y_val, y_pred, zero_division=0)
    f1 = f1_score(y_val, y_pred, zero_division=0)

    args.out_dir.mkdir(parents=True, exist_ok=True)
    (args.out_dir / "labels.csv").write_text(
        "file,class_prefix,label\n" + "\n".join(f"{r['file']},{r['class_prefix']},{r['label']}" for r in rows),
        encoding="utf-8",
    )
    model.save(args.out_dir / "gunshot_booster.keras")

    spec = [tf.TensorSpec((None, 521), tf.float32, name="yamnet_scores")]
    args.export_onnx.parent.mkdir(parents=True, exist_ok=True)
    try:
        _onnx_model, _ = tf2onnx.convert.from_keras(
            model,
            input_signature=tuple(spec),
            opset=17,
            output_path=str(args.export_onnx),
        )
    except Exception:
        @tf.function(input_signature=spec)
        def serving(yamnet_scores: tf.Tensor) -> dict[str, tf.Tensor]:
            return {"gunshot_score": model(yamnet_scores)}

        _onnx_model, _ = tf2onnx.convert.from_function(
            serving,
            input_signature=spec,
            opset=17,
            output_path=str(args.export_onnx),
        )

    metrics = {
        "data_dir": str(data_dir),
        "num_samples": int(len(y)),
        "positive": pos,
        "negative": neg,
        "target_classes": sorted(target_classes),
        "class_weight": class_weight,
        "best_threshold": best_th,
        "val_precision": float(precision),
        "val_recall": float(recall),
        "val_f1": float(f1),
        "best_val_f1_sweep": float(best_f1),
        "history": {k: [float(v) for v in vals] for k, vals in hist.history.items()},
        "export_onnx": str(args.export_onnx),
    }
    (args.out_dir / "metrics_gunshot_booster.json").write_text(json.dumps(metrics, indent=2), encoding="utf-8")

    print(f"[data] dir={data_dir} total={len(y)} pos={pos} neg={neg}")
    print(f"[result] threshold={best_th:.3f} precision={precision:.3f} recall={recall:.3f} f1={f1:.3f}")
    print(f"[export] {args.export_onnx}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
