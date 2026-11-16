"""Generate the Vulcan command icon strips for the CommandManager toolbar and ribbon.

Each command occupies one square tile; tiles are laid out horizontally into one
strip per size (20/32/40/64/96/128). ICommandGroup.IconList expects one strip
per size, and AddCommandItem2 indexes into that strip by command order, so the
tile order here must stay in sync with the commandDefinitions order in SwAddIn.cs.
"""
from PIL import Image, ImageDraw
import os

OUT = os.path.dirname(os.path.abspath(__file__))
SIZES = (20, 32, 40, 64, 96, 128)

# Command order must match commandDefinitions in Core/SwAddIn.cs.
NAMES = ('pane', 'cylinder', 'plate', 'settings', 'health', 'status', 'logs', 'about')

BG = (0x0A, 0x0A, 0x0A, 255)
BORDER = (0x2C, 0x2C, 0x2E, 255)
FG = (0xF5, 0xF5, 0xF7, 255)
ACCENT = (0xE1, 0x1D, 0x2E, 255)


def draw_tile(name, s):
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r = max(2, int(s * 0.18))
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=r, fill=BG, outline=BORDER,
                        width=max(1, int(s * 0.03)))
    w = max(1, int(s * 0.07))
    c = s / 2.0

    if name == 'pane':
        d.rectangle([s * 0.22, s * 0.26, s * 0.78, s * 0.74], outline=FG, width=w)
        d.line([s * 0.22, s * 0.40, s * 0.78, s * 0.40], fill=FG, width=w)
        d.ellipse([s * 0.62, s * 0.30, s * 0.72, s * 0.40], fill=ACCENT)
    elif name == 'cylinder':
        d.ellipse([s * 0.28, s * 0.18, s * 0.72, s * 0.36], outline=FG, width=w)
        d.line([s * 0.28, s * 0.27, s * 0.28, s * 0.72], fill=FG, width=w)
        d.line([s * 0.72, s * 0.27, s * 0.72, s * 0.72], fill=FG, width=w)
        d.arc([s * 0.28, s * 0.63, s * 0.72, s * 0.81], start=0, end=180, fill=FG, width=w)
    elif name == 'plate':
        d.rectangle([s * 0.20, s * 0.30, s * 0.80, s * 0.70], outline=FG, width=w)
        for x in (0.30, 0.62):
            for y in (0.40, 0.60):
                d.ellipse([s * x, s * y, s * (x + 0.08), s * (y + 0.08)], fill=ACCENT)
    elif name == 'settings':
        d.ellipse([s * 0.36, s * 0.36, s * 0.64, s * 0.64], outline=FG, width=w)
        for i in range(8):
            a = i * 3.141592653589793 / 4
            import math
            d.line([c + math.cos(a) * s * 0.22, c + math.sin(a) * s * 0.22,
                    c + math.cos(a) * s * 0.32, c + math.sin(a) * s * 0.32], fill=FG, width=w)
    elif name == 'health':
        d.line([s * 0.22, c, s * 0.40, c, s * 0.50, s * 0.30, s * 0.60, s * 0.72,
                s * 0.68, c, s * 0.78, c], fill=FG, width=w, joint='curve')
    elif name == 'status':
        d.line([s * 0.20, s * 0.55, s * 0.36, s * 0.55, s * 0.44, s * 0.34, s * 0.54,
                s * 0.72, s * 0.62, s * 0.48, s * 0.80, s * 0.48], fill=FG, width=w, joint='curve')
        d.ellipse([s * 0.72, s * 0.20, s * 0.82, s * 0.30], fill=ACCENT)
    elif name == 'logs':
        d.rectangle([s * 0.26, s * 0.18, s * 0.74, s * 0.82], outline=FG, width=w)
        for y in (0.34, 0.46, 0.58):
            d.line([s * 0.34, s * y, s * 0.66, s * y], fill=FG, width=w)
        d.ellipse([s * 0.60, s * 0.66, s * 0.70, s * 0.76], fill=ACCENT)
    else:  # about
        d.ellipse([s * 0.28, s * 0.28, s * 0.72, s * 0.72], outline=FG, width=w)
        d.ellipse([s * 0.47, s * 0.36, s * 0.53, s * 0.42], fill=ACCENT)
        d.line([c, s * 0.47, c, s * 0.64], fill=FG, width=w)

    return img


for size in SIZES:
    strip = Image.new('RGBA', (size * len(NAMES), size), (0, 0, 0, 0))
    for i, name in enumerate(NAMES):
        strip.paste(draw_tile(name, size), (i * size, 0))
    strip.save(os.path.join(OUT, f'strip_{size}.png'), optimize=True)

print('command strips written to', OUT)
