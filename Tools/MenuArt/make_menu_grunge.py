#!/usr/bin/env python3
"""Generates the menu's white brush stroke.

Checked in as a SCRIPT rather than only as two PNGs, because a binary in a repository is a
decision nobody can read back. Re-run it and the same two files come out; change a number here
and the change is reviewable as a diff.

It carries its shape in the ALPHA channel and a flat white in RGB, so the Image that draws it is
tinted from UITheme rather than from the artwork - one texture, any colour, and no second file
when the palette moves.

It writes TWO files:

  T_MenuBrushStroke  the white stroke behind the selected row
  T_MenuGrungePanel  the dark torn backing behind the whole menu column

The panel was removed once, when a design reference without one was being matched, and is back
by request. The thing that has to be true of it is measurable and is measured at the bottom of
this file: its outermost row must be FULLY transparent. One opaque pixel there is, when the
Image is stretched, exactly the hard rectangular edge the texture exists to avoid - and the
first attempt at it failed precisely that way, at 98.4% non-empty, because both noise layers had
a floor and could never reach zero.

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


def grunge_panel(w=512, h=512):
    """The dark torn backing behind the menu column.

    Carried in the ALPHA channel over near-black RGB, so the Image that draws it is tinted from
    UITheme like everything else. Three things it must not be: a clean rectangle, a rounded card,
    and opaque at its border. The first two are why the shape comes from noise rather than from a
    draw call; the third is checked rather than hoped for.
    """
    px = [0] * (w * h)
    cx, cy = w * 0.46, h * 0.50
    rx, ry = w * 0.52, h * 0.54

    for y in range(h):
        dy = (y - cy) / ry
        row = y * w
        for x in range(w):
            dx = (x - cx) / rx
            d = math.sqrt(dx * dx + dy * dy)
            # Dense core, long soft shoulder. Nothing survives past d = 1.
            v = 1.0 - d
            if v <= 0.0:
                continue
            px[row + x] = int(min(1.0, v * 1.35) ** 1.25 * 255)

    field = Image.new("L", (w, h))
    field.putdata(px)

    # The edge is CHEWED rather than blurred: coarse noise multiplied in breaks the oval into a
    # torn patch, and because the noise floor is zero the tears go all the way through.
    tear = fractal_noise(w, h, octaves=6, base=3).point(lambda v: min(255, int(v * 1.55)))
    field = ImageChops.multiply(field, tear)

    # A hard vignette to zero over the outer eighth, so the border cannot be anything but empty
    # whatever the noise did. This is the line that makes the measurement below pass.
    margin = max(4, int(min(w, h) * 0.12))
    fade = Image.new("L", (w, h), 255)
    fd = ImageDraw.Draw(fade)
    for i in range(margin):
        level = int(255 * (i / float(margin)) ** 0.8)
        fd.rectangle([i, i, w - 1 - i, h - 1 - i], outline=level)
    field = ImageChops.multiply(field, fade)

    # Lift the middle back up - the multiplies above cost the core its density - and cap well
    # short of opaque, because the corridor has to stay readable through it.
    field = field.point(lambda v: min(208, int(v * 1.6)))
    field = field.filter(ImageFilter.GaussianBlur(1.6))

    # Anything faint is snapped to nothing rather than left as a film. A 2-alpha haze over a
    # whole quarter of the screen is not a shape, it is a tint nobody asked for.
    field = field.point(lambda v: 0 if v < 14 else v)

    img = Image.new("RGBA", (w, h), (10, 9, 11, 0))
    img.putalpha(field)
    return img


if __name__ == "__main__":
    import os
    os.makedirs(OUT, exist_ok=True)
    for name, img in (("T_MenuBrushStroke", brush_stroke()),
                      ("T_MenuGrungePanel", grunge_panel())):
        path = os.path.join(OUT, name + ".png")
        img.save(path)
        mean, lit, solid = coverage(img)
        a = img.getchannel("A")
        ww, hh = img.size
        border = max([a.getpixel((x, 0)) for x in range(ww)] +
                     [a.getpixel((x, hh - 1)) for x in range(ww)] +
                     [a.getpixel((0, y)) for y in range(hh)] +
                     [a.getpixel((ww - 1, y)) for y in range(hh)])
        print(f"{name}.png  {ww}x{hh}  "
              f"mean alpha {mean:.3f}  non-empty {lit:.1%}  fully opaque {solid:.1%}  "
              f"max border alpha {border}")
