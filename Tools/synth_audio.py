"""Synthesises GRIDLOCK's procedural sounds (engine loop, horn, alarm, ambience, and the hub room's sci-fi
hum, boot, UI blips, glitch, warp and arrival) into Assets/Audio/.

Run from the project root:  python3 Tools/synth_audio.py
Deterministic (fixed seed), so re-running produces identical files.
"""
import wave
from pathlib import Path

import numpy as np

SR = 44100
OUT = Path(__file__).resolve().parent.parent / "Assets" / "Audio"
rng = np.random.default_rng(42)


def t(seconds):
    return np.arange(int(SR * seconds)) / SR


def lowpass(x, cutoff):
    """One-pole low-pass, run twice for a gentler 12 dB/oct slope."""
    a = np.exp(-2 * np.pi * cutoff / SR)
    for _ in range(2):
        y = np.empty_like(x)
        acc = 0.0
        for i, v in enumerate(x):
            acc = (1 - a) * v + a * acc
            y[i] = acc
        x = y
    return x


def seamless(x, fade_seconds):
    """Crossfade the tail into the head so the clip loops without a click."""
    n = int(SR * fade_seconds)
    head, tail = x[:n], x[-n:]
    ramp = np.linspace(0, 1, n)
    x = x[:-n].copy()
    x[:n] = head * ramp + tail * (1 - ramp)
    return x


def normalize(x, peak):
    return x / np.max(np.abs(x)) * peak


def save(name, x, stereo=False):
    x = np.clip(x, -1, 1)
    data = (x * 32767).astype(np.int16)
    if stereo:
        data = np.repeat(data[:, None], 2, axis=1)
    with wave.open(str(OUT / name), "wb") as w:
        w.setnchannels(2 if stereo else 1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print(f"wrote {name} ({len(x) / SR:.2f}s)")


def engine():
    # 2 s loop. Fundamental 50 Hz (100 whole cycles), harmonics shaped like a small petrol engine,
    # amplitude-modulated at the 4-stroke firing rate, plus a little filtered intake noise.
    tt = t(2.0)
    f0 = 50.0
    x = sum((0.9 / k) * np.sin(2 * np.pi * f0 * k * tt + k * 0.7) for k in range(1, 9))
    firing = 0.65 + 0.35 * np.sin(2 * np.pi * f0 / 2 * tt) ** 2
    x = x * firing
    noise = lowpass(rng.standard_normal(len(tt)), 900) * 0.6
    x = np.tanh(1.6 * (x + noise))
    x = lowpass(x, 2200)
    return normalize(seamless(x, 0.15), 0.8)


def horn():
    # Classic two-note car horn (F#4 + A#4), two short honks, slightly overdriven.
    def honk(seconds):
        tt = t(seconds)
        sq = lambda f: np.sign(np.sin(2 * np.pi * f * tt)) * 0.5 + np.sin(2 * np.pi * f * tt) * 0.5
        x = sq(370) + sq(466) * 0.8
        env = np.minimum(1, tt / 0.015) * np.minimum(1, (seconds - tt) / 0.04)
        return lowpass(np.tanh(1.4 * x) * env, 3200)

    gap = np.zeros(int(SR * 0.09))
    return normalize(np.concatenate([honk(0.16), gap, honk(0.36)]), 0.85)


def alarm():
    # Two-tone warning (A5/E5), three cycles, with a soft attack so it is urgent but not harsh in a headset.
    parts = []
    for _ in range(3):
        for f in (880, 659):
            tt = t(0.22)
            x = np.sin(2 * np.pi * f * tt) + 0.3 * np.sin(2 * np.pi * 2 * f * tt)
            env = np.minimum(1, tt / 0.01) * np.minimum(1, (0.22 - tt) / 0.03)
            parts.append(x * env)
    x = np.concatenate(parts)
    tail = np.zeros(int(SR * 0.4))
    x = np.concatenate([x, tail])
    # short slap-back echo for space
    d = int(SR * 0.11)
    echo = np.zeros_like(x)
    echo[d:] = x[:-d] * 0.35
    return normalize(x + echo, 0.8)


def ambience():
    # 16 s city bed: distant traffic rumble (brown-ish noise), slow swells of passing cars, faint wind.
    n = int(SR * 16.4)
    tt = np.arange(n) / SR
    rumble = lowpass(np.cumsum(rng.standard_normal(n)) * 0.02, 180)
    rumble -= lowpass(rumble, 8)  # remove DC drift
    swells = sum(np.exp(-((tt - c) / 1.6) ** 2) for c in rng.uniform(1, 15, 5))
    traffic = lowpass(rng.standard_normal(n), 500) * (0.25 + 0.75 * swells)
    wind = lowpass(rng.standard_normal(n), 1200) * (0.15 + 0.05 * np.sin(2 * np.pi * 0.07 * tt))
    x = normalize(rumble, 1) * 0.6 + normalize(traffic, 1) * 0.45 + normalize(wind, 1) * 0.12
    return normalize(seamless(x, 0.4), 0.6)


# ---------------------------------------------------------------------------------------------------------------
# Hub room (sci-fi control room). These use scipy's IIR filter, which is fast enough for long clips.
# ---------------------------------------------------------------------------------------------------------------

def lp(x, cutoff, order=2):
    from scipy.signal import butter, lfilter
    b, a = butter(order, min(cutoff / (SR / 2), 0.99))
    return lfilter(b, a, x)


def hp(x, cutoff, order=2):
    from scipy.signal import butter, lfilter
    b, a = butter(order, cutoff / (SR / 2), btype="high")
    return lfilter(b, a, x)


def env_ad(tt, attack, decay):
    return np.minimum(1, tt / max(attack, 1e-4)) * np.exp(-np.maximum(0, tt - attack) / decay)


def echo(x, seconds, gain, taps=3):
    d = int(SR * seconds)
    y = np.concatenate([x, np.zeros(d * taps)])
    for k in range(1, taps + 1):
        y[d * k:d * k + len(x)] += x * gain ** k
    return y


def hub_ambience():
    # 20 s loop: a ship's reactor hum (55 Hz with slowly beating harmonics), air handling, and faint data chirps.
    n = int(SR * 20.5)
    tt = np.arange(n) / SR
    hum = sum((0.8 / k) * np.sin(2 * np.pi * 55 * k * tt + 0.3 * np.sin(2 * np.pi * 0.05 * k * tt)) for k in (1, 2, 3, 4, 6))
    hum *= 0.75 + 0.25 * np.sin(2 * np.pi * 0.1 * tt)
    air = lp(rng.standard_normal(n), 700) * (0.6 + 0.4 * np.sin(2 * np.pi * 0.033 * tt + 1))
    chirps = np.zeros(n)
    for c in rng.uniform(0.5, 19.5, 14):
        f = rng.choice([1320, 1760, 2093, 2637])
        seg = t(0.09)
        blip = np.sin(2 * np.pi * f * seg) * env_ad(seg, 0.004, 0.03)
        i = int(c * SR)
        chirps[i:i + len(blip)] += blip[: n - i]
    chirps = echo(chirps, 0.13, 0.4)[:n]
    x = normalize(hum, 1) * 0.55 + normalize(air, 1) * 0.25 + normalize(chirps, 1) * 0.08
    return normalize(seamless(x, 0.5), 0.55)


def hub_boot():
    # Power-on: a low thump, a rising sweep, then a soft major chord that settles.
    tt = t(2.6)
    thump = np.sin(2 * np.pi * (45 + 60 * np.exp(-tt * 18)) * tt) * env_ad(tt, 0.005, 0.25)
    sweep_f = 120 + 1600 * np.clip(tt / 0.9, 0, 1) ** 2
    sweep = np.sin(2 * np.pi * np.cumsum(sweep_f) / SR) * np.clip(tt / 0.9, 0, 1) * (tt < 0.95)
    sweep = lp(sweep, 4000)
    chord = sum(np.sin(2 * np.pi * f * tt) for f in (523.25, 659.25, 783.99, 1046.5))
    chord *= np.where(tt > 0.9, env_ad(tt - 0.9, 0.02, 0.7), 0)
    x = thump * 0.9 + sweep * 0.25 + chord * 0.18
    return normalize(echo(x, 0.17, 0.3), 0.8)


def ui_hover():
    tt = t(0.09)
    f = 1900 + 500 * tt / 0.09
    return normalize(np.sin(2 * np.pi * np.cumsum(f) / SR) * env_ad(tt, 0.003, 0.025), 0.45)


def ui_select():
    # Two ascending tones and a sparkle.
    parts = []
    for f, d in ((880, 0.08), (1318.5, 0.32)):
        tt = t(d)
        parts.append((np.sin(2 * np.pi * f * tt) + 0.35 * np.sin(2 * np.pi * 2 * f * tt)) * env_ad(tt, 0.004, d / 2.5))
    x = np.concatenate(parts)
    tt = np.arange(len(x)) / SR
    sparkle = hp(rng.standard_normal(len(x)), 6000) * env_ad(np.maximum(0, tt - 0.08), 0.01, 0.12) * (tt > 0.08)
    return normalize(echo(x + sparkle * 0.15, 0.09, 0.3), 0.7)


def ui_denied():
    tt = t(0.32)
    x = np.sign(np.sin(2 * np.pi * 110 * tt)) * 0.6 + np.sin(2 * np.pi * 116 * tt)
    x = lp(x, 900) * env_ad(tt, 0.005, 0.12)
    return normalize(x, 0.55)


def key_click():
    tt = t(0.05)
    click = hp(rng.standard_normal(len(tt)), 2500) * env_ad(tt, 0.0005, 0.006)
    body = np.sin(2 * np.pi * 320 * tt) * env_ad(tt, 0.001, 0.012)
    return normalize(click + body * 0.5, 0.5)


def glitch():
    # Digital stutter: bit-crushed noise bursts and square chirps.
    x = np.zeros(int(SR * 0.45))
    pos = 0
    while pos < len(x) - 800:
        n = int(rng.uniform(0.012, 0.05) * SR)
        tt = t(n / SR)
        kind = rng.integers(3)
        if kind == 0:
            seg = np.round(rng.standard_normal(n) * 3) / 3
        elif kind == 1:
            seg = np.sign(np.sin(2 * np.pi * rng.uniform(200, 2000) * tt))
        else:
            seg = np.zeros(n)
        seg = seg[: len(x) - pos]
        x[pos:pos + len(seg)] = seg * rng.uniform(0.3, 1)
        pos += n
    return normalize(lp(x, 6000), 0.5)


def warp():
    # Being pulled into the screen: a noise riser, a pitch climbing an octave and a half, then a bright burst.
    d = 2.2
    tt = t(d)
    k = tt / d
    noise = lp(rng.standard_normal(len(tt)), 800) * k ** 2
    noise += hp(rng.standard_normal(len(tt)), 3000) * k ** 4 * 0.6
    f = 110 * 2 ** (k ** 2 * 3.5)
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * (0.3 + 0.7 * k)
    sub = np.sin(2 * np.pi * np.cumsum(f / 2) / SR) * k
    burst = hp(rng.standard_normal(len(tt)), 1500) * np.where(tt > d - 0.35, env_ad(tt - (d - 0.35), 0.02, 0.15), 0)
    x = normalize(noise, 1) * 0.5 + tone * 0.35 + sub * 0.3 + burst * 0.6
    return normalize(x * np.minimum(1, (d - tt) / 0.03), 0.85)


def arrive():
    # Landing in a scenario: a reverse-swell into a soft low bloom.
    tt = t(1.6)
    swell = lp(rng.standard_normal(len(tt)), 2500) * np.exp(-tt * 4)
    bloom = sum(np.sin(2 * np.pi * f * tt) for f in (130.8, 196, 261.6)) * env_ad(tt, 0.05, 0.6)
    return normalize(swell * 0.5 + bloom * 0.3, 0.6)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    save("engine_loop.wav", engine())
    save("car_horn.wav", horn())
    save("deadlock_alarm.wav", alarm())
    save("city_ambience.wav", ambience(), stereo=True)
    hub = OUT / "Hub"
    hub.mkdir(exist_ok=True)
    for name, fn, stereo in (("hub_ambience", hub_ambience, True), ("hub_boot", hub_boot, False), ("ui_hover", ui_hover, False),
                             ("ui_select", ui_select, False), ("ui_denied", ui_denied, False), ("key_click", key_click, False),
                             ("glitch", glitch, False), ("warp", warp, False), ("arrive", arrive, False)):
        save(f"Hub/{name}.wav", fn(), stereo)
