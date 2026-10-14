#!/usr/bin/env python3
"""Renders thunderstore/icon.png (256x256): a glowing ritual circle on a dark background."""
import math
import sys
from PIL import Image, ImageDraw, ImageFilter

SIZE = 256
CENTER = SIZE / 2
out = sys.argv[1] if len(sys.argv) > 1 else "thunderstore/icon.png"

img = Image.new("RGBA", (SIZE, SIZE), (14, 8, 22, 255))

glow = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
d = ImageDraw.Draw(glow)
d.ellipse([CENTER - 92, CENTER - 92, CENTER + 92, CENTER + 92], outline=(180, 110, 255, 255), width=14)
glow = glow.filter(ImageFilter.GaussianBlur(10))
img.alpha_composite(glow)

d = ImageDraw.Draw(img)
d.ellipse([CENTER - 92, CENTER - 92, CENTER + 92, CENTER + 92], outline=(214, 170, 255, 255), width=6)
d.ellipse([CENTER - 70, CENTER - 70, CENTER + 70, CENTER + 70], outline=(150, 90, 220, 255), width=3)

# A torn page in the middle of the circle.
page = [(CENTER - 34, CENTER - 44), (CENTER + 22, CENTER - 44), (CENTER + 34, CENTER - 32),
        (CENTER + 34, CENTER + 44), (CENTER - 34, CENTER + 44)]
d.polygon(page, fill=(46, 26, 70, 255), outline=(214, 170, 255, 255), width=3)
d.line([(CENTER + 22, CENTER - 44), (CENTER + 22, CENTER - 32), (CENTER + 34, CENTER - 32)], fill=(214, 170, 255, 255), width=3)
for i, width in enumerate((44, 36, 44, 28)):
    y = CENTER - 20 + i * 15
    d.line([(CENTER - 22, y), (CENTER - 22 + width, y)], fill=(180, 120, 240, 255), width=3)

# Location pin marker at the top of the ring.
d.ellipse([CENTER - 14, CENTER - 106, CENTER + 14, CENTER - 78], fill=(255, 200, 90, 255))
d.ellipse([CENTER - 6, CENTER - 98, CENTER + 6, CENTER - 86], fill=(14, 8, 22, 255))

img.save(out)
print("wrote", out)
