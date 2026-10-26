"""Builds the hammer-menu icons for the two card tables (Assets/icons/table_*.png, 128x128). Run: python Models/make_table_icons.py"""
import os
from PIL import Image, ImageDraw, ImageFilter
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S = 4  # draw big, shrink for smooth edges


def table(name, w, h):
    W = 128 * S
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx, cy = W // 2, int(W * 0.46)
    hw, hh = int(w * S), int(h * S)
    top = (cx - hw, cy - hh, cx + hw, cy + hh)
    # shadow + legs
    sh = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse((cx - hw, cy + hh + 18 * S, cx + hw, cy + hh + 42 * S), fill=(0, 0, 0, 110))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(5 * S)))
    for lx in (top[0] + 8 * S, top[2] - 16 * S):
        d.rectangle((lx, top[3], lx + 8 * S, top[3] + 34 * S), fill=(78, 52, 30, 255))
    # rim and felt
    d.rounded_rectangle(top, 14 * S, fill=(110, 74, 40, 255))
    d.rounded_rectangle((top[0] + 6 * S, top[1] + 6 * S, top[2] - 6 * S, top[3] - 6 * S), 10 * S, fill=(26, 112, 66, 255))
    d.rounded_rectangle((top[0] + 11 * S, top[1] + 11 * S, top[2] - 11 * S, top[3] - 11 * S), 8 * S, outline=(214, 178, 84, 255), width=S)
    # cards
    for i, (dx, dy, col, rot) in enumerate(((-0.35, -0.1, (240, 238, 228), -14), (0.0, 0.0, (240, 238, 228), 6), (0.3, 0.08, (160, 36, 40), 20))):
        c = Image.new("RGBA", (22 * S, 30 * S), (0, 0, 0, 0))
        cd = ImageDraw.Draw(c)
        cd.rounded_rectangle((0, 0, 22 * S - 1, 30 * S - 1), 3 * S, fill=col + (255,), outline=(60, 50, 40, 255), width=S // 2 or 1)
        if col[0] > 200:
            cd.ellipse((7 * S, 11 * S, 15 * S, 19 * S), fill=(190, 40, 46, 255))
        c = c.rotate(rot, expand=True, resample=Image.BICUBIC)
        im.alpha_composite(c, (int(cx + dx * hw - c.width / 2), int(cy + dy * hh - c.height / 2)))
    # chips
    for (dx, dy, col) in ((0.62, -0.25, (230, 230, 230)), (0.66, -0.08, (200, 50, 50)), (-0.62, 0.2, (40, 90, 200))):
        x, y = cx + dx * hw, cy + dy * hh
        d.ellipse((x - 7 * S, y - 7 * S, x + 7 * S, y + 7 * S), fill=col + (255,), outline=(255, 255, 255, 255), width=S)
        d.ellipse((x - 3 * S, y - 3 * S, x + 3 * S, y + 3 * S), outline=(255, 255, 255, 255), width=S // 2 or 1)
    im = im.resize((128, 128), Image.LANCZOS)
    os.makedirs(os.path.join(root, "Assets", "icons"), exist_ok=True)
    im.save(os.path.join(root, "Assets", "icons", name + ".png"))


table("table_small", 44, 30)
table("table_long", 56, 28)
