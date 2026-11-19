"""Compare several asset PNGs in one family review sheet.

Usage:
  python art/workflow/make_family_sheet.py \
      --images a.png b.png c.png --labels "A" "B" "C" \
      --output art/workflow/out/family_review.png --title "..."
"""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

BACKGROUND = (11, 18, 32, 255)
TEXT = (210, 225, 235, 255)
MUTED = (120, 145, 160, 255)
FONT_PATH = "C:/Windows/Fonts/consola.ttf"


def font(size: int):
    try:
        return ImageFont.truetype(FONT_PATH, size)
    except OSError:
        return ImageFont.load_default()


def label(draw: ImageDraw.ImageDraw, xy, text: str, size: int, color=TEXT) -> None:
    draw.text(xy, text, fill=color, font=font(size))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--images", nargs="+", required=True)
    parser.add_argument("--labels", nargs="+", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--title", default="SEEDFIELD / ASSET FAMILY / STYLE REVIEW")
    args = parser.parse_args()

    if len(args.images) != len(args.labels):
        raise SystemExit("--images and --labels must have the same length")

    images = []
    for path, name in zip(args.images, args.labels):
        image = Image.open(Path(path)).convert("RGBA")
        images.append((name, image))

    cell = max(image.width for _, image in images)
    thumb = 128
    padding = 24
    width = padding + len(images) * (cell + padding)
    full_y = padding + 48
    thumb_y = full_y + cell + 52
    silhouette_y = thumb_y + thumb + 52
    height = silhouette_y + thumb + padding

    sheet = Image.new("RGBA", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    label(draw, (padding, 16), args.title, 24)

    for index, (name, image) in enumerate(images):
        x = padding + index * (cell + padding)
        sheet.alpha_composite(image, (x, full_y))
        label(draw, (x, full_y + cell + 8), f"{name}  {image.width}px", 14, MUTED)

    label(draw, (padding, thumb_y - 26), "READABILITY @ 128px", 14, MUTED)
    for index, (_, image) in enumerate(images):
        x = padding + index * (thumb + 32)
        resized = image.resize((thumb, thumb), Image.Resampling.LANCZOS)
        sheet.alpha_composite(resized, (x, thumb_y))

    label(draw, (padding, silhouette_y - 26), "SILHOUETTE @ 128px", 14, MUTED)
    for index, (_, image) in enumerate(images):
        x = padding + index * (thumb + 32)
        alpha = image.getchannel("A").resize((thumb, thumb), Image.Resampling.LANCZOS)
        shape = Image.new("RGBA", (thumb, thumb), (210, 225, 235, 255))
        shape.putalpha(alpha)
        sheet.alpha_composite(shape, (x, silhouette_y))

    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(output)
    print(f"FAMILY_SHEET {output} {sheet.width}x{sheet.height}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
