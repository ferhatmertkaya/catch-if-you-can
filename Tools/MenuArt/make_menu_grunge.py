#!/usr/bin/env python3
"""Generates the two menu sprites: the white brush stroke and the dark grunge panel.

Checked in as a SCRIPT rather than only as two PNGs, because a binary in a repository is a
decision nobody can read back. Re-run it and the same two files come out; change a number here
and the change is reviewable as a diff.

Both files carry their shape in the ALPHA channel and a flat colour in RGB, so the Image that
draws them is tinted from UITheme rather than from the artwork - one texture, any colour, and
no second file when the palette moves.

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


def brush_stroke(w=768, h=160):
    """One rough horizontal paint stroke: ragged edges, dry-brush streaks, torn ends."""
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)

    mid = h * 0.5
    half = h * 0.33
    top = wobble(w, -1.0, 1.0)
    bot = wobble(w, -1.0, 1.0)

    for x in range(w):
        # Ends taper and tear rather than stopping square.
        t = x / (w - 1.0)
        end = min(1.0, (min(t, 1.0 - t) / 0.09))
        end = end ** 0.65
        if end <= 0.001:
            continue
        y0 = mid - (half + top[x] * 16.0) * end
        y1 = mid + (half + bot[x] * 16.0) * end
        d.line([(x, y0), (x, y1)], fill=255)

    # Dry brush: horizontal streaks lifted out of the body, so it reads as bristles.
    streaks = Image.new("L", (w, h), 255)
    sd = ImageDraw.Draw(streaks)
    for _ in range(26):
        y = random.uniform(4, h - 4)
        thick = random.uniform(1.0, 4.0)
        x0 = random.uniform(-40, w * 0.6)
        x1 = x0 + random.uniform(w * 0.25, w * 0.9)
        sd.line([(x0, y), (x1, y)], fill=random.randint(60, 170), width=int(thick))
    streaks = streaks.filter(ImageFilter.GaussianBlur(1.2))
    mask = ImageChops.multiply(mask, streaks)

    # Broken grain over the whole stroke.
    grain = fractal_noise(w, h, octaves=5, base=6).point(lambda v: 120 + int(v * 0.62))
    mask = ImageChops.multiply(mask, grain)
    mask = mask.filter(ImageFilter.GaussianBlur(0.7))
    mask = mask.point(lambda v: 0 if v < 26 else min(255, int((v - 26) * 1.45)))

    img = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    img.putalpha(mask)
    return img


def grunge_panel(w=768, h=768):
    """A dark irregular overlay: solid in the middle, torn apart towards every edge."""
    # A soft rectangular core, so the middle is genuinely opaque and the edge is not a rectangle.
    core = Image.new("L", (w, h), 0)
    cd = ImageDraw.Draw(core)
    inset_x, inset_y = w * 0.085, h * 0.085
    cd.rectangle([inset_x, inset_y, w - inset_x, h - inset_y], fill=255)
    core = core.filter(ImageFilter.GaussianBlur(w * 0.075))

    # Torn edges: the falloff is chewed by noise, so no straight line survives anywhere.
    # NO FLOOR under the noise. The first version added one (70 + v*0.72) and the panel came out
    # 98% non-empty - a uniform veil right up to the border, which is the black box it exists to
    # avoid, only softer. A mask that can never reach zero has no torn edge to give.
    tear = fractal_noise(w, h, octaves=6, base=3).point(lambda v: min(255, int(v * 2.05)))
    mask = ImageChops.multiply(core, tear)

    # Painted streaks, so it reads as brushed on rather than as a gradient.
    strokes = Image.new("L", (w, h), 255)
    sd = ImageDraw.Draw(strokes)
    for _ in range(64):
        y = random.uniform(0, h)
        sd.line([(random.uniform(-60, w * 0.5), y),
                 (random.uniform(w * 0.5, w + 60), y + random.uniform(-6, 6))],
                fill=random.randint(95, 200), width=int(random.uniform(3, 16)))
    strokes = strokes.filter(ImageFilter.GaussianBlur(3.5))
    mask = ImageChops.multiply(mask, strokes)

    # Speckle: small holes that let the 3D scene through in the thin areas.
    holes = fractal_noise(w, h, octaves=6, base=16).point(lambda v: 255 if v > 110 else 120 + v)
    mask = ImageChops.multiply(mask, holes)

    mask = mask.filter(ImageFilter.GaussianBlur(2.2))

    # An S-curve rather than a gain: the core is pushed to solid and the thin outer half is
    # pushed to nothing, which is what separates a torn edge from a gradient.
    def curve(v):
        t = v / 255.0
        t = max(0.0, (t - 0.12) / 0.40)
        t = min(1.0, t)
        return int(255 * (t * t * (3 - 2 * t)))
    mask = mask.point(curve)

    # The border is forced to zero. A sprite that is stretched must not end on a visible seam,
    # and one opaque pixel in the outermost row is exactly that seam.
    bd = ImageDraw.Draw(mask)
    bd.rectangle([0, 0, w - 1, h - 1], outline=0, width=3)

    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
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
    for name, img in (("T_MenuBrushStroke", brush_stroke()),
                      ("T_MenuGrungePanel", grunge_panel())):
        path = os.path.join(OUT, name + ".png")
        img.save(path)
        mean, lit, solid = coverage(img)
        print(f"{name}.png  {img.size[0]}x{img.size[1]}  "
              f"mean alpha {mean:.3f}  non-empty {lit:.1%}  fully opaque {solid:.1%}")
