"""Generate seamless topdown terrain tiles + normal maps.

Usage:
  python tools/generate_terrain_tiles.py --output game/SeedField.Game/Art/style_topdown
"""

from __future__ import annotations

import argparse
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw


def lattice(size: int, cells: int, seed: int) -> list[list[float]]:
    rng = random.Random(seed)
    return [[rng.random() for _ in range(cells)] for _ in range(cells)]


def sample(grid: list[list[float]], x: float, y: float) -> float:
    cells = len(grid)
    x0 = int(math.floor(x)) % cells
    y0 = int(math.floor(y)) % cells
    x1 = (x0 + 1) % cells
    y1 = (y0 + 1) % cells
    fx = x - math.floor(x)
    fy = y - math.floor(y)
    fx = fx * fx * (3 - 2 * fx)
    fy = fy * fy * (3 - 2 * fy)
    a = grid[y0][x0] * (1 - fx) + grid[y0][x1] * fx
    b = grid[y1][x0] * (1 - fx) + grid[y1][x1] * fx
    return a * (1 - fy) + b * fy


def height_field(size: int, seed: int) -> list[float]:
    octaves = (
        (4, 0.50),
        (8, 0.28),
        (16, 0.14),
        (32, 0.08),
    )
    grids = [lattice(cells, cells, seed + cells) for cells, _ in octaves]
    height = [0.0] * (size * size)
    for y in range(size):
        for x in range(size):
            value = 0.0
            for (cells, weight), grid in zip(octaves, grids):
                value += weight * sample(grid, x / size * cells, y / size * cells)
            height[y * size + x] = value
    return height


def normal_map(height: list[float], size: int, strength: float = 3.0) -> Image.Image:
    image = Image.new("RGBA", (size, size))
    pixels = image.load()
    for y in range(size):
        for x in range(size):
            left = height[y * size + (x - 1) % size]
            right = height[y * size + (x + 1) % size]
            up = height[((y - 1) % size) * size + x]
            down = height[((y + 1) % size) * size + x]
            nx = (left - right) * strength
            ny = (up - down) * strength
            length = math.sqrt(nx * nx + ny * ny + 1.0)
            pixels[x, y] = (
                int((nx / length * 0.5 + 0.5) * 255),
                int((ny / length * 0.5 + 0.5) * 255),
                int((1.0 / length * 0.5 + 0.5) * 255),
                255,
            )
    return image


def concrete(size: int, seed: int) -> Image.Image:
    height = height_field(size, seed)
    image = Image.new("RGBA", (size, size))
    pixels = image.load()
    for y in range(size):
        for x in range(size):
            h = height[y * size + x]
            tone = 34 + h * 34
            seam = x % 128 < 2 or y % 128 < 2
            if seam:
                tone *= 0.62
            stain = max(0.0, h - 0.72) * 1.5
            pixels[x, y] = (
                int(tone + 6 + stain * 22),
                int(tone + 10 + stain * 14),
                int(tone + 16 + stain * 6),
                255,
            )
    draw = ImageDraw.Draw(image)
    rng = random.Random(seed + 7)
    for _ in range(14):
        x = rng.randrange(size)
        y = rng.randrange(size)
        for _ in range(rng.randrange(18, 48)):
            nx = (x + rng.choice((-2, -1, 0, 1, 2))) % size
            ny = (y + rng.choice((-2, -1, 0, 1, 2))) % size
            draw.line((x, y, nx, ny), fill=(20, 24, 29, 190), width=1)
            x, y = nx, ny
    for _ in range(18):
        x = rng.randrange(size)
        y = rng.randrange(size)
        draw.ellipse((x - 2, y - 2, x + 2, y + 2), fill=(88, 64, 38, 180))
    return image


def road(size: int, seed: int) -> Image.Image:
    height = height_field(size, seed)
    image = Image.new("RGBA", (size, size))
    pixels = image.load()
    for y in range(size):
        for x in range(size):
            h = height[y * size + x]
            tone = 20 + h * 20
            pixels[x, y] = (
                int(tone),
                int(tone + 6),
                int(tone + 12),
                255,
            )
    draw = ImageDraw.Draw(image)
    for x in range(0, size, 96):
        draw.rectangle((x, size // 2 - 3, x + 42, size // 2 + 3), fill=(126, 92, 34, 210))
    return image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--size", type=int, default=512)
    options = parser.parse_args()
    options.output.mkdir(parents=True, exist_ok=True)

    tiles = (
        ("terrain_concrete", concrete(options.size, 71)),
        ("terrain_road", road(options.size, 83)),
    )
    for name, image in tiles:
        image.save(options.output / f"{name}_topdown_000.png")
        normal_map(height_field(options.size, 71 if name == "terrain_concrete" else 83), options.size).save(
            options.output / f"{name}_topdown_000_normal.png"
        )
        print(f"TERRAIN_TILE {name}")


if __name__ == "__main__":
    main()
