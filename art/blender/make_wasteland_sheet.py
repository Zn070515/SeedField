"""Build a contact sheet for the top-down wasteland asset pack."""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
INPUT = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "game/SeedField.Game/Art/style_topdown"
OUTPUT = Path(sys.argv[2]) if len(sys.argv) > 2 else ROOT / "art/renders/wasteland_pack_v1.png"

BACKGROUND = (20, 16, 12, 255)
CELL = 256
PADDING = 18
COLUMNS = 4
ROWS = 4
TEXT = (226, 214, 195, 255)
MUTED = (154, 140, 120, 255)
FONT_PATH = "C:/Windows/Fonts/consola.ttf"


def font(size: int):
    try:
        return ImageFont.truetype(FONT_PATH, size)
    except OSError:
        return ImageFont.load_default()


def main() -> int:
    names = [
        "waste_core",
        "waste_generator",
        "waste_relay",
        "waste_turret",
        "waste_drill",
        "waste_foundry",
        "waste_hostile",
        "waste_scout",
        "waste_sentry",
        "prop_barrels",
        "prop_sandbags",
        "prop_scrap",
        "ore_copper",
        "ore_lead",
        "ore_titanium",
        "ore_coal",
    ]
    paths = [INPUT / f"{name}_topdown_000.png" for name in names]
    missing = [str(path) for path in paths if not path.exists()]
    if missing:
        print("MISSING " + ", ".join(missing))
        return 1

    width = PADDING + COLUMNS * (CELL + PADDING)
    height = PADDING + 44 + ROWS * (CELL + 30 + PADDING)
    sheet = Image.new("RGBA", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    title = font(24)
    label = font(14)
    draw.text(
        (PADDING, 16),
        "SEEDFIELD / WASTELAND TOP-DOWN PACK v1 / STYLE TEST",
        fill=TEXT,
        font=title,
    )

    for index, (name, path) in enumerate(zip(names, paths)):
        image = Image.open(path).convert("RGBA").resize(
            (CELL, CELL), Image.Resampling.LANCZOS
        )
        column = index % COLUMNS
        row = index // COLUMNS
        x = PADDING + column * (CELL + PADDING)
        y = 60 + row * (CELL + 30 + PADDING)
        # Checker-free dark tile so the transparent silhouette stays readable.
        draw.rounded_rectangle(
            (x - 4, y - 4, x + CELL + 4, y + CELL + 4),
            radius=8,
            fill=(30, 25, 19, 255),
            outline=(62, 52, 40, 255),
        )
        sheet.alpha_composite(image, (x, y))
        draw.text((x, y + CELL + 6), name, fill=MUTED, font=label)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(OUTPUT)
    print(f"WASTELAND_SHEET {OUTPUT} {sheet.width}x{sheet.height}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
