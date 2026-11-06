"""Generate the Vulcan icon set from the brand palette (forge hammer tile)."""
from PIL import Image, ImageDraw
import os

OUT = os.path.dirname(os.path.abspath(__file__))
EMB = os.path.join(os.path.dirname(OUT), 'Vulcan Setup', 'embedded')
os.makedirs(EMB, exist_ok=True)

S = 1024
CX = CY = S // 2


def vgrad(size, top, bottom):
    top_img = Image.new('RGBA', size, top)
    bot_img = Image.new('RGBA', size, bottom)
    mask = Image.linear_gradient('L').resize(size)
    return Image.composite(bot_img, top_img, mask)


def hgrad(size, left, mid, right):
    w, h = size
    grad = Image.new('RGBA', size)
    d = ImageDraw.Draw(grad)
    for x in range(w):
        t = x / max(1, w - 1)
        if t < 0.55:
            u = t / 0.55
            c = tuple(int(left[i] + (mid[i] - left[i]) * u) for i in range(3)) + (255,)
        else:
            u = (t - 0.55) / 0.45
            c = tuple(int(mid[i] + (right[i] - mid[i]) * u) for i in range(3)) + (255,)
        d.line([(x, 0), (x, h)], fill=c)
    return grad


tile_mask = Image.new('L', (S, S), 0)
ImageDraw.Draw(tile_mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=255)
tile = Image.new('RGBA', (S, S), (0, 0, 0, 0))
tile.paste(vgrad((S, S), (0x16, 0x22, 0x3A, 255), (0x0A, 0x10, 0x1D, 255)), (0, 0), tile_mask)
d = ImageDraw.Draw(tile)
d.rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), outline=(0x2A, 0x3A, 0x55, 255), width=max(2, S // 64))
d.rounded_rectangle([int(S * 0.04), int(S * 0.04), S - 1 - int(S * 0.04), S - 1 - int(S * 0.04)],
                    radius=int(S * 0.18), outline=(255, 255, 255, 16), width=max(1, S // 128))

ham = Image.new('RGBA', (S, S), (0, 0, 0, 0))
hw = int(S * 0.075)
hx0 = CX - hw // 2
hy0 = CY - int(S * 0.12)
hy1 = CY + int(S * 0.34)
handle_mask = Image.new('L', (S, S), 0)
ImageDraw.Draw(handle_mask).rounded_rectangle([hx0, hy0, hx0 + hw, hy1], radius=hw // 2, fill=255)
ham.paste(vgrad((S, S), (0xF8, 0xFA, 0xFC, 255), (0xCB, 0xD5, 0xE1, 255)), (0, 0), handle_mask)
ImageDraw.Draw(ham).rounded_rectangle(
    [hx0 + hw // 4, hy0 + int(S * 0.02), hx0 + hw // 4 + max(2, hw // 9), hy1 - int(S * 0.03)],
    radius=max(1, hw // 9), fill=(255, 255, 255, 140))

head_x0 = CX - int(S * 0.27)
head_x1 = CX + int(S * 0.27)
head_y0 = CY - int(S * 0.23)
head_y1 = CY - int(S * 0.08)
head_mask = Image.new('L', (S, S), 0)
ImageDraw.Draw(head_mask).rounded_rectangle([head_x0, head_y0, head_x1, head_y1], radius=int(S * 0.05), fill=255)
ham.paste(hgrad((S, S), (0xC2, 0x41, 0x0C, 255), (0xF9, 0x73, 0x16, 255), (0xFD, 0xBA, 0x74, 255)), (0, 0), head_mask)
ImageDraw.Draw(ham).rounded_rectangle(
    [head_x0 + int(S * 0.02), head_y0 + int(S * 0.015), head_x1 - int(S * 0.02), head_y0 + int(S * 0.04)],
    radius=int(S * 0.013), fill=(255, 255, 255, 55))
ImageDraw.Draw(ham).polygon(
    [(head_x0 + int(S * 0.04), head_y1 - int(S * 0.045)),
     (head_x0 + int(S * 0.10), head_y1 - int(S * 0.045)),
     (head_x0 + int(S * 0.07), head_y1 + int(S * 0.004))],
    fill=(0x9A, 0x34, 0x12, 140))
ham = ham.rotate(-45, resample=Image.BICUBIC, center=(CX, CY))
tile.alpha_composite(ham)

sd = ImageDraw.Draw(tile)
sd.ellipse([int(S * 0.72), int(S * 0.16), int(S * 0.86), int(S * 0.30)], fill=(0xF9, 0x73, 0x16, 40))
sd.ellipse([int(S * 0.775), int(S * 0.215), int(S * 0.805), int(S * 0.245)], fill=(0xFD, 0xBA, 0x74, 255))
sd.ellipse([int(S * 0.85), int(S * 0.32), int(S * 0.863), int(S * 0.333)], fill=(0xF9, 0x73, 0x16, 255))
sd.ellipse([int(S * 0.695), int(S * 0.185), int(S * 0.705), int(S * 0.195)], fill=(0xFD, 0xBA, 0x74, 255))

for size in (20, 32, 40, 64, 96, 128):
    tile.resize((size, size), Image.LANCZOS).save(os.path.join(OUT, f'icon_{size}.png'), optimize=True)

base = tile.resize((256, 256), Image.LANCZOS)
ico_sizes = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48),
             (64, 64), (96, 96), (128, 128), (256, 256)]
base.save(os.path.join(OUT, 'Vulcan.ico'), sizes=ico_sizes)
base.save(os.path.join(EMB, 'Vulcan.ico'), sizes=ico_sizes)
tile.resize((512, 512), Image.LANCZOS).save(os.path.join(EMB, 'Vulcan.png'), optimize=True)

print('icons written to', OUT)
