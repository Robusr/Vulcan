"""Generate the Vulcan command icon strips for the CommandManager toolbar and ribbon.

Each command occupies one square tile; tiles are laid out horizontally into one
strip per size (20/32/40/64/96/128). ICommandGroup.IconList expects one strip
per size, and AddCommandItem2 indexes into that strip by command order, so the
tile order here must stay in sync with the commandDefinitions order in SwAddIn.cs.

SolidWorks 2025 SP0 renders this add-in's CommandManager band with a fixed
20 px tile at 100% DPI, so the glyph inside each tile is drawn 1.25x larger
around the tile center. This keeps the tile size and layout unchanged while
making the visible symbol noticeably bigger.
"""
from PIL import Image, ImageDraw
import math
import os

OUT = os.path.dirname(os.path.abspath(__file__))
SIZES = (20, 32, 40, 64, 96, 128)

# Command order must match commandDefinitions in Core/SwAddIn.cs.
NAMES = ('pane', 'cylinder', 'plate', 'settings', 'health', 'status', 'logs', 'about')

BG = (0x0A, 0x0A, 0x0A, 255)
BORDER = (0x2C, 0x2C, 0x2E, 255)
FG = (0xF5, 0xF5, 0xF7, 255)
ACCENT = (0xE1, 0x1D, 0x2E, 255)

# Enlarge the glyph around the tile center; every shape's outermost point is at
# 0.18..0.82, so 1.25x keeps it inside 0.10..0.90 of the tile.
GLYPH_SCALE = 1.25
SUPERSAMPLE = 8


def draw_glyph(name, s):
    canvas = s * SUPERSAMPLE
    img = Image.new('RGBA', (canvas, canvas), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    S = canvas
    w = max(1, int(S * 0.06))
    c = S / 2.0

    if name == 'pane':
        d.rectangle([S * 0.22, S * 0.26, S * 0.78, S * 0.74], outline=FG, width=w)
        d.line([S * 0.22, S * 0.40, S * 0.78, S * 0.40], fill=FG, width=w)
        d.ellipse([S * 0.62, S * 0.30, S * 0.72, S * 0.40], fill=ACCENT)
    elif name == 'cylinder':
        d.ellipse([S * 0.28, S * 0.18, S * 0.72, S * 0.36], outline=FG, width=w)
        d.line([S * 0.28, S * 0.27, S * 0.28, S * 0.72], fill=FG, width=w)
        d.line([S * 0.72, S * 0.27, S * 0.72, S * 0.72], fill=FG, width=w)
        d.arc([S * 0.28, S * 0.63, S * 0.72, S * 0.81], start=0, end=180, fill=FG, width=w)
    elif name == 'plate':
        d.rectangle([S * 0.20, S * 0.30, S * 0.80, S * 0.70], outline=FG, width=w)
        for x in (0.30, 0.62):
            for y in (0.40, 0.60):
                d.ellipse([S * x, S * y, S * (x + 0.08), S * (y + 0.08)], fill=ACCENT)
    elif name == 'settings':
        d.ellipse([S * 0.36, S * 0.36, S * 0.64, S * 0.64], outline=FG, width=w)
        for i in range(8):
            a = i * math.pi / 4
            d.line([c + math.cos(a) * S * 0.22, c + math.sin(a) * S * 0.22,
                    c + math.cos(a) * S * 0.32, c + math.sin(a) * S * 0.32], fill=FG, width=w)
    elif name == 'health':
        d.line([S * 0.22, c, S * 0.40, c, S * 0.50, S * 0.30, S * 0.60, S * 0.72,
                S * 0.68, c, S * 0.78, c], fill=FG, width=w, joint='curve')
    elif name == 'status':
        d.line([S * 0.20, S * 0.55, S * 0.36, S * 0.55, S * 0.44, S * 0.34, S * 0.54,
                S * 0.72, S * 0.62, S * 0.48, S * 0.80, S * 0.48], fill=FG, width=w, joint='curve')
        d.ellipse([S * 0.72, S * 0.20, S * 0.82, S * 0.30], fill=ACCENT)
    elif name == 'logs':
        d.rectangle([S * 0.26, S * 0.18, S * 0.74, S * 0.82], outline=FG, width=w)
        for y in (0.34, 0.46, 0.58):
            d.line([S * 0.34, S * y, S * 0.66, S * y], fill=FG, width=w)
        d.ellipse([S * 0.60, S * 0.66, S * 0.70, S * 0.76], fill=ACCENT)
    else:  # about
        d.ellipse([S * 0.28, S * 0.28, S * 0.72, S * 0.72], outline=FG, width=w)
        d.ellipse([S * 0.47, S * 0.36, S * 0.53, S * 0.42], fill=ACCENT)
        d.line([c, S * 0.47, c, S * 0.64], fill=FG, width=w)

    scaled = img.resize(
        (round(canvas * GLYPH_SCALE), round(canvas * GLYPH_SCALE)),
        Image.LANCZOS,
    )
    off = (scaled.width - canvas) // 2
    glyph = scaled.crop((off, off, off + canvas, off + canvas))
    return glyph.resize((s, s), Image.LANCZOS)


def draw_tile(name, s):
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r = max(2, int(s * 0.18))
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=r, fill=BG, outline=BORDER,
                        width=max(1, int(s * 0.03)))
    img.alpha_composite(draw_glyph(name, s))
    return img


for size in SIZES:
    strip = Image.new('RGBA', (size * len(NAMES), size), (0, 0, 0, 0))
    for i, name in enumerate(NAMES):
        strip.paste(draw_tile(name, size), (i * size, 0))
    strip.save(os.path.join(OUT, f'strip_{size}.png'), optimize=True)

print('command strips written to', OUT)
