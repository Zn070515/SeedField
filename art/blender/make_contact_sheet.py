"""Build a style-test contact sheet: full views, 128px thumbnails, silhouettes."""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
INPUT = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "art/generated/power_node"
OUTPUT = Path(sys.argv[2]) if len(sys.argv) > 2 else ROOT / "art/renders/power_node_sheet.png"

BACKGROUND = (11, 18, 32, 255)
TEXT = (210, 225, 235, 255)
MUTED = (120, 145, 160, 255)
TITLE = "SEEDFIELD / POWER NODE / STYLE TEST"
FONT_PATH = "C:/Windows/Fonts/consola.ttf"


def font(size: int):
    try:
        return ImageFont.truetype(FONT_PATH, size)
    except OSError:
        return ImageFont.load_default()


def label(draw: ImageDraw.ImageDraw, xy, text: str, size: int, color=TEXT) -> None:
    draw.text(xy, text, fill=color, font=font(size))


def main() -> int:
    names = [f"power_node_{angle:03d}.png" for angle in (0, 90, 180, 270)]
    names.append("power_node_off.png")
    paths = [INPUT / name for name in names]
    missing = [str(path) for path in paths if not path.exists()]
    if missing:
        print("MISSING " + ", ".join(missing))
        return 1

    images = [Image.open(path).convert("RGBA") for path in paths]
    cell = 512
    thumb = 128
    padding = 24
    width = padding + len(images) * (cell + padding)
    full_y = padding + 48
    thumb_y = full_y + cell + 24 + 40
    silhouette_y = thumb_y + thumb + 40
    height = silhouette_y + thumb + padding

    sheet = Image.new("RGBA", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    label(draw, (padding, 16), TITLE, 24)

    for index, (name, image) in enumerate(zip(names, images)):
        x = padding + index * (cell + padding)
        sheet.alpha_composite(image, (x, full_y))
        view = "OFF" if name.endswith("_off.png") else f"{name[11:14]}°"
        label(draw, (x, full_y + cell + 6), f"{view}  {name}", 14, MUTED)

    label(draw, (padding, thumb_y - 26), "READABILITY @ 128px", 14, MUTED)
    for index, image in enumerate(images):
        x = padding + index * (thumb + 32)
        resized = image.resize((thumb, thumb), Image.Resampling.LANCZOS)
        sheet.alpha_composite(resized, (x, thumb_y))

    silhouette_y = thumb_y + thumb + 40
    label(draw, (padding, silhouette_y - 26), "SILHOUETTE @ 128px", 14, MUTED)
    for index, image in enumerate(images):
        x = padding + index * (thumb + 32)
        alpha = image.getchannel("A").resize(
            (thumb, thumb),
            Image.Resampling.LANCZOS,
        )
        silhouette = Image.new("RGBA", (thumb, thumb), (0, 0, 0, 0))
        silhouette.putalpha(alpha)
        shape = Image.new("RGBA", (thumb, thumb), (210, 225, 235, 255))
        shape.putalpha(alpha)
        sheet.alpha_composite(shape, (x, silhouette_y))

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(OUTPUT)
    print(f"CONTACT_SHEET {OUTPUT} {sheet.width}x{sheet.height}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
