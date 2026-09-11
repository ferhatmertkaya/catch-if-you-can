#!/usr/bin/env python3
"""Generates the menu's white brush stroke.

Checked in as a SCRIPT rather than only as two PNGs, because a binary in a repository is a
decision nobody can read back. Re-run it and the same two files come out; change a number here
and the change is reviewable as a diff.

It carries its shape in the ALPHA channel and a flat white in RGB, so the Image that draws it is
tinted from UITheme rather than from the artwork - one texture, any colour, and no second file
when the palette moves.

There WAS a second file here, a dark grunge panel behind the whole menu. It is gone: the design
reference has no panel, and the scene's own left side is already black, so the panel was covering
a problem that does not exist with a rectangle-shaped answer. Removed together with its meta, its
use in the baker and the guard that measured it - a texture nobody draws is the kind of leftover
that reads as intentional to the next person (mistake 14).

    python3 Tools/MenuArt/make_menu_grunge.py
"""
import math, random
from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUT = "Assets/CatchIfYouCan/Resources/UI/Menu"
random.seed(20260911)          # fixed: the same art every run, so a rebuild is not a redesign


def fractal_noise(w, h, octaves=5, base=4):
    """Layered value noise, coarse to fine, normalised to 0..255."""
    acc = Image.new("L", (w, h), 0)
    amp = 1.0
    total = 0.0
    for o in range(octaves):
        cells = base * (2 ** o)
        small = Image.effect_noise((max(2, cells), max(2, cells)), 96)
        layer = small.resize((w, h), Image.BICUBIC)
        acc = ImageChops.add(acc, layer.point(lambda v, a=amp: int(v * a)), scale=1.0)
        total += amp
        amp *= 0.55
    return acc.point(lambda v: min(255, int(v / max(total, 0.001) * 1.35)))


def wobble(n, low, high, smooth=9):
    """A smoothed random walk of n values in [low, high] - one ragged edge."""
    raw = [random.uniform(low, high) for _ in range(n)]
    out = []
    for i in range(n):
        a = max(0, i - smooth)
        b = min(n, i + smooth + 1)
        out.append(sum(raw[a:b]) / (b - a))
    return out


def brush_stroke(w=768, h=112):
    """One rough horizontal paint stroke: ragged edges, dry-brush streaks, torn ends."""
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)

    mid = h * 0.5
    half = h * 0.40
    top = wobble(w, -1.0, 1.0)
    bot = wobble(w, -1.0, 1.0)

    for x in range(w):
        # Ends taper and tear rather than stopping square.
        t = x / (w - 1.0)
        end = min(1.0, (min(t, 1.0 - t) / 0.055))
        end = end ** 0.65
        if end <= 0.001:
            continue
        y0 = mid - (half + top[x] * 16.0) * end
        y1 = mid + (half + bot[x] * 16.0) * end
        d.line([(x, y0), (x, y1)], fill=255)

    # Dry brush: horizontal streaks lifted out of the body, so it reads as bristles.
    streaks = Image.new("L", (w, h), 255)
    sd = ImageDraw.Draw(streaks)
    for _ in range(14):
        y = random.uniform(4, h - 4)
        thick = random.uniform(1.0, 4.0)
        x0 = random.uniform(-40, w * 0.6)
        x1 = x0 + random.uniform(w * 0.25, w * 0.9)
        sd.line([(x0, y), (x1, y)], fill=random.randint(130, 205), width=int(thick))
    streaks = streaks.filter(ImageFilter.GaussianBlur(1.2))
    mask = ImageChops.multiply(mask, streaks)

    # Broken grain over the whole stroke.
    grain = fractal_noise(w, h, octaves=5, base=6).point(lambda v: 165 + int(v * 0.42))
    mask = ImageChops.multiply(mask, grain)
    mask = mask.filter(ImageFilter.GaussianBlur(0.7))
    mask = mask.point(lambda v: 0 if v < 26 else min(255, int((v - 26) * 1.45)))

    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    img.putalpha(mask)
    return img


def coverage(img):
    a = img.getchannel("A")
    px = list(a.getdata())
    n = len(px)
    return (sum(px) / n / 255.0,
            sum(1 for v in px if v > 8) / n,
            sum(1 for v in px if v > 247) / n)


if __name__ == "__main__":
    import os
    os.makedirs(OUT, exist_ok=True)
    for name, img in (("T_MenuBrushStroke", brush_stroke()),):
        path = os.path.join(OUT, name + ".png")
        img.save(path)
        mean, lit, solid = coverage(img)
        print(f"{name}.png  {img.size[0]}x{img.size[1]}  "
              f"mean alpha {mean:.3f}  non-empty {lit:.1%}  fully opaque {solid:.1%}")
