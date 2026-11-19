"""Composite sprites on real terrain at in-game scales.

Usage:
  python art/workflow/make_context_sheet.py \
      --terrain game/SeedField.Game/Art/style_topdown/terrain_dirt_topdown_000.png \
      --entries "GENERATOR=...png" "SHIELD=...png" \
      --output art/workflow/out/context_review.png
"""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

BACKGROUND = (11, 18, 32, 255)
TEXT = (210, 225, 235, 255)
MUTED = (150, 170, 185, 255)
FONT_PATH = "C:/Windows/Fonts/consola.ttf"


def font(size: int):
    try:
        return ImageFont.truetype(FONT_PATH, size)
    except OSError:
        return ImageFont.load_default()


def label(draw: ImageDraw.ImageDraw, xy, text: str, size: int, color=TEXT) -> None:
    draw.text(xy, text, fill=color, font=font(size))


def shadow(size: int) -> Image.Image:
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    margin = size // 6
    draw.ellipse(
        (margin, size - margin * 2, size - margin, size - margin // 2),
        fill=(0, 0, 0, 120),
    )
    return layer.filter(ImageFilter.GaussianBlur(size // 24))


def paste_sprite(canvas: Image.Image, sprite: Image.Image, size: int, xy: tuple[int, int]) -> None:
    resized = sprite.resize((size, size), Image.Resampling.LANCZOS)
    sx, sy = xy
    canvas.alpha_composite(shadow(size), (sx, sy))
    canvas.alpha_composite(resized, (sx, sy))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--terrain", required=True)
    parser.add_argument("--entries", nargs="+", required=True, help="LABEL=path.png")
    parser.add_argument("--output", required=True)
    parser.add_argument("--title", default="SEEDFIELD / IN-WORLD SCALE CHECK")
    args = parser.parse_args()

    terrain = Image.open(args.terrain).convert("RGBA")
    width, height = 1200, 620
    canvas = Image.new("RGBA", (width, height), BACKGROUND)
    for y in range(0, height, terrain.height):
        for x in range(0, width, terrain.width):
            canvas.alpha_composite(terrain, (x, y))

    draw = ImageDraw.Draw(canvas)
    label(draw, (24, 18), args.title, 22)

    entries = []
    for item in args.entries:
        name, path = item.split("=", 1)
        entries.append((name, Image.open(path).convert("RGBA")))

    column = width // len(entries)
    for index, (name, sprite) in enumerate(entries):
        x = index * column
        label(draw, (x + 24, 62), name, 18, TEXT)
        paste_sprite(canvas, sprite, 256, (x + 24, 96))
        paste_sprite(canvas, sprite, 128, (x + 24, 376))
        paste_sprite(canvas, sprite, 64, (x + 180, 408))
        label(draw, (x + 24, 348), "256px", 13, MUTED)
        label(draw, (x + 24, 512), "128px", 13, MUTED)
        label(draw, (x + 180, 480), "64px", 13, MUTED)

        # a little in-world clutter so the scale reads
        for dx, dy, size in ((210, 120, 32), (250, 160, 24), (196, 300, 40)):
            sx = x + dx
            sy = dy
            if sx + size < width:
                paste_sprite(canvas, sprite, size, (sx, sy))

    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(output)
    print(f"CONTEXT_SHEET {output} {canvas.width}x{canvas.height}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
