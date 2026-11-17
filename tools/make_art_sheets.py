"""Build the A/B review sheets from runtime assets and real Godot screenshots."""

from __future__ import annotations

from pathlib import Path
import json

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "game" / "SeedField.Game" / "Art"
SHOTS = ROOT / "game" / "SeedField.Game" / "screenshots"
OUT = ROOT / "art" / "renders"
BACKGROUND = (10, 17, 29, 255)
PANEL = (17, 27, 43, 255)
TEXT = (214, 228, 238, 255)
MUTED = (128, 152, 168, 255)
ACCENT = (67, 217, 232, 255)
WARM = (232, 184, 92, 255)
FONT = "C:/Windows/Fonts/consola.ttf"


def font(size: int):
    try:
        return ImageFont.truetype(FONT, size)
    except OSError:
        return ImageFont.load_default()


def label(draw, xy, text: str, size: int, color=TEXT) -> None:
    draw.text(xy, text, fill=color, font=font(size))


def asset_path(route: str, name: str) -> Path:
    return ART / f"style_{route}" / f"{name}_{route}_000.png"


def content(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    box = image.getchannel("A").getbbox()
    return image.crop(box) if box else image


def fitted(image: Image.Image, size: int) -> Image.Image:
    cropped = content(image)
    scale = min(size / cropped.width, size / cropped.height)
    resized = cropped.resize(
        (max(1, round(cropped.width * scale)), max(1, round(cropped.height * scale))),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.alpha_composite(
        resized,
        ((size - resized.width) // 2, (size - resized.height) // 2),
    )
    return canvas


def silhouette(image: Image.Image, size: int) -> Image.Image:
    alpha = fitted(image, size).getchannel("A")
    shape = Image.new("RGBA", (size, size), (214, 228, 238, 255))
    shape.putalpha(alpha)
    return shape


def layers_sheet() -> None:
    names = (
        ("seed_core", "SEED CORE"),
        ("power_node", "POWER NODE"),
        ("worker_frame", "WORKER FRAME"),
    )
    routes = (("oblique", "A / OBLIQUE 35°"), ("topdown", "B / TOPDOWN 90°"))
    cell_w, cell_h = 430, 370
    padding = 28
    width = padding * 2 + len(routes) * cell_w
    height = 112 + len(names) * cell_h + padding
    sheet = Image.new("RGBA", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    label(draw, (padding, 22), "SEEDFIELD / A-B CAMERA / RUNTIME SPRITES", 24)
    label(
        draw,
        (padding, 56),
        "Blender 5.2 procedural assets, 512px RGBA, alpha bbox checked by tests",
        13,
        MUTED,
    )
    for column, (route, title) in enumerate(routes):
        x = padding + column * cell_w
        label(draw, (x + 14, 84), title, 15, ACCENT if column == 0 else WARM)

    for row, (name, title) in enumerate(names):
        y = 112 + row * cell_h
        for column, (route, _) in enumerate(routes):
            x = padding + column * cell_w
            panel = (x + 6, y + 6, x + cell_w - 6, y + cell_h - 6)
            draw.rounded_rectangle(panel, radius=10, fill=PANEL)
            image = Image.open(asset_path(route, name))
            preview = fitted(image, 260)
            sheet.alpha_composite(preview, (x + 22, y + 54))
            sheet.alpha_composite(silhouette(image, 64), (x + 318, y + 74))
            label(draw, (x + 24, y + 22), title, 14, TEXT)
            label(draw, (x + 318, y + 150), "SIL", 10, MUTED)
            label(draw, (x + 24, y + 322), f"{name} · {route}", 11, MUTED)

    OUT.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(OUT / "camera-ab-layers.png")
    print(f"SHEET {OUT / 'camera-ab-layers.png'} {sheet.width}x{sheet.height}")


def zoom_sheet() -> None:
    routes = (("oblique", "A / OBLIQUE 35°"), ("topdown", "B / TOPDOWN 90°"))
    zooms = (("far", "FAR 0.62x"), ("mid", "MID 1.00x"), ("near", "NEAR 1.65x"))
    thumb_w, thumb_h = 480, 270
    cell_w, cell_h = 512, 332
    padding = 28
    width = padding * 2 + len(zooms) * cell_w
    height = 112 + len(routes) * cell_h + padding
    sheet = Image.new("RGBA", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    label(draw, (padding, 22), "SEEDFIELD / WORLD-SPACE CAMERA / REAL GODOT", 24)
    label(
        draw,
        (padding, 56),
        "One Camera2D, same world; terrain and devices scale together",
        13,
        MUTED,
    )
    for column, (_, title) in enumerate(zooms):
        label(draw, (padding + column * cell_w + 14, 84), title, 15, MUTED)

    for row, (route, route_title) in enumerate(routes):
        y = 112 + row * cell_h
        for column, (zoom, _) in enumerate(zooms):
            x = padding + column * cell_w
            panel = (x + 6, y + 6, x + cell_w - 6, y + cell_h - 6)
            draw.rounded_rectangle(panel, radius=10, fill=PANEL)
            name = f"art-{route}-{zoom}-default.png"
            image = Image.open(SHOTS / name).convert("RGB")
            thumb = image.resize((thumb_w, thumb_h), Image.Resampling.LANCZOS)
            sheet.paste(thumb, (x + 16, y + 44))
            label(
                draw,
                (x + 16, y + 322),
                f"{route_title} · {name}",
                10,
                ACCENT if route == "oblique" else WARM,
            )

    OUT.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(OUT / "camera-ab-zoom.png")
    print(f"SHEET {OUT / 'camera-ab-zoom.png'} {sheet.width}x{sheet.height}")


def direction_sheet() -> None:
    report_path = ROOT / "art/generated/style_oblique/render-report-oblique.json"
    seconds = None
    if report_path.exists():
        report = json.loads(report_path.read_text(encoding="utf-8"))
        probe = next(
            (asset for asset in report["assets"] if asset["name"] == "direction_probe"),
            None,
        )
        seconds = probe["seconds"] if probe else None

    frames = sorted(
        (path for path in (ART / "style_oblique").glob("direction_probe_oblique_*.png")),
        key=lambda path: path.name,
    )
    subsets = (("4 directions", frames[::4]), ("8 directions", frames[::2]), ("16 directions", frames))
    thumb = 74
    padding = 28
    width = padding * 2 + 16 * (thumb + 6)
    height = 112 + len(subsets) * 126 + padding
    sheet = Image.new("RGBA", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    label(draw, (padding, 22), "SEEDFIELD / DIRECTION PROBE / 4 vs 8 vs 16", 24)
    label(
        draw,
        (padding, 56),
        "asymmetric drill frame · 512px"
        + (f" · {seconds}s for 16 frames" if seconds is not None else ""),
        13,
        MUTED,
    )
    for row, (title, chosen) in enumerate(subsets):
        y = 112 + row * 126
        label(draw, (padding, y), f"{title} · {len(chosen)} frames", 13, TEXT)
        total_bytes = sum(path.stat().st_size for path in chosen)
        label(
            draw,
            (padding + 260, y),
            f"{total_bytes / 1024:.0f} KB",
            12,
            MUTED,
        )
        for index, path in enumerate(chosen):
            image = fitted(Image.open(path), thumb)
            x = padding + index * (thumb + 6)
            sheet.alpha_composite(image, (x, y + 22))
            label(draw, (x, y + 100), path.stem.split("_")[-1], 9, MUTED)

    OUT.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(OUT / "direction-4-8-16.png")
    print(f"SHEET {OUT / 'direction-4-8-16.png'} {sheet.width}x{sheet.height}")


def core_states_sheet() -> None:
    states = (
        ("seed_core", "IDLE"),
        ("seed_core_regenerating", "REGENERATING"),
        ("seed_core_bootstrap", "BOOTSTRAP"),
        ("seed_core_besieged", "BESIEGED"),
    )
    cell = 300
    padding = 28
    width = padding * 2 + len(states) * 320
    height = 112 + cell + 70
    sheet = Image.new("RGBA", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    label(draw, (padding, 22), "SEEDFIELD / CORE STATES / RUNTIME SPRITES", 24)
    label(draw, (padding, 56), "idle · regenerating · bootstrap · besieged", 13, MUTED)
    for index, (name, title) in enumerate(states):
        x = padding + index * 320
        image = Image.open(asset_path("oblique", name))
        sheet.alpha_composite(fitted(image, cell), (x, 120))
        sheet.alpha_composite(silhouette(image, 70), (x + 220, 350))
        label(draw, (x, 96), title, 14, TEXT)
        label(draw, (x, 430), name, 10, MUTED)

    OUT.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(OUT / "core-states.png")
    print(f"SHEET {OUT / 'core-states.png'} {sheet.width}x{sheet.height}")


def module_sheet() -> None:
    modules = (
        ("worker_frame", "EMPTY SOCKET"),
        ("worker_frame_drill", "DRILL MODULE"),
        ("worker_frame_sensor", "SENSOR MODULE"),
    )
    cell = 280
    padding = 28
    width = padding * 2 + len(modules) * 300
    height = 112 + cell + 70
    sheet = Image.new("RGBA", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    label(draw, (padding, 22), "SEEDFIELD / MODULE SWAP / RUNTIME SPRITES", 24)
    label(draw, (padding, 56), "same frame, different module silhouette", 13, MUTED)
    for index, (name, title) in enumerate(modules):
        x = padding + index * 300
        image = Image.open(asset_path("oblique", name))
        sheet.alpha_composite(fitted(image, cell), (x, 120))
        sheet.alpha_composite(silhouette(image, 64), (x + 206, 350))
        label(draw, (x, 96), title, 14, TEXT)
        label(draw, (x, 410), name, 10, MUTED)

    OUT.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(OUT / "module-swap.png")
    print(f"SHEET {OUT / 'module-swap.png'} {sheet.width}x{sheet.height}")


def mask_sheet() -> None:
    assets = (
        ("worker_frame_sensor", "SENSOR"),
        ("seed_core", "CORE"),
    )
    channels = (("", "DIFFUSE"), ("_normal", "NORMAL"), ("_emissive", "EMISSIVE"))
    cell = 240
    padding = 28
    width = padding * 2 + len(channels) * 260
    height = 112 + len(assets) * 300
    sheet = Image.new("RGBA", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    label(draw, (padding, 22), "SEEDFIELD / MASK PASSES / BLENDER 5.2", 24)
    label(draw, (padding, 56), "camera-space normal + emissive only", 13, MUTED)
    for row, (name, title) in enumerate(assets):
        y = 112 + row * 300
        label(draw, (padding, y), title, 13, TEXT)
        for column, (suffix, channel) in enumerate(channels):
            x = padding + column * 260
            path = ART / "style_oblique" / f"{name}_oblique_000{suffix}.png"
            sheet.alpha_composite(fitted(Image.open(path), cell), (x, y + 28))
            label(draw, (x, y + 276), channel, 10, MUTED)

    OUT.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(OUT / "mask-sheet.png")
    print(f"SHEET {OUT / 'mask-sheet.png'} {sheet.width}x{sheet.height}")


def main() -> None:
    layers_sheet()
    zoom_sheet()
    direction_sheet()
    core_states_sheet()
    module_sheet()
    mask_sheet()


if __name__ == "__main__":
    main()
