"""
Draw Board Vikings card backs and pack them into Assets/card_backs.png.

The sheet layout matches CardBacks.cs (do not change one without the other):
  512x1024, 3 columns x 5 rows
  origin (2, 2), pitch 140 x 190, tile 136 x 186, corner radius 12

Each back is a printed card: white edge, solid border, tone-on-tone pattern,
and a center mark (diamond, ring, or triangle). Geometry only — no imported artwork.

  python scripts/make_card_backs.py
"""
from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets" / "card_backs.png"

SHEET_W, SHEET_H = 512, 1024
X0, Y0 = 2, 2
PITCH_X, PITCH_Y = 140, 190
TILE_W, TILE_H = 136, 186
CORNER = 12
SCALE = 4

# Drawn large, then reduced, so the lines stay even when a giant card scales them up.
TW, TH = TILE_W * SCALE, TILE_H * SCALE
WHITE = 5 * SCALE
BAND = 7 * SCALE
RULE = SCALE  # the light line inside the border


def _rounded(h: int, w: int, inset: int, radius: int) -> np.ndarray:
    """Inside a rounded rectangle. radius is the outer corner; it shrinks with inset."""
    r = max(1, radius - inset)
    x0, y0 = inset, inset
    x1, y1 = w - inset, h - inset
    ys, xs = np.ogrid[:h, :w]
    inside = (xs >= x0) & (xs < x1) & (ys >= y0) & (ys < y1)
    ix = xs - x0
    iy = ys - y0
    iw, ih = x1 - x0, y1 - y0
    cx = np.where(ix < r, r, np.where(ix >= iw - r, iw - 1 - r, ix))
    cy = np.where(iy < r, r, np.where(iy >= ih - r, ih - 1 - r, iy))
    return inside & ((ix - cx) ** 2 + (iy - cy) ** 2 <= r * r)


def _ellipse(h: int, w: int, cx: float, cy: float, rx: float, ry: float) -> np.ndarray:
    ys, xs = np.ogrid[:h, :w]
    return ((xs - cx) / rx) ** 2 + ((ys - cy) / ry) ** 2 <= 1


def _paint(img: np.ndarray, color: tuple[int, int, int], mask: np.ndarray) -> None:
    img[mask, 0] = color[0]
    img[mask, 1] = color[1]
    img[mask, 2] = color[2]
    img[mask, 3] = 255


def _lattice(h: int, w: int, step: int, thick: int) -> np.ndarray:
    ys, xs = np.ogrid[:h, :w]
    return ((xs + ys) % step < thick) | ((xs - ys) % step < thick)


def _chevrons(h: int, w: int, step: int, thick: int) -> np.ndarray:
    ys, xs = np.ogrid[:h, :w]
    band = (ys // step) % 2
    zig = np.where(band == 0, (xs + ys) % step, (xs - ys) % step)
    return zig < thick


def _waves(h: int, w: int, step: int, thick: int) -> np.ndarray:
    ys, xs = np.ogrid[:h, :w]
    phase = np.sin(xs * (2 * np.pi) / (step * 2.2)) * (step * 0.28)
    rel = (ys - phase) % step
    dist = np.minimum(rel, step - rel)
    return dist < thick


def _scales(h: int, w: int, step: int, thick: int) -> np.ndarray:
    ys, xs = np.ogrid[:h, :w]
    row = ys // step
    offset = (row % 2) * (step // 2)
    lx = (xs + offset) % step - step / 2
    ly = ys % step - step * 0.15
    rad = step * 0.62
    outer = lx ** 2 + ly ** 2
    inner = (rad - thick) ** 2
    return (outer <= rad ** 2) & (outer >= inner) & (ly < step * 0.55)


def _basket(h: int, w: int, step: int, thick: int) -> np.ndarray:
    ys, xs = np.ogrid[:h, :w]
    row = ys // step
    vertical = ((xs + (row % 2) * (step // 2)) % step) < thick
    horizontal = (ys % step) < thick
    return vertical | horizontal


def _coins(h: int, w: int, step: int, thick: int) -> np.ndarray:
    ys, xs = np.ogrid[:h, :w]
    lx = (xs % step) - step / 2
    ly = (ys % step) - step / 2
    outer = (step * 0.30) ** 2
    inner = (step * 0.30 - thick) ** 2
    d = lx ** 2 + ly ** 2
    return (d <= outer) & (d >= inner)


def _dots(h: int, w: int, step: int, rad: int) -> np.ndarray:
    ys, xs = np.ogrid[:h, :w]
    lx = (xs % step) - step / 2
    ly = (ys % step) - step / 2
    return lx ** 2 + ly ** 2 <= rad ** 2


def _diamond(h: int, w: int, cx: float, cy: float, rx: float, ry: float, stroke: float) -> np.ndarray:
    ys, xs = np.ogrid[:h, :w]
    d = np.abs(xs - cx) / rx + np.abs(ys - cy) / ry
    inner = np.abs(xs - cx) / (rx - stroke) + np.abs(ys - cy) / (ry - stroke)
    return (d <= 1) & (inner >= 1)


def _triangle(h: int, w: int, cx: float, cy: float, size: float, stroke: float) -> np.ndarray:
    """Equilateral triangle, point up, stroked."""
    hgt = size * 0.866
    pts = (
        (cx, cy - hgt * 0.66),
        (cx - size / 2, cy + hgt * 0.34),
        (cx + size / 2, cy + hgt * 0.34),
    )
    ys, xs = np.ogrid[:h, :w]
    dist = np.minimum(
        _seg(xs, ys, pts[0], pts[1]),
        np.minimum(_seg(xs, ys, pts[1], pts[2]), _seg(xs, ys, pts[2], pts[0])),
    )
    return dist <= stroke


def _seg(xs: np.ndarray, ys: np.ndarray, a: tuple[float, float], b: tuple[float, float]) -> np.ndarray:
    ax, ay = a
    bx, by = b
    abx, aby = bx - ax, by - ay
    t = ((xs - ax) * abx + (ys - ay) * aby) / (abx * abx + aby * aby + 1e-6)
    t = np.clip(t, 0, 1)
    dx = xs - (ax + t * abx)
    dy = ys - (ay + t * aby)
    return np.sqrt(dx * dx + dy * dy)


PATTERNS = {
    "lattice": _lattice,
    "chevron": _chevrons,
    "wave": _waves,
    "scale": _scales,
    "basket": _basket,
    "coin": _coins,
    "dot": _dots,
}


# name, field, pattern ink, seal color, pattern, step (at 4x), mark
# step is the repeat. mark is the seal: diamond, triangle, circle, or none.
BACKS: list[tuple] = [
    ("Crimson Knot", (142, 42, 46), (92, 22, 26), (230, 196, 110), "lattice", 36, "triangle"),
    ("Pine Braid", (36, 92, 58), (18, 54, 34), (230, 196, 110), "chevron", 28, "diamond"),
    ("Frost", (62, 106, 134), (30, 60, 82), (236, 242, 244), "lattice", 32, "diamond"),
    ("Banner", (154, 46, 42), (96, 24, 24), (230, 196, 110), "lattice", 28, "diamond"),
    ("Mead", (168, 118, 48), (110, 72, 24), (255, 236, 196), "coin", 52, "circle"),
    ("Iron", (62, 70, 80), (34, 38, 46), (198, 206, 214), "lattice", 24, "diamond"),
    ("Leather", (122, 74, 44), (74, 42, 24), (230, 196, 110), "dot", 40, "diamond"),
    ("Sea", (30, 92, 98), (14, 52, 56), (214, 232, 228), "wave", 36, "circle"),
    ("Bone", (196, 176, 142), (122, 98, 70), (74, 56, 40), "lattice", 40, "diamond"),
    ("Ember", (184, 68, 36), (122, 36, 18), (240, 196, 130), "scale", 34, "circle"),
    ("Moss", (74, 106, 50), (42, 64, 28), (228, 214, 160), "basket", 32, "diamond"),
    ("Night", (28, 30, 38), (230, 196, 110), (230, 196, 110), "dot", 44, "circle"),
    ("Wolf", (106, 110, 116), (62, 66, 72), (232, 228, 220), "chevron", 30, "diamond"),
    ("Copper", (164, 90, 58), (108, 50, 28), (240, 208, 168), "coin", 48, "circle"),
    ("Stone", (122, 114, 104), (74, 68, 60), (232, 224, 208), "dot", 36, "circle"),
]


def _seal_mark(kind: str, h: int, w: int, cx: float, cy: float, color_mask_size: float) -> np.ndarray:
    if kind == "triangle":
        return _triangle(h, w, cx, cy + 4, color_mask_size, SCALE * 1.6)
    if kind == "diamond":
        return _diamond(h, w, cx, cy, color_mask_size * 0.42, color_mask_size * 0.55, SCALE * 1.7)
    if kind == "circle":
        outer = _ellipse(h, w, cx, cy, color_mask_size * 0.28, color_mask_size * 0.28)
        inner = _ellipse(h, w, cx, cy, color_mask_size * 0.14, color_mask_size * 0.14)
        return outer & ~inner
    return np.zeros((h, w), dtype=bool)


def _shield_points(cx: float, cy: float, scale: float = 1.0) -> list[tuple[float, float]]:
    """Heater shield: wide flat top, short sides, then a taper to a point."""
    hw = 72 * scale
    top = cy - 96 * scale
    corner = 16 * scale
    side_y = cy - 28 * scale
    point = cy + 108 * scale
    pts: list[tuple[float, float]] = []

    # Top-left corner, then the flat top, then the top-right corner.
    for i in range(9):
        ang = np.pi - i / 8 * (np.pi / 2)
        pts.append((cx - hw + corner + np.cos(ang) * corner, top + corner - np.sin(ang) * corner))
    pts.append((cx + hw - corner, top))
    for i in range(1, 9):
        ang = np.pi / 2 - i / 8 * (np.pi / 2)
        pts.append((cx + hw - corner + np.cos(ang) * corner, top + corner - np.sin(ang) * corner))

    drop = point - side_y
    pts.extend(
        (
            (cx + hw, side_y),
            (cx + hw * 0.55, side_y + drop * 0.38),
            (cx + hw * 0.18, side_y + drop * 0.74),
            (cx, point),
            (cx - hw * 0.18, side_y + drop * 0.74),
            (cx - hw * 0.55, side_y + drop * 0.38),
            (cx - hw, side_y),
            (cx - hw, top + corner),
        )
    )
    return pts


def _fill_polygon(h: int, w: int, pts: list[tuple[float, float]]) -> np.ndarray:
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).polygon(pts, fill=255)
    return np.array(mask) > 0


def _shield(h: int, w: int, cx: float, cy: float, scale: float = 1.0) -> np.ndarray:
    return _fill_polygon(h, w, _shield_points(cx, cy, scale))


def draw_back(spec: tuple, frame: str = "oval") -> Image.Image:
    _name, field, ink, seal, pattern, step, mark = spec
    img = np.zeros((TH, TW, 4), dtype=np.uint8)
    outer_r = CORNER * SCALE

    card = _rounded(TH, TW, 0, outer_r)
    _paint(img, (255, 255, 255), card)

    band = _rounded(TH, TW, WHITE, outer_r)
    _paint(img, ink, band)

    field_at = WHITE + BAND
    body = _rounded(TH, TW, field_at, outer_r)
    _paint(img, field, body)

    # Thin light rule between the border and the pattern.
    rule_outer = _rounded(TH, TW, field_at - RULE, outer_r)
    _paint(img, seal, rule_outer & ~body)

    cx, cy = (TW - 1) / 2, (TH - 1) / 2
    # A little lighter than the field so a framed seal reads, still the same ink family.
    paper = tuple(min(255, int(c + (255 - c) * 0.22)) for c in field)
    if field[0] > 170:
        paper = tuple(min(255, c + 18) for c in field)

    if frame == "shield":
        seal_fill = _shield(TH, TW, cx, cy + 6, 1.0)
        seal_inner = _shield(TH, TW, cx, cy + 6, 0.84)
    elif frame == "mark":
        seal_fill = np.zeros((TH, TW), dtype=bool)
        seal_inner = seal_fill
    else:
        rx, ry = 92, 118
        seal_fill = _ellipse(TH, TW, cx, cy, rx, ry)
        seal_inner = _ellipse(TH, TW, cx, cy, rx - 3 * SCALE, ry - 3 * SCALE)

    pat = PATTERNS[pattern](TH, TW, step, max(3, SCALE))
    _paint(img, ink, body & pat & ~seal_fill)

    if frame == "mark":
        # The shape itself, in the seal color, big enough to read without a frame around it.
        size = 210 if mark == "circle" else 156
        _paint(img, seal, body & _seal_mark(mark, TH, TW, cx, cy, size))
    else:
        _paint(img, paper, body & seal_fill)
        _paint(img, seal, body & seal_fill & ~seal_inner)
        mark_cy = cy - 18 if frame == "shield" else cy
        _paint(img, ink, body & _seal_mark(mark, TH, TW, cx, mark_cy, 70 if frame == "shield" else 78))

    big = Image.fromarray(img, "RGBA").resize((TILE_W, TILE_H), Image.Resampling.LANCZOS)
    out = np.array(big)
    keep = _rounded(TILE_H, TILE_W, 0, CORNER)
    out[~keep, 3] = 0
    out[~keep, :3] = 0
    return Image.fromarray(out, "RGBA")


def _write_sheet(path: Path, frames: dict[str, str] | None = None) -> None:
    if len(BACKS) != 15:
        raise SystemExit(f"Expected 15 backs, got {len(BACKS)}")
    sheet = Image.new("RGBA", (SHEET_W, SHEET_H), (0, 0, 0, 0))
    for i, spec in enumerate(BACKS):
        col, row = i % 3, i // 3
        frame = "mark" if frames is None else frames[spec[0]]
        tile = draw_back(spec, frame)
        sheet.paste(tile, (X0 + col * PITCH_X, Y0 + row * PITCH_Y), tile)
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(path, format="PNG", compress_level=3)
    print(f"Wrote {path} ({SHEET_W}x{SHEET_H})")


def main() -> None:
    _write_sheet(OUT)


if __name__ == "__main__":
    main()
