#!/usr/bin/env python3
"""Generate the 38-0-20 favicon / app-icon set + an OpenGraph banner.

Brand: near-black pitch (#07100b) + emerald (#34d39a) + amber (#e9b949), the "38-0" wordmark
(an unbeaten 38-game season). App icon = stacked "38" / "-0"; OG image = the full "38-0-20"
wordmark + tagline. Run: python3 tools/make_icons.py
"""

import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
WWW  = os.path.abspath(os.path.join(HERE, "..", "WebClient", "wwwroot"))

BG_TOP   = (10, 27, 18)    # #0a1b12 — faint green-black, top
BG_BOT   = (7, 16, 11)     # #07100b — theme background, bottom
EMERALD  = (52, 211, 154)  # #34d39a
EMERALD_D= (31, 143, 104)  # #1f8f68
AMBER    = (233, 185, 73)  # #e9b949
WHITE    = (255, 255, 255)

BLACK_FONT = "/System/Library/Fonts/Supplemental/Arial Black.ttf"


def _vgrad(size_w, size_h, top, bot):
    base = Image.new("RGB", (size_w, size_h), top)
    px = base.load()
    for y in range(size_h):
        t = y / max(1, size_h - 1)
        r = int(top[0] + (bot[0] - top[0]) * t)
        g = int(top[1] + (bot[1] - top[1]) * t)
        b = int(top[2] + (bot[2] - top[2]) * t)
        for x in range(size_w):
            px[x, y] = (r, g, b)
    return base


def _emerald_glow(w, h, cx, cy, radius, strength=0.5):
    """A soft radial emerald glow layer (RGBA) to composite behind the text."""
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    gp = glow.load()
    for y in range(h):
        for x in range(w):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
            a = max(0.0, 1.0 - d / radius)
            if a > 0:
                gp[x, y] = (EMERALD[0], EMERALD[1], EMERALD[2], int(255 * strength * a * a))
    return glow


def _fit_font(path, text, target_px):
    """Binary-search a font size whose text height ≈ target_px."""
    lo, hi = 4, max(8, target_px * 3)
    best = ImageFont.truetype(path, lo)
    while lo <= hi:
        mid = (lo + hi) // 2
        f = ImageFont.truetype(path, mid)
        bb = f.getbbox(text)
        hgt = bb[3] - bb[1]
        if hgt <= target_px:
            best = f
            lo = mid + 1
        else:
            hi = mid - 1
    return best


def _draw_centered(draw, cx, y_top, text, font, fill):
    bb = font.getbbox(text)
    w = bb[2] - bb[0]
    draw.text((cx - w / 2 - bb[0], y_top - bb[1]), text, font=font, fill=fill)


def make_icon(size):
    """Square app icon: rounded dark tile, emerald glow, stacked '38' / '-0'."""
    scale = 4 if size < 256 else 2     # supersample for crisp small icons
    S = size * scale
    img = _vgrad(S, S, BG_TOP, BG_BOT).convert("RGBA")

    # Emerald glow behind the wordmark.
    img = Image.alpha_composite(img, _emerald_glow(S, S, S * 0.5, S * 0.42, S * 0.62, 0.42))

    draw = ImageDraw.Draw(img)

    # "38" dominant (white), "-0" accent (emerald) beneath — the 38-0 brand.
    f_top = _fit_font(BLACK_FONT, "38", int(S * 0.40))
    f_bot = _fit_font(BLACK_FONT, "-0", int(S * 0.30))
    _draw_centered(draw, S * 0.5, int(S * 0.17), "38", f_top, WHITE)
    _draw_centered(draw, S * 0.5, int(S * 0.55), "-0", f_bot, EMERALD)

    # Rounded-corner mask (iOS-style squircle-ish radius).
    mask = Image.new("L", (S, S), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=255)
    img.putalpha(mask)

    return img.resize((size, size), Image.LANCZOS)


def make_og():
    """1200x630 OpenGraph banner: '38-0-20' wordmark + tagline on the pitch theme."""
    W, H = 1200, 630
    img = _vgrad(W, H, BG_TOP, BG_BOT).convert("RGBA")
    img = Image.alpha_composite(img, _emerald_glow(W, H, W * 0.5, H * 0.40, W * 0.55, 0.30))
    draw = ImageDraw.Draw(img)

    # Wordmark "38-0-20" with the two hyphens in emerald (mirrors the home screen).
    f = _fit_font(BLACK_FONT, "38-0-20", int(H * 0.26))
    parts = [("38", WHITE), ("-", EMERALD), ("0", WHITE), ("-", EMERALD), ("20", WHITE)]
    total = sum(f.getbbox(t)[2] - f.getbbox(t)[0] for t, _ in parts)
    bb = f.getbbox("38-0-20")
    x = (W - total) / 2
    y = H * 0.30
    for t, col in parts:
        tb = f.getbbox(t)
        draw.text((x - tb[0], y - bb[1]), t, font=f, fill=col)
        x += tb[2] - tb[0]

    # Tagline.
    f2 = _fit_font(BLACK_FONT, "x", int(H * 0.052))
    _draw_centered(draw, W * 0.5, int(H * 0.62), "DRAFT YOUR XI. GO UNBEATEN.", f2, (255, 255, 255))
    # Sub-line in emerald.
    f3 = _fit_font(BLACK_FONT, "x", int(H * 0.038))
    _draw_centered(draw, W * 0.5, int(H * 0.73), "20 MANAGERS  ·  38 MATCHDAYS", f3, EMERALD)

    return img.convert("RGB")


def main():
    out = {
        os.path.join(WWW, "favicon.png"):                 make_icon(32),
        os.path.join(WWW, "apple-touch-icon.png"):        make_icon(180),
        os.path.join(WWW, "icons", "apple-touch-icon.png"): make_icon(180),
        os.path.join(WWW, "icons", "icon-192.png"):       make_icon(192),
        os.path.join(WWW, "icons", "icon-512.png"):       make_icon(512),
    }
    for path, im in out.items():
        os.makedirs(os.path.dirname(path), exist_ok=True)
        im.save(path)
        print("wrote", os.path.relpath(path, WWW))

    og_dir = os.path.join(WWW, "images")
    os.makedirs(og_dir, exist_ok=True)
    og_path = os.path.join(og_dir, "og.png")
    make_og().save(og_path)
    print("wrote", os.path.relpath(og_path, WWW))


if __name__ == "__main__":
    main()
