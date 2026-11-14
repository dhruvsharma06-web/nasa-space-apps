"""Synthesises ASTRA Academy UI cues (self-made, CC0). Usage: python3 make_audio.py <out_dir>"""
import math, os, random, struct, sys, uuid, wave

SR = 44100
OUT = sys.argv[1]

def env(n, attack=0.005, release=0.05, total=None):
    a = max(1, int(attack * SR)); r = max(1, int(release * SR))
    out = []
    for i in range(n):
        g = 1.0
        if i < a: g = i / a
        if i > n - r: g = min(g, max(0.0, (n - i) / r))
        out.append(g)
    return out

def tone(freqs, dur, shape="sine", decay=None, attack=0.004, release=0.04):
    n = int(dur * SR); e = env(n, attack, release); s = []
    for i in range(n):
        t = i / SR
        f = freqs(t) if callable(freqs) else freqs
        v = 0.0
        for k, fk in enumerate(f if isinstance(f, (list, tuple)) else [f]):
            ph = 2 * math.pi * fk * t
            if shape == "sine": w = math.sin(ph)
            elif shape == "soft_square": w = math.tanh(3 * math.sin(ph)) / math.tanh(3)
            else: w = math.sin(ph)
            v += w / (k + 1)
        if decay: v *= math.exp(-t / decay)
        s.append(v * e[i])
    return s

def sweep(f0, f1, dur, **kw):
    # phase-continuous glide
    n = int(dur * SR); e = env(n, kw.get("attack", 0.004), kw.get("release", 0.04)); s = []; ph = 0.0
    for i in range(n):
        f = f0 + (f1 - f0) * (i / n)
        ph += 2 * math.pi * f / SR
        v = math.sin(ph) + 0.3 * math.sin(2 * ph)
        if kw.get("decay"): v *= math.exp(-(i / SR) / kw["decay"])
        s.append(v * e[i])
    return s

def click(dur=0.008, seed=1, hp=True):
    rnd = random.Random(seed); n = int(dur * SR); prev = 0.0; s = []
    for i in range(n):
        x = rnd.uniform(-1, 1); y = x - prev if hp else x; prev = x
        s.append(y * math.exp(-i / (n / 4)))
    return s

def mix(*parts, offsets=None):
    offsets = offsets or [0.0] * len(parts)
    n = max(int(o * SR) + len(p) for p, o in zip(parts, offsets))
    out = [0.0] * n
    for p, o in zip(parts, offsets):
        st = int(o * SR)
        for i, v in enumerate(p): out[st + i] += v
    return out

def seq(*notes, gap=0.0):
    out = []
    for nte in notes:
        out += nte + [0.0] * int(gap * SR)
    return out

def normalise(s, rms_db=-14.0, peak_db=-1.0):
    rms = math.sqrt(sum(v * v for v in s) / max(1, len(s))) or 1e-9
    g = (10 ** (rms_db / 20)) / rms
    peak = max(abs(v) for v in s) or 1e-9
    g = min(g, (10 ** (peak_db / 20)) / peak)   # never clip: peak-limited
    return [v * g for v in s]

def write(name, s, rms_db=-14.0):
    s = normalise(s, rms_db)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v)) * 32767)) for v in s))
    meta = path + ".meta"
    if not os.path.exists(meta):
        open(meta, "w").write(
            "fileFormatVersion: 2\nguid: %s\nAudioImporter:\n  externalObjects: {}\n  serializedVersion: 8\n"
            "  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n"
            "    compressionFormat: 1\n    quality: 1\n    conversionMode: 0\n    preloadAudioData: 0\n"
            "  platformSettingOverrides: {}\n  forceToMono: 0\n  normalize: 0\n  loadInBackground: 0\n  ambisonic: 0\n"
            "  3D: 0\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % uuid.uuid4().hex)
    peak = max(abs(v) for v in s); rms = math.sqrt(sum(v * v for v in s) / len(s))
    print(f"{name:13s} {len(s)/SR:5.2f}s  rms {20*math.log10(rms):6.1f} dBFS  peak {20*math.log10(peak):5.1f} dBFS")

C6, E6, G6, C5, E5, G5 = 1046.5, 1318.5, 1568.0, 523.25, 659.25, 783.99
write("hover", tone(2200, 0.04, decay=0.012, release=0.01))
write("select", seq(tone(880, 0.05, decay=0.05), tone(1320, 0.07, decay=0.06)))
write("toggle", mix(click(0.008, 2), tone(1200, 0.035, decay=0.015)))
write("button", mix(click(0.006, 3), tone([660, 1320], 0.07, decay=0.04)))
write("dial_tick", mix(click(0.005, 4), tone(3000, 0.015, decay=0.004, release=0.005)))
write("grab", sweep(300, 620, 0.11, decay=0.12))
write("place", mix(sweep(420, 180, 0.15, decay=0.08), click(0.01, 5), offsets=[0.0, 0.0]))
write("correct", mix(tone(C6, 0.25, decay=0.18), tone(E6, 0.25, decay=0.18), offsets=[0.0, 0.08]))
write("wrong", tone(180, 0.25, shape="soft_square", decay=0.2, release=0.06))
write("phase_change", mix(tone(C5, 0.2, decay=0.15), tone(E5, 0.2, decay=0.15), tone(G5, 0.3, decay=0.2), offsets=[0, 0.09, 0.18]))
write("alarm", seq(tone(800, 0.25, attack=0.02, release=0.05), tone(600, 0.25, attack=0.02, release=0.05), gap=0.25))  # 1 Hz, never strobing
write("success", mix(tone(C5, 1.0, decay=0.6), tone(E5, 0.9, decay=0.6), tone(G5, 0.8, decay=0.6), tone(C6, 0.8, decay=0.7), offsets=[0, 0.1, 0.2, 0.3]))
write("fail", seq(tone(G5 / 2 * 1.0, 0.25, decay=0.3), tone(311.13, 0.4, decay=0.35)))

# Seamless 4 s hum: integer cycles of 60/120/180 Hz plus a looped, crossfaded soft noise bed.
n = 4 * SR; rnd = random.Random(7); noise = []; prev = 0.0
for i in range(n + SR // 2):
    prev = 0.995 * prev + 0.005 * rnd.uniform(-1, 1); noise.append(prev)
bed = []
fade = SR // 2
for i in range(n):
    v = noise[i]
    if i < fade:  # crossfade tail into head so the loop point is seamless
        k = i / fade; v = noise[i] * k + noise[n + i] * (1 - k)
    t = i / SR
    bed.append(0.6 * math.sin(2 * math.pi * 60 * t) + 0.25 * math.sin(2 * math.pi * 120 * t) + 0.1 * math.sin(2 * math.pi * 180 * t) + 6 * v)
write("ambient_hum", bed, rms_db=-30.0)
