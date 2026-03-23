#!/usr/bin/env python3
"""Rebuild installer splash GIF from site icon: pulse animation + black→transparent."""
from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageEnhance

REPO = Path(__file__).resolve().parents[1]
SRC = REPO / "app/assets/img/icons/android-chrome-512x512.png"
OUT_INSTALLER = REPO / "installer/QwertyStock.Bootstrapper/Assets/QS_LOGO.gif"
OUT_APP = REPO / "app/installer/QS_LOGO.gif"


def key_black_transparent(img: Image.Image, threshold: int = 38) -> Image.Image:
    rgba = img.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if r <= threshold and g <= threshold and b <= threshold:
                px[x, y] = (0, 0, 0, 0)
    return rgba


def main() -> None:
    base = Image.open(SRC).convert("RGBA")
    base = base.resize((220, 220), Image.Resampling.LANCZOS)
    base = key_black_transparent(base)

    n = 24
    frames: list[Image.Image] = []
    for i in range(n):
        t = (math.sin(i / n * 2 * math.pi) + 1) / 2
        br = 0.94 + 0.12 * t
        fr = ImageEnhance.Brightness(base).enhance(br)
        frames.append(key_black_transparent(fr))

    for path in (OUT_INSTALLER, OUT_APP):
        path.parent.mkdir(parents=True, exist_ok=True)
        frames[0].save(
            path,
            save_all=True,
            append_images=frames[1:],
            duration=70,
            loop=0,
            disposal=2,
            optimize=False,
        )
        print(path, path.stat().st_size, "bytes")


if __name__ == "__main__":
    main()
