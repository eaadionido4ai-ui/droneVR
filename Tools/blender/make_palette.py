#!/usr/bin/env python3
"""Collects every flat color used by the exported models and lays them out on one small picture, the palette.

    python3 make_palette.py <folder of .glb files> <palette.png> <palette.json>

Every flat-colored piece in DRONE VR is drawn with this one picture: each face points at the square of
its color. That way the whole barangay needs a single material in Unity, which is light work for a headset.
The palette is a GRID x GRID sheet of squares. Colors keep their square from one run to the next (the list
in palette.json only grows), so models exported earlier stay right.
"""
import json, struct, sys, os, glob

GRID = 32      # squares per side: room for 1024 colors
CELL = 16      # pixels per square


def names_in_glb(path):
    with open(path, 'rb') as f:
        magic, version, length = struct.unpack('<4sII', f.read(12))
        assert magic == b'glTF', path
        clen, ctype = struct.unpack('<I4s', f.read(8))
        doc = json.loads(f.read(clen))
    return [n.get('name', '') for n in doc.get('nodes', [])]


def main():
    src, png, js = sys.argv[1:4]
    colors = []
    if os.path.exists(js):
        colors = json.load(open(js))['colors']
    seen = set(colors)
    for path in sorted(glob.glob(os.path.join(src, '*.glb'))):
        for name in names_in_glb(path):
            parts = name.split('__')
            if parts[0] in ('S', 'W') and len(parts) >= 4:
                c = parts[3][:6].lower()
                if c not in seen:
                    seen.add(c); colors.append(c)
    if len(colors) > GRID * GRID:
        raise SystemExit('too many colors for the palette: %d' % len(colors))
    from PIL import Image
    im = Image.new('RGB', (GRID * CELL, GRID * CELL), (255, 0, 255))
    px = im.load()
    for k, c in enumerate(colors):
        r, g, b = int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16)
        cx, cy = (k % GRID) * CELL, (k // GRID) * CELL
        for y in range(CELL):
            for x in range(CELL):
                px[cx + x, cy + y] = (r, g, b)
    im.save(png)
    json.dump({'grid': GRID, 'cell': CELL, 'colors': colors}, open(js, 'w'), indent=0)
    print('palette: %d colors -> %s' % (len(colors), png))


if __name__ == '__main__':
    main()
