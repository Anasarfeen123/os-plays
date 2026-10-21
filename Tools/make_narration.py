"""Turns Assets/Audio/Narration/narration.json into one WAV per line with Piper neural TTS.

The JSON is the single source for the narration: this tool speaks it, and the scene builder uses the same text for the
subtitles on the guide screen and the hub console. A line may add "say" when the spoken form should differ from the
subtitle (e.g. "P 2" so the voice reads "P two" while the subtitle shows "P2").

Usage (from the project root):
    python3 Tools/make_narration.py --piper /path/to/piper/piper --model /path/to/en_US-ljspeech-high.onnx [--missing]

Get Piper from https://github.com/rhasspy/piper/releases (piper_linux_x86_64.tar.gz) and the voice from
https://huggingface.co/rhasspy/piper-voices (en/en_US/ljspeech/high). LJ Speech is public domain.
"""
import argparse
import json
import subprocess
import wave
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
FOLDER = ROOT / "Assets" / "Audio" / "Narration"


def polish(path, tail_seconds=0.25):
    """Trim leading silence, add a short tail, normalise to -1 dBFS and fade the edges so clips never click."""
    with wave.open(str(path)) as w:
        rate = w.getframerate()
        x = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float32) / 32768
    start = int(np.argmax(np.abs(x) > 0.01))
    x = x[max(0, start - int(0.03 * rate)):]
    x = np.concatenate([x, np.zeros(int(tail_seconds * rate), np.float32)])
    x *= 0.89 / max(1e-6, np.max(np.abs(x)))
    fade = int(0.01 * rate)
    x[:fade] *= np.linspace(0, 1, fade)
    x[-fade:] *= np.linspace(1, 0, fade)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    return len(x) / rate


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--piper", required=True)
    ap.add_argument("--model", required=True)
    ap.add_argument("--length-scale", default="0.95", help="below 1 speaks faster")
    ap.add_argument("--missing", action="store_true",
                    help="only lines without a WAV yet (Piper's voice varies a little from run to run)")
    args = ap.parse_args()

    lines = json.loads((FOLDER / "narration.json").read_text())["lines"]
    total = 0.0
    for line in lines:
        out = FOLDER / f"{line['id']}.wav"
        if args.missing and out.exists():
            continue
        subprocess.run([args.piper, "--model", args.model, "--length_scale", args.length_scale,
                        "--sentence_silence", "0.25", "--output_file", str(out)],
                       input=line.get("say", line["text"]).encode(), check=True, capture_output=True)
        seconds = polish(out)
        total += seconds
        print(f"{line['id']:12s} {seconds:5.1f}s  {line['title']}")
    print(f"total {total:5.1f}s")


if __name__ == "__main__":
    main()
