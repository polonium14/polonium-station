#!/usr/bin/env python3
"""Rebuild linear.yml from Functional Basics + Items/Tide/sections."""
from __future__ import annotations

import base64
import re
import struct
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / "Resources/Maps/_Polonium/Tutorial/linear.yml"
BRANCH = "tutorial/master"
MAP_UID = 596
BASICS_GRID = 1
BASICS = "Resources/Maps/_Functional/TutorialServer/Roles/Basics.yml"
GAP = 16
CHUNK = 16
ROW_WRAP = 220

MAP_ENTITY = (
    f"  - uid: {MAP_UID}\n"
    "    components:\n"
    "    - type: MetaData\n"
    "      name: Map Entity\n"
    "    - type: Transform\n"
    "    - type: Map\n"
    "      mapPaused: True\n"
    "    - type: GridTree\n"
    "    - type: Broadphase\n"
    "    - type: OccluderTree\n"
)

ROLES = [
    ("Items", "Resources/Maps/_Functional/TutorialServer/Roles/Items.yml"),
    ("Tide", "Resources/Maps/_Functional/TutorialServer/Roles/Tide.yml"),
]

SECTIONS = [
    ("SectionArrivals", "Resources/Maps/_Functional/TutorialServer/Sections/Arrivals.yml"),
    ("SectionAtmos", "Resources/Maps/_Functional/TutorialServer/Sections/Atmos.yml"),
    ("SectionBar", "Resources/Maps/_Functional/TutorialServer/Sections/Bar.yml"),
    ("SectionBrig", "Resources/Maps/_Functional/TutorialServer/Sections/Brig.yml"),
    ("SectionCargoOffice", "Resources/Maps/_Functional/TutorialServer/Sections/CargoOffice.yml"),
    ("SectionChapel", "Resources/Maps/_Functional/TutorialServer/Sections/Chapel.yml"),
    ("SectionChem", "Resources/Maps/_Functional/TutorialServer/Sections/Chem.yml"),
    ("SectionCommand", "Resources/Maps/_Functional/TutorialServer/Sections/Command.yml"),
    ("SectionEngineering", "Resources/Maps/_Functional/TutorialServer/Sections/Engineering.yml"),
    ("SectionHydroponics", "Resources/Maps/_Functional/TutorialServer/Sections/Hydroponics.yml"),
    ("SectionJanitor", "Resources/Maps/_Functional/TutorialServer/Sections/Janitor.yml"),
    ("SectionKitchen", "Resources/Maps/_Functional/TutorialServer/Sections/Kitchen.yml"),
    ("SectionMaintAntag", "Resources/Maps/_Functional/TutorialServer/Sections/MaintAntag.yml"),
    ("SectionMedbay", "Resources/Maps/_Functional/TutorialServer/Sections/Medbay.yml"),
    ("SectionScience", "Resources/Maps/_Functional/TutorialServer/Sections/Science.yml"),
    ("SectionSecurity", "Resources/Maps/_Functional/TutorialServer/Sections/Security.yml"),
    ("SectionSurgery", "Resources/Maps/_Functional/TutorialServer/Sections/Surgery.yml"),
    ("SectionTheatre", "Resources/Maps/_Functional/TutorialServer/Sections/Theatre.yml"),
]


def git_show(spec: str) -> str:
    text = subprocess.check_output(
        ["git", "show", f"{BRANCH}:{spec}"],
        cwd=ROOT,
        text=True,
        encoding="utf-8",
    )
    # YAML document end; leftover in a merged file starts a second document.
    return re.sub(r"^\.\.\.\s*$", "", text, flags=re.M)


def collect_entity_ids() -> set[str]:
    ids: set[str] = set()
    ent = re.compile(r"^  id:\s*([A-Za-z0-9_.-]+)", re.M)
    is_ent = re.compile(r"^- type:\s*entity\b", re.M)
    for path in (ROOT / "Resources/Prototypes").rglob("*.yml"):
        text = path.read_text(encoding="utf-8", errors="ignore")
        if not is_ent.search(text):
            continue
        ids.update(ent.findall(text))
    return ids


def collect_tile_info() -> tuple[set[str], dict[str, str], dict[str, int]]:
    tiles: set[str] = set()
    alias: dict[str, str] = {}
    variants: dict[str, int] = {}
    cur_type = None
    cur_id = None
    buf: dict = {}
    for path in (ROOT / "Resources/Prototypes").rglob("*.yml"):
        cur_type = None
        cur_id = None
        buf = {}
        for line in path.read_text(encoding="utf-8", errors="ignore").splitlines():
            if line.startswith("- type:"):
                if cur_type == "tile" and cur_id:
                    tiles.add(cur_id)
                    if "variants" in buf:
                        variants[cur_id] = int(buf["variants"])
                elif cur_type == "tileAlias" and cur_id and "target" in buf:
                    alias[cur_id] = buf["target"]
                cur_type = line.split(":", 1)[1].strip()
                cur_id = None
                buf = {}
                continue
            m = re.match(r"^  id:\s*(\S+)\s*$", line)
            if m:
                cur_id = m.group(1)
                continue
            m = re.match(r"^  (variants|target):\s*(\S+)\s*$", line)
            if m:
                buf[m.group(1)] = m.group(2)
        if cur_type == "tile" and cur_id:
            tiles.add(cur_id)
            if "variants" in buf:
                variants[cur_id] = int(buf["variants"])
        elif cur_type == "tileAlias" and cur_id and "target" in buf:
            alias[cur_id] = buf["target"]
    return tiles, alias, variants


def resolve_tile(name: str, tiles: set[str], alias: dict[str, str]) -> str:
    seen: set[str] = set()
    while name in alias and name not in seen:
        seen.add(name)
        name = alias[name]
    if name in tiles:
        return name
    return "Plating" if name != "Space" else "Space"


def parse_tilemap(text: str) -> dict[int, str]:
    m = re.search(r"^tilemap:\n((?:  .+\n)+)", text, re.M)
    if not m:
        return {}
    out = {}
    for line in m.group(1).splitlines():
        mm = re.match(r"\s+(\d+):\s+(\S+)", line)
        if mm:
            out[int(mm.group(1))] = mm.group(2)
    return out


def split_proto_groups(raw: str) -> tuple[str, list[str]]:
    marker = "\nentities:\n"
    idx = raw.find(marker)
    if idx < 0:
        raise SystemExit("no entities block")
    header = raw[: idx + len(marker)]
    body = raw[idx + len(marker) :]
    starts = [m.start() for m in re.finditer(r"^- proto: ", body, re.M)]
    if not starts:
        raise SystemExit("no proto groups")
    groups = []
    for i, start in enumerate(starts):
        end = starts[i + 1] if i + 1 < len(starts) else len(body)
        groups.append(body[start:end])
    return header, groups


def proto_name(group: str) -> str:
    m = re.match(r"^- proto: (.+)$", group, re.M)
    if not m:
        return ""
    return m.group(1).strip().strip('"')


def split_entities(group: str) -> tuple[str, list[str]]:
    m = re.search(r"^  entities:\n", group, re.M)
    if not m:
        return group, []
    prefix = group[: m.end()]
    rest = group[m.end() :]
    starts = [mm.start() for mm in re.finditer(r"^  - uid: ", rest, re.M)]
    if not starts:
        return prefix, []
    ents = []
    for i, start in enumerate(starts):
        end = starts[i + 1] if i + 1 < len(starts) else len(rest)
        ents.append(rest[start:end])
    return prefix, ents


def entity_uid(ent: str) -> int:
    m = re.match(r"^  - uid: (\d+)", ent, re.M)
    if not m:
        raise SystemExit("entity without uid")
    return int(m.group(1))


def is_mapgrid(ent: str) -> bool:
    return "\n    - type: MapGrid\n" in ent or ent.startswith("    - type: MapGrid")


def chunk_bbox(ent: str) -> tuple[int, int, int, int]:
    xs: list[int] = []
    ys: list[int] = []
    for m in re.finditer(r"^\s+ind: (-?\d+),(-?\d+)\s*$", ent, re.M):
        xs.append(int(m.group(1)))
        ys.append(int(m.group(2)))
    if not xs:
        return 0, 0, CHUNK, CHUNK
    return min(xs) * CHUNK, min(ys) * CHUNK, (max(xs) + 1) * CHUNK, (max(ys) + 1) * CHUNK


def remap_chunk(
    b64: str,
    version: int,
    src_map: dict[int, str],
    dest_ids: dict[str, int],
    tiles: set[str],
    alias: dict[str, str],
    variants: dict[str, int],
) -> str:
    raw = base64.b64decode(b64)
    if version >= 7:
        in_stride = 7
    elif version >= 6:
        in_stride = 6
    else:
        in_stride = 4
    n = len(raw) // in_stride
    out = bytearray()
    for i in range(n):
        off = i * in_stride
        if version >= 6:
            tid = struct.unpack_from("<i", raw, off)[0]
            flags = raw[off + 4]
            var = raw[off + 5]
            rot = raw[off + 6] if version >= 7 else 0
        else:
            tid = struct.unpack_from("<H", raw, off)[0]
            flags = raw[off + 2]
            var = raw[off + 3]
            rot = 0
        name = src_map.get(tid, "Space")
        name = resolve_tile(name, tiles, alias)
        new_tid = dest_ids[name]
        vc = variants.get(name, 1)
        if name != "Space" and vc < 1:
            vc = 1
        if name != "Space" and var >= vc:
            var %= vc
        out += struct.pack("<iBBB", new_tid, flags, var, rot)
    return base64.b64encode(bytes(out)).decode("ascii")


def remap_grid_tiles(
    ent: str,
    src_map: dict[int, str],
    dest_ids: dict[str, int],
    tiles: set[str],
    alias: dict[str, str],
    variants: dict[str, int],
) -> str:
    def repl(m: re.Match) -> str:
        indent, b64, ver_s = m.group(1), m.group(2), m.group(3)
        ver = int(ver_s)
        new_b64 = remap_chunk(b64, ver, src_map, dest_ids, tiles, alias, variants)
        return f"{indent}tiles: {new_b64}\n{indent}version: 7"
    # tiles then version (usual order)
    ent = re.sub(
        r"^(\s+)tiles: (\S+)\n\1version: (\d+)",
        repl,
        ent,
        flags=re.M,
    )
    return ent


def strip_becomes_station(ent: str) -> str:
    return re.sub(
        r"^    - type: BecomesStation\n(?:      .+\n)*",
        "",
        ent,
        flags=re.M,
    )


def set_grid_header(ent: str, name: str, pos: tuple[float, float], parent: int, new_uid: int) -> str:
    ent = re.sub(r"^  - uid: \d+", f"  - uid: {new_uid}", ent, count=1, flags=re.M)
    if re.search(r"^    - type: MetaData\n      name:", ent, re.M):
        ent = re.sub(
            r"(    - type: MetaData\n      name: ).+",
            rf"\1{name}",
            ent,
            count=1,
            flags=re.M,
        )
    else:
        ent = re.sub(
            r"    - type: MetaData\n",
            f"    - type: MetaData\n      name: {name}\n",
            ent,
            count=1,
            flags=re.M,
        )
    x, y = pos
    new_xf = (
        f"    - type: Transform\n"
        f"      pos: {x:g},{y}\n"
        f"      parent: {parent}\n"
    )
    ent, n = re.subn(
        r"    - type: Transform\n(?:      .+\n)*",
        new_xf,
        ent,
        count=1,
    )
    if n != 1:
        raise SystemExit(f"could not rewrite Transform on {name}")
    return strip_becomes_station(ent)


def remap_parents_and_uids(ent: str, uidmap: dict[int, int]) -> str:
    def uid_sub(m: re.Match) -> str:
        old = int(m.group(1))
        return f"  - uid: {uidmap[old]}"

    def parent_sub(m: re.Match) -> str:
        old = m.group(1)
        if old == "invalid":
            return m.group(0)
        n = int(old)
        return f"parent: {uidmap.get(n, n)}"

    ent = re.sub(r"^  - uid: (\d+)", uid_sub, ent, count=1, flags=re.M)
    ent = re.sub(r"parent: (\d+|invalid)", parent_sub, ent)
    return ent


def parse_linear_uids(text: str) -> set[int]:
    return {int(m.group(1)) for m in re.finditer(r"^  - uid: (\d+)", text, re.M)}


def linear_bbox(text: str) -> tuple[int, int, int, int]:
    header, groups = split_proto_groups(text)
    for g in groups:
        if proto_name(g) != "":
            continue
        _, ents = split_entities(g)
        for e in ents:
            if is_mapgrid(e):
                return chunk_bbox(e)
    return 0, 0, 32, 32


def proto_key(pname: str) -> str:
    return '""' if pname == "" else pname


def join_ents(ents: list[str]) -> str:
    return "".join(e if e.endswith("\n") else e + "\n" for e in ents)


def strip_source(raw: str, entity_ids: set[str]) -> tuple[str, list[tuple[str, str]], list[str]]:
    _, groups = split_proto_groups(raw)
    grid_ent = None
    other: list[tuple[str, str]] = []
    dropped: list[str] = []
    for g in groups:
        pname = proto_name(g)
        _, ents = split_entities(g)
        if pname == "":
            for e in ents:
                if is_mapgrid(e):
                    grid_ent = e
                elif "\n    - type: Map\n" in e:
                    continue
                else:
                    other.append(("", e))
            continue
        if pname not in entity_ids:
            dropped.append(f"{pname}({len(ents)})")
            continue
        for e in ents:
            other.append((pname, e))
    if grid_ent is None:
        raise SystemExit("no MapGrid in source")
    return grid_ent, other, dropped


def wrap_meta(text: str) -> str:
    text = re.sub(r"^  category: Grid$", "  category: Map", text, count=1, flags=re.M)
    text = re.sub(r"^maps: \[\]\n", f"maps:\n- {MAP_UID}\n", text, count=1, flags=re.M)
    text = re.sub(r"^orphans:\n(?:- \d+\n)+", "orphans: []\n", text, count=1, flags=re.M)
    return text


def set_basics_grid(ent: str) -> str:
    ent = re.sub(r"^  - uid: \d+", f"  - uid: {BASICS_GRID}", ent, count=1, flags=re.M)
    if re.search(r"^    - type: MetaData\n      name:", ent, re.M):
        ent = re.sub(
            r"(    - type: MetaData\n      name: ).+",
            r"\1Stacja Symulacyjna",
            ent,
            count=1,
            flags=re.M,
        )
    ent = re.sub(
        r"    - type: Transform\n(?:      .+\n)*",
        f"    - type: Transform\n      parent: {MAP_UID}\n      pos: 0,0\n",
        ent,
        count=1,
    )
    if "BecomesStation" not in ent:
        ent = re.sub(
            r"(    - type: MetaData\n(?:      .+\n)+)",
            r"\1    - type: BecomesStation\n      id: Tutorial\n",
            ent,
            count=1,
        )
    return ent


def insert_unnamed(dest: str, ents: list[str]) -> str:
    m = re.search(r'^- proto: ""\n  entities:\n', dest, re.M)
    if not m:
        raise SystemExit('missing proto: "" group')
    nxt = re.search(r"^- proto: ", dest[m.end() :], re.M)
    if not nxt:
        raise SystemExit("no proto after unnamed group")
    at = m.end() + nxt.start()
    return dest[:at] + join_ents(ents) + dest[at:]


def main() -> None:
    entity_ids = collect_entity_ids()
    tiles, alias, variants = collect_tile_info()

    basics_raw = git_show(BASICS)
    dest_tilemap = parse_tilemap(basics_raw)
    dest_ids = {name: i for i, name in dest_tilemap.items()}
    next_tile = max(dest_tilemap) + 1

    def ensure_tile(name: str) -> int:
        nonlocal next_tile
        name = resolve_tile(name, tiles, alias)
        if name not in dest_ids:
            dest_ids[name] = next_tile
            dest_tilemap[next_tile] = name
            next_tile += 1
        return dest_ids[name]

    for name in list(dest_tilemap.values()):
        ensure_tile(name)

    grid_ent, other, dropped = strip_source(basics_raw, entity_ids)
    grid_ent = remap_grid_tiles(grid_ent, dest_tilemap, dest_ids, tiles, alias, variants)
    grid_ent = set_basics_grid(grid_ent)

    header, _ = split_proto_groups(basics_raw)
    header = wrap_meta(header)

    unnamed = [MAP_ENTITY, grid_ent if grid_ent.endswith("\n") else grid_ent + "\n"]
    by_proto: dict[str, list[str]] = {}
    for pname, e in other:
        by_proto.setdefault(pname, []).append(e)

    dest = header + f'- proto: ""\n  entities:\n' + "".join(unnamed)
    for pname, ents in by_proto.items():
        dest += f"- proto: {proto_key(pname)}\n  entities:\n" + join_ents(ents)
    if dropped:
        print(f"Basics dropped={len(dropped)} {', '.join(dropped)}")
    print(f"Basics: grid={BASICS_GRID} map={MAP_UID} ents={1 + 1 + len(other)}")

    used = parse_linear_uids(dest)
    next_uid = max(used | {MAP_UID}) + 1

    def alloc(old: int, uidmap: dict[int, int]) -> int:
        nonlocal next_uid
        if old not in uidmap:
            while next_uid == MAP_UID:
                next_uid += 1
            uidmap[old] = next_uid
            next_uid += 1
        return uidmap[old]

    bx0, by0, bx1, by1 = linear_bbox(dest)
    cursor_x = bx1 + GAP
    cursor_y = by0
    row_x0 = cursor_x
    row_h = 0
    grid_uids: list[int] = [BASICS_GRID]
    extra_grids: list[str] = []
    new_groups: list[str] = []
    stripped_log: list[str] = []
    if dropped:
        stripped_log.append(f"Basics: {', '.join(dropped)}")

    for label, spec in ROLES + SECTIONS:
        raw = git_show(spec)
        src_map = parse_tilemap(raw)
        for n in src_map.values():
            ensure_tile(n)
        grid_ent, other, dropped = strip_source(raw, entity_ids)

        gx0, gy0, gx1, gy1 = chunk_bbox(grid_ent)
        w, h = gx1 - gx0, gy1 - gy0
        if cursor_x > row_x0 and cursor_x + w > row_x0 + ROW_WRAP:
            cursor_x = row_x0
            cursor_y += row_h + GAP
            row_h = 0
        pos = (cursor_x - gx0, cursor_y - gy0)
        cursor_x += w + GAP
        row_h = max(row_h, h)

        uidmap: dict[int, int] = {}
        new_grid = alloc(entity_uid(grid_ent), uidmap)
        grid_uids.append(new_grid)
        for _, e in other:
            alloc(entity_uid(e), uidmap)

        grid_ent = remap_grid_tiles(grid_ent, src_map, dest_ids, tiles, alias, variants)
        grid_ent = set_grid_header(grid_ent, label, pos, MAP_UID, new_grid)
        extra_grids.append(grid_ent if grid_ent.endswith("\n") else grid_ent + "\n")

        imported: dict[str, list[str]] = {}
        for pname, e in other:
            imported.setdefault(pname, []).append(remap_parents_and_uids(e, uidmap))
        for pname, ents in imported.items():
            new_groups.append(
                f"- proto: {proto_key(pname)}\n  entities:\n" + join_ents(ents)
            )
        if dropped:
            stripped_log.append(f"{label}: {', '.join(dropped)}")
        print(
            f"{label}: grid={new_grid} pos={pos[0]:.0f},{pos[1]:.0f} "
            f"size={w}x{h} ents={1 + len(other)} dropped={len(dropped)}"
        )

    tm_lines = "".join(f"  {i}: {n}\n" for i, n in sorted(dest_tilemap.items()))
    dest = re.sub(r"^tilemap:\n(?:  .+\n)+", f"tilemap:\n{tm_lines}", dest, count=1, flags=re.M)
    dest = re.sub(
        r"^grids:\n(?:- \d+\n)+",
        "grids:\n" + "".join(f"- {u}\n" for u in grid_uids),
        dest,
        count=1,
        flags=re.M,
    )
    dest = insert_unnamed(dest, extra_grids)
    if not dest.endswith("\n"):
        dest += "\n"
    dest += "".join(new_groups)
    n_ent = len(re.findall(r"^  - uid: ", dest, re.M))
    dest = re.sub(r"^  entityCount: \d+", f"  entityCount: {n_ent}", dest, count=1, flags=re.M)
    dest = re.sub(r"^\.\.\.\s*\n", "", dest, flags=re.M)

    DEST.write_text(dest, encoding="utf-8", newline="\n")
    print(f"wrote {DEST.relative_to(ROOT)} entities={n_ent} grids={len(grid_uids)}")
    for line in stripped_log:
        print(" stripped", line)


if __name__ == "__main__":
    main()
