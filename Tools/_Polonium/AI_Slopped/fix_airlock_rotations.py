#!/usr/bin/env python3
"""Fix airlock/firelock rotations on SS14 maps so their (respritened) directional
sprites align with the wall line they sit in.

Convention (verified empirically against hand-placed doors on box.yml, 745 doors,
zero counterexamples): a door whose wall line runs East-West wants rot 0; a door
whose wall line runs North-South wants rot 90deg (pi/2).

Detection is tiered per door tile:
  1. walls/barriers on E or W (and not N/S) -> horizontal line -> rot 0
     walls/barriers on N or S (and not E/W) -> vertical   line -> rot 90
  2. no walls (door sits in a run of doors / windoors) -> use neighbouring DOORS'
     axis instead: door on E/W -> rot 0, door on N/S -> rot 90.  This is what
     handles doors standing next to other doors (a multi-tile airlock run).
  3. ambiguous (both axes present, or nothing adjacent) -> left untouched, listed
     under REVIEW so a human can eyeball it in the editor.

Only doors whose AXIS is currently wrong are rewritten. A door already on the
right axis is left exactly as-is -- we never flip an intentional 180/270 facing,
and we can't infer front/back from walls anyway (see ceiling below).

# ponytail: axis-only. Front/back facing (rot 0 vs 180, 90 vs 270) is not
# derivable from wall geometry; for glass airlocks where the window side matters,
# eyeball those in the map editor. This fixes the 90deg-wrong-axis problem, which
# is the actual respritening breakage.

Dry-run by default. Pass --write to edit files in place (git tracks the rest).
"""
import argparse, math, re, sys
import yaml

HALF_PI = "1.5707963267948966 rad"  # canonical pi/2 string, matches SS14 serializer

# --- loader that tolerates SS14 custom !type: tags -------------------------
_L = yaml.CSafeLoader
def _any(loader, tag_suffix, node):
    if isinstance(node, yaml.ScalarNode):   return loader.construct_scalar(node)
    if isinstance(node, yaml.SequenceNode): return loader.construct_sequence(node)
    return loader.construct_mapping(node)
_L.add_multi_constructor('', _any)

# Strict loader that rejects duplicate mapping keys, exactly like SS14's parser
# (PyYAML's default silently keeps the last value, which hides a corrupt write).
class DuplicateKey(Exception): pass
class _Strict(yaml.CSafeLoader): pass
_Strict.add_multi_constructor('', _any)
def _strict_map(loader, node, deep=False):
    seen, out = set(), {}
    for k, v in node.value:
        kk = loader.construct_object(k, deep=True)
        if kk in seen:
            raise DuplicateKey(f"duplicate key {kk!r} at {k.start_mark}")
        seen.add(kk); out[kk] = loader.construct_object(v, deep=deep)
    return out
_Strict.construct_mapping = _strict_map

def validate(path):
    yaml.load(open(path), Loader=_Strict)   # raises DuplicateKey on corruption

# --- classification --------------------------------------------------------
_DOOR_PREFIXES = ("Airlock", "Firelock", "Windoor", "Shutters", "BlastDoor", "HighSec", "PlasticFlaps")
def is_door(p):
    # Material doors (WoodDoor, GoldDoor, PlasmaDoor, ...) share no prefix but all
    # end in "Door" and sit in a wall line just like airlocks. Frames end in
    # "Frame" and electronics in "Electronics", so neither is caught here.
    # ponytail: a handful of item ids also end in "Door" (SpellbookWandDoor,
    # WeaponWandPolymorphDoor, WizardWallDoor); none are map-placed wall barriers,
    # and they're never in the target list, so treating them as "doors" is inert.
    return p.startswith(_DOOR_PREFIXES) or p.endswith("Door")
def is_barrier(p):   # forms a wall line the door should continue
    # Most window ids don't start with "Window" (ReinforcedWindow, PlasmaWindow,
    # ShuttleWindow, ...), so match "Window" anywhere. Doors win the tie (a
    # ShuttersWindow is a door, not a barrier), and wall-mounted buttons/signs
    # that carry "Window" in their id (SignalButtonWindows) are not barriers.
    if is_door(p) or "Button" in p:
        return False
    return p.startswith(("Wall", "Grille")) or "Window" in p

# Never treat these as doors, even though they share a door prefix:
#  Edge        -> edge-mounted firelocks: rotation picks a tile EDGE, not a wall line
#  Electronics -> circuit-board ITEMS (FirelockElectronics, AirlockElectronics), not doors
#  Assembly    -> unfinished construction frames: they're norot, a rot line does nothing
EXCLUDE = ("Edge", "Electronics", "Assembly")
def is_excluded(p):
    return any(x in p for x in EXCLUDE)

def axis_of(rad):    # 0 = horizontal (rot 0/180), 1 = vertical (rot 90/270)
    return round(rad / (math.pi / 2)) % 2

def decide(occ, grid, tx, ty):
    """Return needed axis (0=horizontal, 1=vertical) or None if undetermined.

    A door blocks passage along its facing axis, so its *passage* direction
    (front/back, perpendicular to the door line) must be open -- you can't walk
    through a wall. A horizontal door (rot 0) passes N-S, so walls on BOTH N and
    S make horizontal impossible; likewise walls on both E and W rule out
    vertical. That physical constraint decides most doors outright.

    When neither orientation is blocked, fall back to which axis forms a
    *through-line* (both opposite neighbours occupied by a barrier OR another
    door -- this is what makes doors-next-to-doors runs work), then to the axis
    backed by more actual walls."""
    W = occ.get((grid, tx-1, ty), ());  E = occ.get((grid, tx+1, ty), ())
    N = occ.get((grid, tx, ty+1), ());  S = occ.get((grid, tx, ty-1), ())
    bW, bE = "barrier" in W, "barrier" in E
    bN, bS = "barrier" in N, "barrier" in S

    horiz_blocked = bN and bS   # horizontal door would open into walls N/S
    vert_blocked  = bE and bW   # vertical door would open into walls E/W
    if horiz_blocked and not vert_blocked: return 1
    if vert_blocked and not horiz_blocked: return 0
    if horiz_blocked and vert_blocked:     return None  # walled all round -> review

    occupied = lambda c: ("barrier" in c) or ("door" in c)
    ew_line = occupied(W) and occupied(E)
    ns_line = occupied(N) and occupied(S)
    if ew_line and not ns_line: return 0
    if ns_line and not ew_line: return 1
    ew_b, ns_b = bW + bE, bN + bS
    if ew_b != ns_b: return 0 if ew_b > ns_b else 1
    return None  # genuinely ambiguous (corner / isolated) -> manual review

# --- main ------------------------------------------------------------------
def process(path, target_prefixes, write):
    doc = yaml.load(open(path), Loader=_L)
    groups = doc.get("entities")
    if not groups:
        return None

    occ = {}          # (grid,tx,ty) -> set{"barrier","door"}
    doors = []        # (uid, grid, tx, ty, rad, is_target)
    skipped = 0       # target doors dropped (off-centre or unparseable rot) -> reconcile totals
    for g in groups:
        p = g["proto"]
        barrier = is_barrier(p); door = is_door(p)
        if not (barrier or door):
            continue
        target = door and not is_excluded(p) and any(p.startswith(t) for t in target_prefixes)
        for e in g["entities"]:
            tr = next((c for c in e["components"] if c.get("type") == "Transform"), None)
            if not tr or "pos" not in tr:
                continue
            grid = tr.get("parent")
            try:
                x, y = map(float, str(tr["pos"]).split(","))
            except ValueError:
                continue
            # real placed doors/walls sit tile-centred (x.5, y.5); anything else is
            # a loose item (e.g. an electronics board dropped on the floor) -> ignore
            if abs(x - math.floor(x) - 0.5) > 0.1 or abs(y - math.floor(y) - 0.5) > 0.1:
                if target: skipped += 1
                continue
            tx, ty = math.floor(x), math.floor(y)
            s = occ.setdefault((grid, tx, ty), set())
            if barrier: s.add("barrier")
            if door:    s.add("door")
            if door:
                rot = tr.get("rot")
                # SS14 also accepts a bare-degrees rot ("rot: 90"); we can't trust the
                # value as radians and _rewrite's regex only matches "... rad", so it
                # would duplicate the key. Skip such doors rather than corrupt them.
                if rot is not None and not str(rot).rstrip().endswith("rad"):
                    if target: skipped += 1
                    continue
                rad = float(str(rot).split()[0]) if rot else 0.0
                doors.append((e["uid"], grid, tx, ty, rad, target))

    changes = {}      # uid -> new_axis (0 => remove rot, 1 => set pi/2)
    review = []
    for uid, grid, tx, ty, rad, target in doors:
        if not target:
            continue
        need = decide(occ, grid, tx, ty)
        if need is None:
            review.append((uid, tx, ty))
            continue
        if axis_of(rad) != need:
            changes[uid] = need

    stats = dict(doors=sum(1 for d in doors if d[5]), changes=len(changes),
                 review=len(review), skipped=skipped)
    if write and changes:
        text = _rewritten_text(path, changes)
        yaml.load(text, Loader=_Strict)   # reject a duplicate rot key BEFORE writing to disk
        open(path, "w").write(text)
    return stats, changes, review

def _rewritten_text(path, changes):
    """Line-level edit: set/remove the rot line inside each changed door's
    Transform block, keyed by uid. Everything else stays byte-identical.
    Returns the new file text (caller validates, then writes)."""
    lines = open(path).read().splitlines(keepends=True)
    out = []
    cur_uid = None
    in_tf = False
    rot_done = False   # emitted (or intentionally omitted) rot for the current changed transform
    uid_re  = re.compile(r'^(\s*)- uid:\s*(\d+)\s*$')
    type_re = re.compile(r'^\s*- type:\s*(\S+)')
    rot_re  = re.compile(r'^(\s*)rot:\s*\S+\s*rad\s*$')
    pos_re  = re.compile(r'^(\s*)pos:\s*')
    for line in lines:
        m = uid_re.match(line)
        if m:
            cur_uid = int(m.group(2)); in_tf = False; rot_done = False
            out.append(line); continue
        tm = type_re.match(line)
        if tm:
            in_tf = (tm.group(1) == "Transform"); rot_done = False
            out.append(line); continue
        if in_tf and cur_uid in changes:
            want = changes[cur_uid]        # 0 => no rot line, 1 => exactly one rot = pi/2
            rm = rot_re.match(line)
            if rm:
                # drop every existing rot line; emit our single one here if not done yet
                if want == 1 and not rot_done:
                    out.append(f"{rm.group(1)}rot: {HALF_PI}\n"); rot_done = True
                continue
            pm = pos_re.match(line)
            if pm and want == 1 and not rot_done:
                out.append(f"{pm.group(1)}rot: {HALF_PI}\n"); rot_done = True
                out.append(line); continue
        out.append(line)
    return "".join(out)

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("maps", nargs="+", help="map .yml files")
    ap.add_argument("--write", action="store_true", help="edit in place (default: dry-run)")
    ap.add_argument("--protos",
                    default="Airlock,Firelock,Shutters,BlastDoor,HighSec,PlasticFlaps,"
                            "WoodDoor,GoldDoor,SilverDoor,PlasmaDoor,PaperDoor,WebDoor,"
                            "CardDoor,MetalDoor,BananiumDoor",
                    help="comma-separated proto prefixes to rotate (default includes "
                         "airlocks/firelocks/shutters/blast/highsec + material doors)")
    ap.add_argument("--show-review", action="store_true",
                    help="list tiles that couldn't be resolved")
    a = ap.parse_args()
    targets = [t.strip() for t in a.protos.split(",") if t.strip()]
    # Edge-mounted doors pick a tile EDGE, not a wall-line axis -- the convention
    # doesn't apply, so refuse them as targets rather than silently mis-rotate.
    bad = [t for t in targets if t == "Windoor" or "Edge" in t]
    if bad:
        ap.error(f"edge-mounted, not wall-line-axis doors: {bad} -- cannot rotate these")
    grand = 0
    for path in a.maps:
        res = process(path, targets, a.write)
        if res is None:
            print(f"{path}: no entities, skipped"); continue
        stats, changes, review = res
        verb = "rewrote" if a.write else "would change"
        extra = f", {stats['skipped']} skipped (off-centre/odd rot)" if stats['skipped'] else ""
        print(f"{path}: {stats['doors']} target doors, {verb} {stats['changes']}, "
              f"{stats['review']} need manual review{extra}")
        grand += stats['changes']
        if a.show_review and review:
            for uid, tx, ty in review[:40]:
                print(f"    review uid={uid} tile=({tx},{ty})")
    print(f"total {'rewritten' if a.write else 'to change'}: {grand}")

if __name__ == "__main__":
    main()
