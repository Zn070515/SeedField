"""Build a review sheet for one asset brief render.

Usage:
  python art/workflow/make_review_sheet.py \
      --input art/workflow/out/waste_shield_generator \
      --asset waste_shield_generator \
      --output art/workflow/out/waste_shield_generator/review_sheet.png
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
    parser.add_argument("--input", required=True)
    parser.add_argument("--asset", required=True)
    parser.add_argument("--output", default="")
    parser.add_argument("--title", default="")
    args = parser.parse_args()

    input_dir = Path(args.input).resolve()
    main_image = input_dir / f"{args.asset}_topdown_000.png"
    review_images = sorted((input_dir / "review").glob(f"{args.asset}_angle_*.png"))
    paths = ([main_image] if main_image.exists() else []) + review_images
    if not paths:
        print(f"MISSING {main_image}")
        return 1

    images: list[tuple[str, Image.Image]] = []
    for path in paths:
        angle = "000" if path == main_image else path.stem.rsplit("_", 1)[-1]
        images.append((angle, Image.open(path).convert("RGBA")))

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
    title = args.title or f"SEEDFIELD / {args.asset.upper()} / STYLE REVIEW"
    label(draw, (padding, 16), title, 24)

    for index, (angle, image) in enumerate(images):
        x = padding + index * (cell + padding)
        sheet.alpha_composite(image, (x, full_y))
        view = "TOPDOWN 90°" if angle == "000" else f"{int(angle):03d}°"
        label(draw, (x, full_y + cell + 8), f"{view}  {image.width}px", 14, MUTED)

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

    output = Path(args.output).resolve() if args.output else input_dir / "review_sheet.png"
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(output)
    print(f"REVIEW_SHEET {output} {sheet.width}x{sheet.height}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
