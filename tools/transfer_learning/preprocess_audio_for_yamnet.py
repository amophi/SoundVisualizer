"""YAMNet 정합 최소 전처리: 16k/mono + 약한 fade + DC offset 제거."""

from __future__ import annotations

import argparse
from pathlib import Path

import librosa
import numpy as np
import soundfile as sf


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--input-dir", type=Path, default=Path("../../data"))
    p.add_argument("--output-dir", type=Path, default=Path("../../data_preprocessed"))
    p.add_argument("--sample-rate", type=int, default=16_000)
    p.add_argument("--fade-ms", type=float, default=8.0)
    p.add_argument("--target-peak", type=float, default=0.98)
    p.add_argument("--overwrite", action="store_true")
    return p.parse_args()


def apply_fade(y: np.ndarray, fade_samples: int) -> np.ndarray:
    if fade_samples <= 0 or y.size == 0:
        return y
    n = min(fade_samples, y.size // 2)
    if n <= 0:
        return y
    ramp = np.linspace(0.0, 1.0, n, dtype=np.float32)
    y[:n] *= ramp
    y[-n:] *= ramp[::-1]
    return y


def preprocess_one(path: Path, sample_rate: int, fade_ms: float, target_peak: float) -> np.ndarray:
    y, _ = librosa.load(str(path), sr=sample_rate, mono=True, dtype=np.float32)
    if y.size == 0:
        return y

    # 1) DC offset 제거
    y = y - float(np.mean(y))

    # 2) 약한 fade-in/out
    fade_samples = int(sample_rate * (fade_ms / 1000.0))
    y = apply_fade(y, fade_samples)

    # 3) peak 정규화(과도한 clip 방지)
    peak = float(np.max(np.abs(y))) if y.size else 0.0
    if peak > 1e-8:
        gain = min(1.0, target_peak / peak) if peak > target_peak else (target_peak / peak)
        y = y * np.float32(gain)

    y = np.clip(y, -1.0, 1.0).astype(np.float32)
    return y


def main() -> int:
    args = parse_args()
    input_dir = args.input_dir.resolve()
    output_dir = args.output_dir.resolve()

    if not input_dir.is_dir():
        raise FileNotFoundError(f"입력 폴더가 없습니다: {input_dir}")
    output_dir.mkdir(parents=True, exist_ok=True)

    wavs = sorted(input_dir.glob("*.wav"))
    if not wavs:
        raise RuntimeError(f"입력 폴더에 wav가 없습니다: {input_dir}")

    done = 0
    skipped = 0
    for src in wavs:
        dst = output_dir / src.name
        if dst.exists() and not args.overwrite:
            skipped += 1
            continue
        y = preprocess_one(src, args.sample_rate, args.fade_ms, args.target_peak)
        sf.write(str(dst), y, args.sample_rate, subtype="PCM_16")
        done += 1

    print(f"[done] input={input_dir}")
    print(f"[done] output={output_dir}")
    print(f"[done] files_total={len(wavs)} processed={done} skipped={skipped}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
