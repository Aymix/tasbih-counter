#!/usr/bin/env python3
"""Generate the Tasbih Counter app icon (icon.ico + preview PNG).

Design: a misbaha (prayer-bead) ring on a dark rounded tile. Beads use the
app's dhikr green (#66E0A3) with one gold accent bead (#F0C674, = Allahu akbar),
plus a leader bead and short tassel at the bottom gap. Reproducible — just rerun.
"""
import math
from PIL import Image, ImageDraw

SS = 8                     # supersample factor for crisp anti-aliasing
BASE = 256                 # logical icon size
N = BASE * SS              # working canvas size

GREEN = (0x66, 0xE0, 0xA3, 255)
GREEN_DK = (0x3E, 0x9E, 0x74, 255)
GOLD = (0xF0, 0xC6, 0x74, 255)
GOLD_DK = (0xB9, 0x92, 0x45, 255)
TILE = (0x18, 0x1B, 0x1A, 255)
TILE_EDGE = (0x2C, 0x53, 0x45, 255)


def disc(draw, cx, cy, r, fill, outline=None, ow=0):
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill,
                 outline=outline, width=ow)


def bead(draw, cx, cy, r, base, dark):
    """A bead with a soft shaded rim and a small specular highlight."""
    disc(draw, cx, cy, r, dark)
    disc(draw, cx, cy, int(r * 0.86), base)
    hl = int(r * 0.30)
    disc(draw, cx - int(r * 0.32), cy - int(r * 0.34), hl,
         (255, 255, 255, 90))


def build():
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Rounded dark tile.
    pad = int(N * 0.055)
    rad = int(N * 0.22)
    d.rounded_rectangle([pad, pad, N - pad, N - pad], radius=rad,
                        fill=TILE, outline=TILE_EDGE, width=int(N * 0.012))

    cx = cy = N // 2
    ring_r = int(N * 0.30)
    bead_r = int(N * 0.052)

    # Bead ring: 12 beads over a 300° arc, 60° gap at the bottom for the tassel.
    beads = 12
    arc = 300.0
    start = 90 + (360 - arc) / 2          # begin just past bottom-left
    for i in range(beads):
        ang = math.radians(start + arc * i / (beads - 1))
        bx = cx + ring_r * math.cos(ang)
        by = cy + ring_r * math.sin(ang)
        # One gold accent bead near the top; the rest green.
        if i == beads // 2:
            bead(d, bx, by, int(bead_r * 1.05), GOLD, GOLD_DK)
        else:
            bead(d, bx, by, bead_r, GREEN, GREEN_DK)

    # Leader bead + tassel at the bottom gap.
    ly = cy + ring_r + int(N * 0.02)
    bead(d, cx, ly, int(bead_r * 1.25), GREEN, GREEN_DK)
    d.line([cx, ly + bead_r, cx, ly + int(N * 0.085)],
           fill=GREEN_DK, width=int(N * 0.016))
    disc(d, cx, ly + int(N * 0.10), int(bead_r * 0.7), GOLD)

    # Downsample to the logical size, then emit a multi-resolution .ico.
    icon = img.resize((BASE, BASE), Image.LANCZOS)
    icon.save("icon.ico", format="ICO",
              sizes=[(256, 256), (128, 128), (64, 64),
                     (48, 48), (32, 32), (16, 16)])
    icon.save("icon-preview.png")
    print("wrote icon.ico and icon-preview.png")


if __name__ == "__main__":
    build()
