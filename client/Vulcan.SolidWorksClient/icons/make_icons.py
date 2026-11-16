"""Generate the Vulcan icon set: minimal V mark, black tile, signal red dot."""
from PIL import Image, ImageDraw
import os

OUT = os.path.dirname(os.path.abspath(__file__))
EMB = os.path.join(os.path.dirname(OUT), 'Vulcan Setup', 'embedded')
os.makedirs(EMB, exist_ok=True)

S = 1024
BLACK = (0x0A, 0x0A, 0x0A, 255)
BORDER = (0x26, 0x26, 0x26, 255)
WHITE = (0xFF, 0xFF, 0xFF, 255)
RED = (0xE1, 0x1D, 0x2E, 255)


def render(size=S):
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pad = max(1, int(size * 0.03))
    radius = int(size * 0.22)
    d.rounded_rectangle(
        [pad, pad, size - pad, size - pad],
        radius=radius,
        fill=BLACK,
        outline=BORDER,
        width=max(1, int(size * 0.016)),
    )
    stroke = max(2, int(size * 0.095))
    points = [
        (int(size * 0.30), int(size * 0.30)),
        (int(size * 0.50), int(size * 0.72)),
        (int(size * 0.70), int(size * 0.30)),
    ]
    d.line(points, fill=WHITE, width=stroke, joint='curve')
    r = stroke // 2
    for x, y in (points[0], points[2]):
        d.ellipse([x - r, y - r, x + r, y + r], fill=WHITE)
    dot = int(size * 0.062)
    cx, cy = int(size * 0.70), int(size * 0.30)
    d.ellipse([cx - dot, cy - dot, cx + dot, cy + dot], fill=RED)
    return img


tile = render()

for size in (20, 32, 40, 64, 96, 128):
    tile.resize((size, size), Image.LANCZOS).save(
        os.path.join(OUT, f'icon_{size}.png'), optimize=True
    )

base = tile.resize((256, 256), Image.LANCZOS)
ico_sizes = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48),
             (64, 64), (96, 96), (128, 128), (256, 256)]
base.save(os.path.join(OUT, 'Vulcan.ico'), sizes=ico_sizes)
base.save(os.path.join(EMB, 'Vulcan.ico'), sizes=ico_sizes)
tile.resize((512, 512), Image.LANCZOS).save(os.path.join(EMB, 'Vulcan.png'), optimize=True)

print('icons written to', OUT)
