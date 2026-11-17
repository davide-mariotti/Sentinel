"""Builds Sentinel's icons from the dm. app-icon template.

Template (same for every app of mine): 512 rounded square (rx 112, like the dm. logo),
one Lucide glyph in white, the app's own background colour, the dm. dot in the corner.
Small sizes (<= 24 px) get a bigger, thicker glyph and no dot, or the tray icon turns to mush.

Needs Chrome (renders the SVG) and Pillow. Run: python icon/make-icons.py
"""
import base64, pathlib, subprocess, tempfile
from PIL import Image

HERE = pathlib.Path(__file__).parent
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
SIZES = [16, 20, 24, 32, 40, 48, 64, 256]

# Lucide 0.469.0 glyphs (ISC): eye / eye-off
EYE = '<path d="M2.062 12.348a1 1 0 0 1 0-.696 10.75 10.75 0 0 1 19.876 0 1 1 0 0 1 0 .696 10.75 10.75 0 0 1-19.876 0"/><circle cx="12" cy="12" r="3"/>'
EYE_OFF = ('<path d="M10.733 5.076a10.744 10.744 0 0 1 11.205 6.575 1 1 0 0 1 0 .696 10.747 10.747 0 0 1-1.444 2.49"/>'
           '<path d="M14.084 14.158a3 3 0 0 1-4.242-4.242"/>'
           '<path d="M17.479 17.499a10.75 10.75 0 0 1-15.417-5.151 1 1 0 0 1 0-.696 10.75 10.75 0 0 1 4.446-5.143"/>'
           '<path d="m2 2 20 20"/>')

ICONS = {  # file name: (background, glyph)
    "sentinel": ("#0e7490", EYE),
    "sentinel-off": ("#52525b", EYE_OFF),
}


def svg(bg, glyph, small=False):
    # glyph box: 300/512 on big sizes, 448/512 on small ones
    box, stroke, dot = (448, 2.6, "") if small else (300, 2.3, '<circle cx="432" cy="432" r="20" fill="#020402"/>')
    off = (512 - box) / 2
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">'
            f'<rect width="512" height="512" rx="112" fill="{bg}"/>'
            f'<g transform="translate({off} {off}) scale({box / 24})" fill="none" stroke="#fff" '
            f'stroke-width="{stroke}" stroke-linecap="round" stroke-linejoin="round">{glyph}</g>{dot}</svg>')


def render(svgs):
    """svgs: list of (svg, size) -> list of RGBA images, one Chrome screenshot for all."""
    x, cells = 0, []
    for s, px in svgs:
        cells.append(f'<img style="position:absolute;left:{x}px;top:0;width:{px}px;height:{px}px" '
                     f'src="data:image/svg+xml;base64,{base64.b64encode(s.encode()).decode()}">')
        x += px + 8
    with tempfile.TemporaryDirectory() as tmp:
        page, shot = pathlib.Path(tmp, "p.html"), pathlib.Path(tmp, "s.png")
        page.write_text(f'<body style="margin:0;background:transparent">{"".join(cells)}</body>', encoding="utf-8")
        subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                        "--default-background-color=00000000", f"--user-data-dir={tmp}/profile",
                        f"--window-size={x},{max(p for _, p in svgs)}", f"--screenshot={shot}", page.as_uri()],
                       check=True, capture_output=True)
        sheet = Image.open(shot).convert("RGBA")
        out, x = [], 0
        for _, px in svgs:
            out.append(sheet.crop((x, 0, x + px, px)))
            x += px + 8
        return out


for name, (bg, glyph) in ICONS.items():
    (HERE / f"{name}.svg").write_text(svg(bg, glyph), encoding="utf-8")
    imgs = render([(svg(bg, glyph, small=px <= 24), px) for px in SIZES])
    imgs[-1].save(HERE / f"{name}.png")
    imgs[-1].save(HERE / f"{name}.ico", sizes=[(p, p) for p in SIZES], append_images=imgs[:-1])
    print(name, "ok")
