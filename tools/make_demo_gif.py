"""Assemble the README workflow GIF from captured SolidWorks frames.

Usage:
    python tools/make_demo_gif.py --frames <dir> --out docs/assets/vulcan-workflow.gif

Frames are expected as frame_01.png ... frame_NN.png in capture order. Each
frame gets a caption bar drawn at the bottom. Captions can be supplied with
--captions "text1|text2|..." or default to the built-in workflow labels.
"""
import argparse
import glob
import os

from PIL import Image, ImageDraw, ImageFont

DEFAULT_CAPTIONS = [
    "1. 在 SolidWorks 中打开 Vulcan AI 面板",
    "2. 输入自然语言需求",
    "3. 检查并编辑生成的 JSON",
    "4. 执行建模，得到可编辑特征树",
]

FONT_CANDIDATES = [
    r"C:\Windows\Fonts\msyh.ttc",
    r"C:\Windows\Fonts\msyhbd.ttc",
    r"C:\Windows\Fonts\simhei.ttf",
]

BAR_HEIGHT = 46
WIDTH = 960


def load_font(size):
    for path in FONT_CANDIDATES:
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, size)
            except OSError:
                continue
    return ImageFont.load_default()


def build_frame(path, caption, font):
    img = Image.open(path).convert("RGB")
    ratio = WIDTH / img.width
    img = img.resize((WIDTH, max(1, int(img.height * ratio))), Image.LANCZOS)

    canvas = Image.new("RGB", (WIDTH, img.height + BAR_HEIGHT), (15, 23, 42))
    canvas.paste(img, (0, 0))
    draw = ImageDraw.Draw(canvas)
    draw.rectangle([0, img.height, WIDTH, img.height + BAR_HEIGHT], fill=(15, 23, 42))
    draw.rectangle([0, img.height, WIDTH, img.height + 2], fill=(249, 115, 22))
    text_y = img.height + (BAR_HEIGHT - 20) // 2
    draw.text((18, text_y), caption, font=font, fill=(248, 250, 252))
    return canvas


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--frames", required=True, help="directory with frame_*.png")
    parser.add_argument("--out", required=True, help="output GIF path")
    parser.add_argument("--caption-seconds", type=float, default=1.8)
    parser.add_argument("--captions", default="")
    args = parser.parse_args()

    paths = sorted(glob.glob(os.path.join(args.frames, "frame_*.png")))
    if not paths:
        raise SystemExit("no frame_*.png found in " + args.frames)

    captions = args.captions.split("|") if args.captions else DEFAULT_CAPTIONS
    font = load_font(20)
    frames = []
    for index, path in enumerate(paths):
        caption = captions[index] if index < len(captions) else ""
        frames.append(build_frame(path, caption, font))

    duration = int(args.caption_seconds * 1000)
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    frames[0].save(
        args.out,
        save_all=True,
        append_images=frames[1:],
        duration=duration,
        loop=0,
        optimize=True,
        disposal=2,
    )
    print("wrote", args.out, os.path.getsize(args.out), "bytes,", len(frames), "frames")


if __name__ == "__main__":
    main()
