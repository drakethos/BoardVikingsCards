"""Builds icon.png (256x256): a fan of three cards on green felt. Run: python Models/make_icon.py"""
import os
from PIL import Image, ImageDraw, ImageFilter
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S = 256
bg = Image.new("RGBA", (S, S))
d = ImageDraw.Draw(bg)
for y in range(S):  # felt gradient
    t = y / S
    d.line([(0, y), (S, y)], fill=(int(18 + 14 * (1 - t)), int(96 + 34 * (1 - t)), int(58 + 20 * (1 - t)), 255))
mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle((0, 0, S - 1, S - 1), 36, fill=255)
ImageDraw.Draw(bg).rounded_rectangle((8, 8, S - 9, S - 9), 30, outline=(214, 178, 84, 255), width=4)
canvas = bg.copy()
for name, ang, dx in (("SA", 22, 6), ("DQ", -22, -6), ("HK", 0, 0)):
    face = Image.open(os.path.join(root, "Assets", "faces", name + ".png")).convert("RGBA")
    face = face.resize((int(face.width * 0.78), int(face.height * 0.78)), Image.LANCZOS)
    pad = Image.new("RGBA", (face.width + 60, face.height + 60))
    shadow = Image.new("RGBA", pad.size, (0, 0, 0, 0))
    shadow.paste((0, 0, 0, 130), (30, 36), face.split()[3])
    pad = Image.alpha_composite(pad, shadow.filter(ImageFilter.GaussianBlur(5)))
    pad.alpha_composite(face, (30, 30))
    pad = pad.rotate(-ang, resample=Image.BICUBIC, expand=True, center=(pad.width / 2, pad.height * 0.9))
    canvas.alpha_composite(pad, (S // 2 - pad.width // 2 + dx, 28))
out = Image.new("RGBA", (S, S), (0, 0, 0, 0))
out.paste(canvas, (0, 0), mask)
out.save(os.path.join(root, "icon.png"))
