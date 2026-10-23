"""Draws the Squeaker icon and logo: a mouse peeking out of its mouse hole.

Run from anywhere:  python branding/make_branding.py
Writes the app icon and a grey "paused" one (.ico, every size Windows asks for) into the
app's Assets, plus a big PNG and light/dark logos next to this script.

Small sizes get their own chunkier drawing; the whiskers and highlights turn to mush below ~32px.
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ASSETS = HERE.parent / "src" / "Squeaker" / "Assets"

# Palette
TILE = (0, 120, 212)          # Windows blue, same as <div>
TILE_PAUSED = (118, 118, 118)
HOLE = (12, 34, 64)           # the dark of the mouse hole
FUR = (226, 230, 236)
EAR = (246, 168, 186)         # inside the ears, and the nose
EYE = (20, 20, 24)
WHISKER = (150, 158, 170)

SS = 4  # supersampling factor


def tile(img, s, detailed, colour):
    """A blue tile with a mouse hole in it, and a mouse peeking out from the dark."""
    u = lambda v: v * s  # noqa: E731  unit (0..1) to pixels
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([u(0.02), u(0.02), u(0.98), u(0.98)], radius=u(0.2), fill=colour)

    # The hole and the mouse share a layer, so everything below the floor can be cut off.
    mouse = Image.new("RGBA", img.size, (0, 0, 0, 0))
    m = ImageDraw.Draw(mouse)
    left, right, top, floor = 0.14, 0.86, 0.24, 0.84
    m.ellipse([u(left), u(top), u(right), u(top + right - left)], fill=HOLE)
    m.rectangle([u(left), u(top + (right - left) / 2), u(right), u(floor)], fill=HOLE)

    ear_r = 0.13 if detailed else 0.15
    for cx in (0.33, 0.67):
        m.ellipse([u(cx - ear_r), u(0.45 - ear_r), u(cx + ear_r), u(0.45 + ear_r)], fill=FUR)
        inner = ear_r * 0.6
        m.ellipse([u(cx - inner), u(0.45 - inner), u(cx + inner), u(0.45 + inner)], fill=EAR)
    m.ellipse([u(0.26), u(0.49), u(0.74), u(1.0)], fill=FUR)

    eye_r = 0.045 if detailed else 0.065
    eye_y = 0.65
    for cx in (0.415, 0.585):
        m.ellipse([u(cx - eye_r), u(eye_y - eye_r), u(cx + eye_r), u(eye_y + eye_r)], fill=EYE)
        if detailed:
            g = eye_r * 0.38
            m.ellipse([u(cx - g + 0.013), u(eye_y - g - 0.013), u(cx + g + 0.013), u(eye_y + g - 0.013)], fill=(255, 255, 255))

    nose_r = 0.035 if detailed else 0.05
    m.ellipse([u(0.5 - nose_r), u(0.765 - nose_r), u(0.5 + nose_r), u(0.765 + nose_r)], fill=EAR)

    if detailed:
        width = max(1, int(u(0.012)))
        for side in (-1, 1):
            for dy in (-0.025, 0.02):
                m.line([(u(0.5 + side * 0.08), u(0.775)), (u(0.5 + side * 0.25), u(0.775 + dy * 2))], fill=WHISKER, width=width)

    m.rectangle([0, u(floor), s, s], fill=(0, 0, 0, 0))
    img.alpha_composite(mouse)

    # The floor the hole stands on.
    d.rounded_rectangle([u(0.07), u(floor), u(0.93), u(floor + 0.05)], radius=u(0.025), fill=FUR)


def draw_icon(px, detailed, colour=TILE):
    s = px * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    tile(img, s, detailed, colour)
    return img.resize((px, px), Image.LANCZOS)


def logo(icon, text_colour):
    """Icon on the left, Squeaker on the right."""
    h = 440
    font = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 190)
    measure = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    width = 470 + int(measure.textlength("Squeaker", font=font)) + 40
    img = Image.new("RGBA", (width, h), (0, 0, 0, 0))
    img.alpha_composite(icon.resize((400, 400), Image.LANCZOS), (20, 20))
    ImageDraw.Draw(img).text((470, h / 2), "Squeaker", font=font, fill=text_colour, anchor="lm")
    return img.crop(img.getbbox())


def save_ico(path, colour, sizes):
    # Below 32px the whiskers are just noise, so use the chunky drawing.
    frames = [draw_icon(n, detailed=n >= 32, colour=colour) for n in sizes]
    frames[-1].save(path, format="ICO", sizes=[(n, n) for n in sizes], append_images=frames[:-1])
    return frames


def main():
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    ASSETS.mkdir(parents=True, exist_ok=True)
    frames = save_ico(ASSETS / "Squeaker.ico", TILE, sizes)
    save_ico(ASSETS / "SqueakerPaused.ico", TILE_PAUSED, sizes)

    big = draw_icon(1024, detailed=True)
    big.save(HERE / "icon.png")
    logo(big, (26, 26, 26)).save(HERE / "logo-light.png")
    logo(big, (255, 255, 255)).save(HERE / "logo-dark.png")

    # Contact sheet of every size, to eyeball the small ones.
    sheet = Image.new("RGBA", (sum(sizes) + 12 * len(sizes) + 12, 256 + 24), (240, 240, 240, 255))
    x = 12
    for n, frame in zip(sizes, frames):
        sheet.alpha_composite(frame, (x, 12))
        x += n + 12
    sheet.save(HERE / "sizes.png")
    print("Wrote", ASSETS / "Squeaker.ico", "and branding PNGs in", HERE)


if __name__ == "__main__":
    main()
