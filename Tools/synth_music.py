"""Composes GRIDLOCK's background music: three seamless loops, one per scene, written as OGG into Assets/Audio/Music/.

  music_hub.ogg     "Orbital"     ambient pads, sub bass and bells for the control room   (70 BPM)
  music_lot.ogg     "Rush Hour"   relaxed lo-fi groove for the Deadlock parking lot      (86 BPM)
  music_banker.ogg  "Ledger"      plucked marimba arpeggios for the Banker puzzle        (96 BPM)

Run from the project root:  python3 Tools/synth_music.py      (needs numpy, scipy and ffmpeg)
Deterministic (fixed seeds). Every track is a whole number of bars and its reverb is applied with circular convolution,
so the tail wraps into the start and the loop point is inaudible.
"""
import subprocess
import tempfile
import wave
from pathlib import Path

import numpy as np
from scipy.signal import butter, sosfilt

SR = 44100
OUT = Path(__file__).resolve().parent.parent / "Assets" / "Audio" / "Music"


def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12)


def env_adsr(n, a, d, s, r):
    """Attack/decay/sustain/release envelope over n samples (times in seconds)."""
    e = np.full(n, s, np.float32)
    na, nd, nr = int(a * SR), int(d * SR), int(r * SR)
    na = max(1, min(na, n))
    e[:na] = np.linspace(0, 1, na)
    nd = min(nd, n - na)
    if nd > 0:
        e[na:na + nd] = np.linspace(1, s, nd)
    nr = min(nr, n)
    if nr > 0:
        e[-nr:] *= np.linspace(1, 0, nr)
    return e


def lowpass(x, cutoff, order=2):
    return sosfilt(butter(order, cutoff, "low", fs=SR, output="sos"), x)


def highpass(x, cutoff, order=2):
    return sosfilt(butter(order, cutoff, "high", fs=SR, output="sos"), x)


def place(track, sound, start):
    """Add a sound into the looping track at a sample offset, wrapping past the end."""
    start %= len(track)
    end = start + len(sound)
    if end <= len(track):
        track[start:end] += sound
    else:
        k = len(track) - start
        track[start:] += sound[:k]
        track[:end - len(track)] += sound[k:]


def circular_reverb(x, seconds, mix, seed, damp=6000):
    """Exponentially decaying noise IR, applied by FFT (circular), so the tail wraps around the loop."""
    rng = np.random.default_rng(seed)
    n = int(seconds * SR)
    ir = rng.standard_normal(n) * np.exp(-np.linspace(0, 7, n))
    ir = lowpass(ir, damp)
    ir /= np.sqrt(np.sum(ir ** 2))
    pad = np.zeros(len(x))
    pad[:n] = ir[:len(x)]
    wet = np.real(np.fft.ifft(np.fft.fft(x) * np.fft.fft(pad)))
    return x * (1 - mix) + wet * mix


# ---------------------------------------------------------------------------------------------------------------
# Instruments
# ---------------------------------------------------------------------------------------------------------------

def pad(freqs, seconds, bright=0.5, detune=0.004, seed=0):
    """Soft additive-saw pad: a few detuned voices per note, band-limited to ~12 harmonics."""
    rng = np.random.default_rng(seed)
    t = np.arange(int(seconds * SR)) / SR
    out = np.zeros_like(t)
    for f in freqs:
        for d in (-detune, 0.0, detune):
            ph = rng.uniform(0, 2 * np.pi)
            ff = f * (1 + d)
            for k in range(1, 13):
                if ff * k > 9000:
                    break
                out += np.sin(2 * np.pi * ff * k * t + ph * k) * (bright ** (k - 1)) / k
    return out / (len(freqs) * 3)


def bell(f, seconds=2.5):
    t = np.arange(int(seconds * SR)) / SR
    partials = [(1.0, 1.0), (2.76, 0.45), (5.4, 0.22), (8.93, 0.1)]
    x = sum(a * np.sin(2 * np.pi * f * r * t) * np.exp(-t * (2.2 + r * 0.9)) for r, a in partials)
    return x * env_adsr(len(t), 0.003, 0.0, 1.0, 0.05)


def epiano(freqs, seconds):
    """Rhodes-like keys: sine + bark harmonic with soft decay and tremolo."""
    t = np.arange(int(seconds * SR)) / SR
    out = np.zeros_like(t)
    for f in freqs:
        tone = np.sin(2 * np.pi * f * t) + 0.35 * np.sin(2 * np.pi * 2 * f * t) * np.exp(-t * 3) \
               + 0.12 * np.sin(2 * np.pi * 3 * f * t) * np.exp(-t * 6)
        out += tone * np.exp(-t * 0.9)
    trem = 1 - 0.18 * (0.5 + 0.5 * np.sin(2 * np.pi * 4.5 * t))
    return out * trem * env_adsr(len(t), 0.004, 0.0, 1.0, 0.12) / len(freqs)


def marimba(f, seconds=0.9):
    t = np.arange(int(seconds * SR)) / SR
    x = np.sin(2 * np.pi * f * t) * np.exp(-t * 7) + 0.25 * np.sin(2 * np.pi * 4 * f * t) * np.exp(-t * 18)
    return x * env_adsr(len(t), 0.002, 0.0, 1.0, 0.03)


def bass(f, seconds, sub_only=False):
    t = np.arange(int(seconds * SR)) / SR
    x = np.sin(2 * np.pi * f * t)
    if not sub_only:
        x += 0.25 * np.sin(2 * np.pi * 2 * f * t) + 0.08 * np.sin(2 * np.pi * 3 * f * t)
    return x * env_adsr(len(t), 0.01, 0.15, 0.7, 0.08)


def kick(seconds=0.5):
    t = np.arange(int(seconds * SR)) / SR
    f = 48 + 90 * np.exp(-t * 30)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9)


def snare(rng, seconds=0.35):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    noise = highpass(lowpass(rng.standard_normal(n), 6000), 900)
    body = 0.4 * np.sin(2 * np.pi * 190 * t) * np.exp(-t * 25)
    return (noise * np.exp(-t * 14) * 0.6 + body)


def hat(rng, seconds=0.08):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    return highpass(rng.standard_normal(n), 7000) * np.exp(-t * 60) * 0.35


def chord(root, kind):
    shapes = {"maj7": [0, 4, 7, 11], "m7": [0, 3, 7, 10], "maj9": [0, 4, 7, 11, 14], "m9": [0, 3, 7, 10, 14],
              "6": [0, 4, 7, 9], "sus2": [0, 2, 7, 12], "m": [0, 3, 7, 12], "maj": [0, 4, 7, 12], "7": [0, 4, 7, 10]}
    return [root + i for i in shapes[kind]]


# ---------------------------------------------------------------------------------------------------------------
# Tracks
# ---------------------------------------------------------------------------------------------------------------

def track_hub():
    bpm, bars = 70, 16
    beat = 60 / bpm
    bar = 4 * beat
    n = int(round(bars * bar * SR))
    left, right = np.zeros(n), np.zeros(n)
    prog = [(50, "maj9"), (47, "m9"), (43, "maj7"), (45, "6")]  # D – Bm – G – A
    rng = np.random.default_rng(3)

    for i in range(bars):
        root, kind = prog[(i // 2) % 4] if bars >= 8 else prog[i % 4]
        start = int(i * bar * SR)
        if i % 2 == 0:  # chords last two bars
            p = pad([hz(m) for m in chord(root, kind)], bar * 2 + 1.5, bright=0.42, seed=i)
            p *= env_adsr(len(p), 1.6, 0.0, 1.0, 1.8)
            place(left, p * 0.55, start)
            place(right, p * 0.55, start + 180)
            sub = bass(hz(root - 12), bar * 2, sub_only=True) * env_adsr(int(bar * 2 * SR), 0.8, 0, 1, 0.8)
            place(left, sub * 0.32, start)
            place(right, sub * 0.32, start)
        # sparse bells on a pentatonic over the chord
        scale = [root + 24 + s for s in (0, 2, 4, 7, 9, 12)]
        for b in range(8):
            if rng.random() < 0.45:
                note = bell(hz(scale[rng.integers(len(scale))]))
                pan = rng.uniform(0.2, 0.8)
                at = start + int(b * beat / 2 * SR)
                place(left, note * 0.10 * (1 - pan), at)
                place(right, note * 0.10 * pan, at)

    # slow filter breathing on the pads
    t = np.arange(n) / SR
    sweep = 0.75 + 0.25 * np.sin(2 * np.pi * t / (bars * bar))
    left, right = left * sweep, right * sweep
    left = circular_reverb(left, 4.5, 0.45, 11)
    right = circular_reverb(right, 4.5, 0.45, 12)
    return left, right


def track_lot():
    bpm, bars = 86, 16
    beat = 60 / bpm
    bar = 4 * beat
    n = int(round(bars * bar * SR))
    left, right = np.zeros(n), np.zeros(n)
    prog = [(53, "maj7"), (52, "m7"), (50, "m7"), (48, "maj7")]  # F – Em – Dm – C
    rng = np.random.default_rng(5)
    swing = 0.12 * beat

    for i in range(bars):
        root, kind = prog[i % 4]
        start = int(i * bar * SR)
        # keys: comping hits on 1 and the "and" of 2
        for off in (0.0, 1.5 * beat, 3.0 * beat):
            k = epiano([hz(m) for m in chord(root, kind)], beat * 1.4)
            at = start + int(off * SR)
            place(left, k * 0.22, at)
            place(right, k * 0.22, at + 120)
        # bass: root, fifth, approach
        for j, (step, length) in enumerate([(0, 1.5), (7, 1.0), (0, 1.0), (5 if i % 4 == 3 else 10, 0.5)]):
            pos = sum(l for _, l in [(0, 1.5), (7, 1.0), (0, 1.0), (5, 0.5)][:j])
            b = bass(hz(root - 24 + step), length * beat * 0.95)
            place(left, b * 0.28, start + int(pos * beat * SR))
            place(right, b * 0.28, start + int(pos * beat * SR))
        # drums
        for b in range(4):
            at = start + int(b * beat * SR)
            if b in (0, 2) or (b == 1 and i % 2 == 1):
                kk = kick() * 0.55
                place(left, kk, at + (int(beat / 2 * SR) if b == 1 else 0))
                place(right, kk, at + (int(beat / 2 * SR) if b == 1 else 0))
            if b in (1, 3):
                s = snare(rng) * 0.22
                place(left, s * 0.9, at)
                place(right, s, at)
            for h in range(2):
                hh = hat(rng) * (0.9 if h == 0 else 0.6)
                hat_at = at + int((h * beat / 2 + (swing if h else 0)) * SR)
                place(left, hh * 0.7, hat_at)
                place(right, hh, hat_at)

    # vinyl crackle + gentle tape warmth
    crackle = (rng.random(n) < 0.0008) * rng.standard_normal(n) * 0.08
    left, right = left + lowpass(crackle, 5000), right + lowpass(crackle, 5000)
    left, right = lowpass(left, 9000), lowpass(right, 9000)
    left = circular_reverb(left, 1.6, 0.18, 21)
    right = circular_reverb(right, 1.6, 0.18, 22)
    return left, right


def track_banker():
    bpm, bars = 96, 16
    beat = 60 / bpm
    bar = 4 * beat
    n = int(round(bars * bar * SR))
    left, right = np.zeros(n), np.zeros(n)
    prog = [(57, "m"), (53, "maj"), (48, "maj"), (55, "maj")]  # Am – F – C – G
    rng = np.random.default_rng(9)
    pattern = [0, 2, 1, 3, 2, 1, 3, 2]  # arpeggio order over chord tones (8ths)

    for i in range(bars):
        root, kind = prog[i % 4]
        notes = chord(root, kind)
        start = int(i * bar * SR)
        p = pad([hz(m - 12) for m in notes[:3]], bar + 0.8, bright=0.3, seed=40 + i)
        p *= env_adsr(len(p), 0.5, 0.0, 1.0, 0.8)
        place(left, p * 0.28, start)
        place(right, p * 0.28, start + 200)
        for s in range(8):
            m = notes[pattern[s]] + (12 if (i % 4 == 3 and s >= 6) else 0)
            mk = marimba(hz(m)) * (0.24 if s % 2 == 0 else 0.17)
            at = start + int(s * beat / 2 * SR)
            pan = 0.35 if s % 2 == 0 else 0.65
            place(left, mk * (1 - pan) * 1.4, at)
            place(right, mk * pan * 1.4, at)
        b = bass(hz(root - 24), bar * 0.9)
        place(left, b * 0.24, start)
        place(right, b * 0.24, start)
        if i >= 4:  # light pulse after the intro bars
            for q in range(4):
                at = start + int(q * beat * SR)
                if q in (0, 2):
                    place(left, kick() * 0.35, at)
                    place(right, kick() * 0.35, at)
                hh = hat(rng) * 0.5
                place(left, hh, at + int(beat / 2 * SR))
                place(right, hh, at + int(beat / 2 * SR))

    left = circular_reverb(left, 2.4, 0.3, 31)
    right = circular_reverb(right, 2.4, 0.3, 32)
    return left, right


# ---------------------------------------------------------------------------------------------------------------

def master(left, right, peak=0.7):
    x = np.stack([left, right], 1)
    x = np.tanh(x * 1.2 / max(1e-6, np.max(np.abs(x)))) # gentle glue
    x *= peak / np.max(np.abs(x))
    return x


def write_ogg(name, stereo):
    OUT.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(suffix=".wav") as tmp:
        with wave.open(tmp.name, "wb") as w:
            w.setnchannels(2)
            w.setsampwidth(2)
            w.setframerate(SR)
            w.writeframes((stereo * 32767).astype(np.int16).tobytes())
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp.name, "-c:a", "libvorbis", "-q:a", "5",
                        str(OUT / name)], check=True)
    print(f"wrote {name}  {len(stereo) / SR:5.1f}s")


if __name__ == "__main__":
    write_ogg("music_hub.ogg", master(*track_hub()))
    write_ogg("music_lot.ogg", master(*track_lot()))
    write_ogg("music_banker.ogg", master(*track_banker()))
