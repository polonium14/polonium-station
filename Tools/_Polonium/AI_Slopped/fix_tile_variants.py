#!/usr/bin/env python3
"""Clamp out-of-range tile variants on SS14 maps so they render with the current
(single-variant) tile sprites instead of a blank/missing texture.

Background: upstream swapped the base floor tiles (FloorSteel, FloorDark,
FloorWood, ...) from 4-variant sprites to single-variant 32px ones, dropping the
`variants: 4` field. Our maps still have variants 1/2/3 baked into the grid
chunks; the renderer has no variant 1 to draw, so those tiles show blank.

Fix is pure map data -- sprites untouched. For every tile whose variant index is
>= the variant count its (post-migration) tile actually has, rewrite the variant
to `variant % count` (single-variant tiles -> 0; a 6-variant tile's stray var 10
-> 4). Everything else in the file stays byte-identical.

The variant count is read from the tile prototype's `variants:` field (default 1
-- exactly what the engine uses to pick a variant), AFTER resolving tileAlias
migrations, because a map stores the pre-migration tile name but the engine draws
the migrated tile (FloorSteelDiagonal -> FloorSteel, variant carried over).

Chunk binary: 256 tiles/chunk. Byte layout per tile (see MapChunkSerializer.cs):
  v7 (7 bytes/tile): int32 TypeId | byte Flags | byte Variant | byte RotationMirroring
  v6 (6 bytes/tile): int32 TypeId | byte Flags | byte Variant
Variant is the single byte at offset 5 in both formats. We touch ONLY that byte --
byte 6 (RotationMirroring, used by e.g. exo.yml) is never read or written.

Dry-run by default. Pass --write to edit in place.
"""
import argparse, base64, glob, struct, sys
import yaml

VAR_OFF = 5         # variant's low byte, offset 5 within a tile (v6 and v7 alike)
TILES_PER_CHUNK = 256
PPM = 32            # tile size in px (fallback variant count = sprite_width // PPM)

_L = yaml.CSafeLoader
_L.add_multi_constructor('', lambda loader, tag, node:
    loader.construct_scalar(node) if isinstance(node, yaml.ScalarNode)
    else loader.construct_sequence(node) if isinstance(node, yaml.SequenceNode)
    else loader.construct_mapping(node))

def _png_width(sprite):
    p = "Resources" + sprite if sprite.startswith("/") else sprite
    try:
        with open(p, "rb") as fh:
            fh.read(16)
            return struct.unpack(">I", fh.read(4))[0]
    except OSError:
        return None

def load_tiledefs():
    """id -> {'variants': int, 'target': migration target or None}."""
    variants, alias = {}, {}
    for f in glob.glob("Resources/Prototypes/**/*.yml", recursive=True):
        try:
            data = yaml.load(open(f), Loader=_L)
        except Exception:
            continue
        if not isinstance(data, list):
            continue
        for n in data:
            if not isinstance(n, dict):
                continue
            if n.get("type") == "tile":
                # engine picks variant from the `variants` field (default 1). Fall
                # back to sprite width only if the field is absent AND the sprite is
                # wider than one tile (older defs that relied on auto-detection).
                v = n.get("variants")
                if v is None:
                    w = _png_width(n["sprite"]) if n.get("sprite") else None
                    v = (w // PPM) if w and w > PPM else 1
                variants[n["id"]] = max(1, int(v))
            elif n.get("type") == "tileAlias":
                alias[n["id"]] = n.get("target")
    return variants, alias

def make_vcount(variants, alias):
    cache = {}
    def resolve(name, seen=None):
        seen = seen or set()
        if name in variants:
            return variants[name]
        if name in alias and name not in seen:
            seen.add(name)
            return resolve(alias[name], seen)
        return 1   # unknown tile -> assume single variant (safest: clamps to 0)
    def vcount(name):
        if name not in cache:
            cache[name] = resolve(name)
        return cache[name]
    return vcount

def process(path, vcount, write):
    doc = yaml.load(open(path), Loader=_L)
    if not isinstance(doc, dict):
        return None
    tilemap = doc.get("tilemap") or {}
    text = open(path).read()
    fixed_tiles = 0
    replacements = {}   # old_b64 -> new_b64 (deduped; identical chunks share)
    for g in doc.get("entities", []):
        for e in g["entities"]:
            for c in e["components"]:
                if not (isinstance(c, dict) and c.get("type") == "MapGrid" and "chunks" in c):
                    continue
                for ch in c["chunks"].values():
                    old_b64 = ch["tiles"]
                    if old_b64 in replacements:
                        continue
                    b = bytearray(base64.b64decode(old_b64))
                    if len(b) % TILES_PER_CHUNK:
                        raise SystemExit(f"{path}: chunk is {len(b)} bytes, not a multiple of "
                                         f"{TILES_PER_CHUNK} tiles -- unexpected format, aborting")
                    stride = len(b) // TILES_PER_CHUNK   # 6 (v6) or 7 (v7)
                    dirty = 0
                    for off in range(0, len(b), stride):
                        tid = struct.unpack_from("<i", b, off)[0]
                        if tid == 0:
                            continue
                        var = b[off + VAR_OFF]           # variant low byte (< 256)
                        vc = vcount(tilemap.get(tid, ""))
                        if var >= vc:
                            b[off + VAR_OFF] = var % vc
                            dirty += 1
                    if dirty:
                        replacements[old_b64] = base64.b64encode(bytes(b)).decode()
                        fixed_tiles += dirty
    chunks_changed = len(replacements)
    if write and replacements:
        # Line-anchored swap: rewrite only the value of each `tiles: <b64>` line
        # whose exact base64 is a fixed chunk. A base64 value never spans a line,
        # so this cannot collide the way a global text.replace(old,new) can (a
        # chunk's b64 appearing as a substring elsewhere would corrupt it).
        import re as _re
        out = []
        for line in text.splitlines(keepends=True):
            m = _re.match(r'^(\s*)tiles:\s*(\S+)\s*$', line)
            if m and m.group(2) in replacements:
                out.append(f"{m.group(1)}tiles: {replacements[m.group(2)]}\n")
            else:
                out.append(line)
        open(path, "w").write("".join(out))
    return dict(chunks=chunks_changed, tiles=fixed_tiles)

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("maps", nargs="+", help="map .yml files")
    ap.add_argument("--write", action="store_true", help="edit in place (default: dry-run)")
    a = ap.parse_args()
    variants, alias = load_tiledefs()
    vcount = make_vcount(variants, alias)
    grand = 0
    for path in a.maps:
        res = process(path, vcount, a.write)
        if res is None:
            print(f"{path}: not a map, skipped"); continue
        verb = "fixed" if a.write else "would fix"
        print(f"{path}: {verb} {res['tiles']} tiles across {res['chunks']} chunks")
        grand += res['tiles']
    print(f"total tiles {'fixed' if a.write else 'to fix'}: {grand}")

if __name__ == "__main__":
    main()
