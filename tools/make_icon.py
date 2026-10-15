#!/usr/bin/env python3
"""Renders thunderstore/icon.png (256x256): the Reincarnation page (a knife inked on a torn page) inside a sacrifice
zone ring. Usage: make_icon.py [out.png]"""
import math
import sys
from PIL import Image, ImageDraw, ImageFilter

SIZE = 256
SS = 4  # supersampling factor
W = SIZE * SS

out = sys.argv[1] if len(sys.argv) > 1 else "thunderstore/icon.png"

BACKGROUND = (14, 8, 22, 255)
RING = (186, 120, 255)
RING_CORE = (226, 196, 255)
RING_INNER = (150, 90, 220)
PIN = (255, 200, 90)
PAGE = (228, 210, 192)
PAGE_EDGE = (196, 170, 156)
FOLD = (70, 52, 92)
INK = (38, 28, 34)
OUTLINE = (70, 150, 255)


def s(v):
    return v * SS


def blank():
    return Image.new("RGBA", (W, W), (0, 0, 0, 0))


def solid(color, alpha):
    return Image.merge("RGBA", (*[Image.new("L", (W, W), c) for c in color], alpha))


def ring(img, c, radius, width):
    box = [c - radius, c - radius, c + radius, c + radius]
    glow = blank()
    ImageDraw.Draw(glow).ellipse(box, outline=RING + (255,), width=width * 3)
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(s(5))))
    ImageDraw.Draw(img).ellipse(box, outline=RING_CORE + (255,), width=width)


def page_layer(cx, cy, w, h, angle):
    """The page, its outline glow and its knife, as a layer to composite."""
    a = math.radians(angle)
    ca, sa = math.cos(a), math.sin(a)

    def to_canvas(u, v):
        return (cx + u * ca - v * sa, cy + u * sa + v * ca)

    fold = w * 0.28
    hw, hh = w / 2, h / 2
    outline_pts = [to_canvas(*p) for p in
                   [(-hw, -hh), (hw, -hh), (hw, hh), (-hw + fold, hh), (-hw, hh - fold)]]

    layer = blank()

    # Blue item outline, like the game's pickup icons.
    mask = Image.new("L", (W, W), 0)
    ImageDraw.Draw(mask).polygon(outline_pts, fill=255)
    halo = mask.filter(ImageFilter.MaxFilter(s(5) | 1)).filter(ImageFilter.GaussianBlur(s(2.5)))
    layer.alpha_composite(solid(OUTLINE, halo))
    layer.alpha_composite(solid(OUTLINE, mask.filter(ImageFilter.MaxFilter(s(2) | 1))))

    # Parchment with darker edges.
    inner = mask.filter(ImageFilter.MinFilter(s(9) | 1)).filter(ImageFilter.GaussianBlur(s(7)))
    paper = Image.composite(Image.new("RGBA", (W, W), PAGE + (255,)), Image.new("RGBA", (W, W), PAGE_EDGE + (255,)), inner)
    layer.paste(paper, (0, 0), mask)

    d = ImageDraw.Draw(layer)
    # The folded-over corner shows the page's dark back.
    d.polygon([to_canvas(-hw, hh - fold), to_canvas(-hw + fold, hh), to_canvas(-hw + fold * 0.9, hh - fold * 0.9)],
              fill=FOLD + (255,))

    # Knife: a curved blade and a rounded handle, tip at the top left. In knife space x runs along the length (0..1)
    # and y across it. The knife's bounding box is centered on the page.
    length = h * 0.88
    k_angle = math.radians(45)
    kc, ks = math.cos(k_angle), math.sin(k_angle)

    def spine(x):
        return 0.09 * (1 - (1 - x / 0.6) ** 2)

    def edge(x):
        return 0.25 * (1 - (1 - x / 0.6) ** 2.6)

    steps = [i / 24 * 0.6 for i in range(25)]
    blade = [(x, spine(x)) for x in steps] + [(x, edge(x)) for x in reversed(steps)]
    handle = [(0.66, 0.16), (0.93, 0.16)]
    grip = 0.1

    xs = [x for x, _ in blade] + [handle[1][0] + grip / 2]  # the handle's round end sticks out past its endpoint
    ys = [y for _, y in blade] + [handle[0][1] - grip / 2, handle[0][1] + grip / 2]
    mid_x = (min(xs) + max(xs)) / 2
    mid_y = (min(ys) + max(ys)) / 2

    def knife(x, y):
        x, y = x - mid_x, y - mid_y
        return to_canvas((x * kc - y * ks) * length, (x * ks + y * kc) * length)

    d.polygon([knife(*q) for q in blade], fill=INK + (255,))
    grip_px = grip * length
    ends = [knife(*q) for q in handle]
    d.line(ends, fill=INK + (255,), width=int(grip_px))
    for x, y in ends:
        d.ellipse([x - grip_px / 2, y - grip_px / 2, x + grip_px / 2, y + grip_px / 2], fill=INK + (255,))

    return layer


def main():
    img = Image.new("RGBA", (W, W), BACKGROUND)
    c = W / 2

    # The page inside a sacrifice zone ring seen from above, with a location pin on the ring.
    ring(img, c, s(100), s(5))
    ImageDraw.Draw(img).ellipse([c - s(80), c - s(80), c + s(80), c + s(80)], outline=RING_INNER + (255,), width=s(2))
    img.alpha_composite(page_layer(c, c, s(100), s(128), -12))
    d = ImageDraw.Draw(img)
    d.ellipse([c - s(14), c - s(114), c + s(14), c - s(86)], fill=PIN + (255,))
    d.ellipse([c - s(6), c - s(106), c + s(6), c - s(94)], fill=BACKGROUND)

    img.resize((SIZE, SIZE), Image.LANCZOS).save(out)
    print("wrote", out)


if __name__ == "__main__":
    main()
