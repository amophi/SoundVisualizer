"""런타임 정합 버전: ONNX YAMNet 점수(521) 기반 3클래스 헤드 학습 + ONNX export."""

from __future__ import annotations

import argparse
import csv
import json
from collections import Counter
from pathlib import Path
from typing import List, Tuple

import librosa
import numpy as np
import onnxruntime as ort
import tensorflow as tf
import tf2onnx
from sklearn.model_selection import train_test_split

from dataset import COARSE_NAMES, iter_waveforms_labeled, list_labeled_wavs

YAMNET_CLASS_MAP_CSV = (
    Path(__file__).resolve().parents[2] / "SoundVisualizer" / "AIModel" / "yamnet_class_map.csv"
)
YAMNET_ONNX_PATH = (
    Path(__file__).resolve().parents[2] / "SoundVisualizer" / "AIModel" / "yamnet.onnx"
)
DEFAULT_EXPORT_ONNX = (
    Path(__file__).resolve().parents[2] / "SoundVisualizer" / "AIModel" / "three_class_score_head.onnx"
)
SAMPLE_RATE = 16_000
FFT_SIZE = 512
WINDOW_LENGTH = 400
HOP_LENGTH = 160
TIME_FRAMES = 96
MEL_BINS = 64
MEL_FMIN = 125.0
MEL_FMAX = 7500.0
LOG_EPS = 0.001


def map_display_name_to_coarse(display_name: str) -> int:
    s = display_name.lower()
    if (
        "footstep" in s
        or "footsteps" in s
        or "gunshot" in s
        or "gunfire" in s
        or "machine gun" in s
        or "artillery" in s
        or "fusillade" in s
        or "cap gun" in s
        or "explosion" in s
        or "fireworks" in s
        or "firecracker" in s
        or "civil defense siren" in s
        or ("police car" in s and "siren" in s)
        or ("ambulance" in s and "siren" in s)
        or "fire engine" in s
        or "fire truck" in s
        or ("siren" in s and "telephone" not in s)
        or "smoke detector" in s
        or "fire alarm" in s
        or s == "alarm"
        or "car alarm" in s
    ):
        return 0
    if (
        "speech" in s
        or "conversation" in s
        or "narration" in s
        or "speaking" in s
        or "babbling" in s
        or "shout" in s
        or "whisper" in s
        or "screaming" in s
        or "laughter" in s
        or "crying" in s
        or "sobbing" in s
        or "singing" in s
        or "choir" in s
        or "rapping" in s
        or "crowd" in s
        or "chatter" in s
        or "hubbub" in s
        or "children playing" in s
    ):
        return 1
    return 2


def load_display_names() -> list[str]:
    with YAMNET_CLASS_MAP_CSV.open("r", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    return [row["display_name"] for row in rows]


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
    mel = mel_fb @ power  # [64, 96]
    logmel = np.log(mel + LOG_EPS).T.astype(np.float32)  # [96,64]
    return logmel


def run_yamnet_onnx_scores(session: ort.InferenceSession, waveform_16k: np.ndarray) -> np.ndarray:
    logmel = compute_logmel_like_runtime(waveform_16k)
    inp = logmel.reshape(1, 1, TIME_FRAMES, MEL_BINS)
    in_name = session.get_inputs()[0].name
    out_name = session.get_outputs()[0].name
    out = session.run([out_name], {in_name: inp})[0]
    return np.asarray(out).reshape(-1).astype(np.float32)  # [521]


def build_dataset(
    yamnet_session: ort.InferenceSession, pairs: List[Tuple[Path, int]], display_names: list[str]
) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    x_scores: list[np.ndarray] = []
    y_human: list[int] = []
    y_teacher: list[np.ndarray] = []

    coarse_index_per_class = np.array(
        [map_display_name_to_coarse(name) for name in display_names], dtype=np.int64
    )

    for waveform, label_idx in iter_waveforms_labeled(pairs):
        scores = run_yamnet_onnx_scores(yamnet_session, waveform)  # [521]
        x_scores.append(scores)
        y_human.append(label_idx)

        coarse_probs = np.zeros(3, dtype=np.float32)
        for c in range(scores.shape[0]):
            coarse_probs[int(coarse_index_per_class[c])] += float(scores[c])
        denom = float(np.sum(coarse_probs))
        if denom <= 0:
            coarse_probs = np.array([1 / 3, 1 / 3, 1 / 3], dtype=np.float32)
        else:
            coarse_probs /= denom
        y_teacher.append(coarse_probs)

    return (
        np.stack(x_scores, axis=0),
        np.array(y_human, dtype=np.int64),
        np.stack(y_teacher, axis=0),
    )


def make_head() -> tf.keras.Model:
    return tf.keras.Sequential(
        [
            tf.keras.layers.Input(shape=(521,), name="yamnet_scores"),
            tf.keras.layers.Dense(128, activation="relu"),
            tf.keras.layers.Dropout(0.15),
            tf.keras.layers.Dense(64, activation="relu"),
            tf.keras.layers.Dense(3, activation="softmax", name="coarse_probs"),
        ]
    )


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--stage1-epochs", type=int, default=20)
    p.add_argument("--stage2-epochs", type=int, default=30)
    p.add_argument("--batch-size", type=int, default=8)
    p.add_argument("--val-ratio", type=float, default=0.2)
    p.add_argument("--seed", type=int, default=42)
    p.add_argument("--export-onnx", type=Path, default=DEFAULT_EXPORT_ONNX)
    p.add_argument("--out-dir", type=Path, default=Path("out_score_head"))
    args = p.parse_args()

    tf.keras.utils.set_random_seed(args.seed)
    pairs = list_labeled_wavs()
    if len(pairs) < 12:
        raise RuntimeError("학습 샘플 수가 너무 적습니다. 최소 12개 이상 권장.")

    print(f"[data] labeled wavs: {len(pairs)}")
    counts = Counter(label for _, label in pairs)
    for idx, name in enumerate(COARSE_NAMES):
        print(f"  - {name}: {counts.get(idx, 0)}")

    if not YAMNET_ONNX_PATH.exists():
        raise FileNotFoundError(f"YAMNet ONNX 파일이 없습니다: {YAMNET_ONNX_PATH}")
    print("[model] loading local YAMNet ONNX (runtime-aligned)...")
    yamnet = ort.InferenceSession(str(YAMNET_ONNX_PATH))
    display_names = load_display_names()
    x, y_human, y_teacher = build_dataset(yamnet, pairs, display_names)
    print(f"[feature] x={x.shape}, y_human={y_human.shape}, y_teacher={y_teacher.shape}")

    teacher_hard = np.argmax(y_teacher, axis=1)
    teacher_agree = float(np.mean(teacher_hard == y_human))
    print(f"[teacher] teacher-human agreement={teacher_agree:.3f}")

    x_train, x_val, y_train, y_val, y_teacher_train, _ = train_test_split(
        x,
        y_human,
        y_teacher,
        test_size=args.val_ratio,
        random_state=args.seed,
        stratify=y_human,
    )

    model = make_head()

    model.compile(
        optimizer=tf.keras.optimizers.Adam(learning_rate=8e-4),
        loss=tf.keras.losses.KLDivergence(),
        metrics=[tf.keras.metrics.CategoricalAccuracy(name="cat_acc")],
    )
    print("[stage1] distill (teacher coarse probs)")
    hist1 = model.fit(
        x_train,
        y_teacher_train,
        validation_split=0.2,
        epochs=args.stage1_epochs,
        batch_size=args.batch_size,
        verbose=2,
        callbacks=[
            tf.keras.callbacks.EarlyStopping(
                monitor="val_cat_acc", mode="max", patience=6, restore_best_weights=True
            )
        ],
    )

    class_counts = np.bincount(y_train, minlength=3).astype(np.float32)
    class_weights = (np.sum(class_counts) / (3.0 * np.maximum(class_counts, 1.0))).astype(np.float32)
    class_weight = {i: float(class_weights[i]) for i in range(3)}

    model.compile(
        optimizer=tf.keras.optimizers.Adam(learning_rate=3e-4),
        loss="sparse_categorical_crossentropy",
        metrics=["accuracy"],
    )
    print(f"[stage2] fine-tune human labels, class_weight={class_weight}")
    hist2 = model.fit(
        x_train,
        y_train,
        validation_data=(x_val, y_val),
        epochs=args.stage2_epochs,
        batch_size=args.batch_size,
        verbose=2,
        class_weight=class_weight,
        callbacks=[
            tf.keras.callbacks.EarlyStopping(
                monitor="val_accuracy", mode="max", patience=8, restore_best_weights=True
            )
        ],
    )

    val_loss, val_acc = model.evaluate(x_val, y_val, verbose=0)
    y_pred = np.argmax(model.predict(x_val, verbose=0), axis=1)
    conf = np.zeros((3, 3), dtype=np.int64)
    for t, p_ in zip(y_val, y_pred):
        conf[int(t), int(p_)] += 1
    print(f"[result] val_loss={val_loss:.4f}, val_acc={val_acc:.4f}")
    print("[result] confusion matrix (rows=true, cols=pred):")
    print(conf)

    args.out_dir.mkdir(parents=True, exist_ok=True)
    keras_path = args.out_dir / "score_head.keras"
    metrics_path = args.out_dir / "metrics_score_head.json"
    model.save(keras_path)

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
            return {"coarse_probs": model(yamnet_scores)}

        _onnx_model, _ = tf2onnx.convert.from_function(
            serving,
            input_signature=spec,
            opset=17,
            output_path=str(args.export_onnx),
        )
    print(f"[export] {args.export_onnx}")

    metrics = {
        "num_samples": int(len(pairs)),
        "teacher_human_agreement": teacher_agree,
        "val_loss": float(val_loss),
        "val_acc": float(val_acc),
        "confusion_matrix": conf.tolist(),
        "class_weight": class_weight,
        "stage1_history": {k: [float(v) for v in vals] for k, vals in hist1.history.items()},
        "stage2_history": {k: [float(v) for v in vals] for k, vals in hist2.history.items()},
        "export_onnx": str(args.export_onnx),
    }
    metrics_path.write_text(json.dumps(metrics, indent=2), encoding="utf-8")
    print(f"[save] {keras_path}")
    print(f"[save] {metrics_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
