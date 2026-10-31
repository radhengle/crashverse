#!/usr/bin/env python3
"""Validate the authored Level 7 tower integration without changing its physics contract."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "levels/ch1/layouts/m1_clockwork_tower.json"
RUNTIME = ROOT / "Assets/_Project/Resources/Data/Layouts/m1_clockwork_tower.json"
TOWER = Path("Assets/_Project/Models/Realistic/Buildings/Bld_StoneTower")
EXPECTED = {
    "ring0": ["R0"],
    "wallF1": ["W1F"],
    "wallB1": ["W1B"],
    "wallL1": ["W1L"],
    "wallR1": ["W1R"],
    "ring1": ["R1"],
    "wallF2": ["W2F"],
    "wallB2": ["W2B"],
    "wallL2": ["W2L"],
    "wallR2": ["W2R"],
    "ring2": ["R2"],
    "crownFloor": ["CrFl"],
    "crownWood": ["CrWdA", "CrWdB"],
    "crownBrick": ["CrBk"],
    "crownRoof": ["CrRfA", "CrRfB"],
}
# These fields define the actual puzzle/simulation, not its render-only skin.
PHYSICS_FIELDS = ("id", "mat", "p", "s", "sup", "joint", "role", "nonObj",
                  "explosive", "indestructible", "spinAxis", "pivot", "spinDps")


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def main():
    source, runtime = load(SOURCE), load(RUNTIME)
    if source.get("id") != runtime.get("id") or source.get("id") != "m1_clockwork_tower":
        raise SystemExit("Level 7 source/runtime layout IDs do not match")

    sb = {b["id"]: b for b in source.get("blocks", [])}
    rb = {b["id"]: b for b in runtime.get("blocks", [])}
    if list(sb) != list(rb):
        raise SystemExit("Level 7 source/runtime block ordering or IDs differ")

    for block_id in sb:
        for field in PHYSICS_FIELDS:
            if sb[block_id].get(field) != rb[block_id].get(field):
                raise SystemExit(f"Level 7 physics/layout drift: {block_id}.{field}")

    for block_id, names in EXPECTED.items():
        block = rb.get(block_id)
        if block is None:
            raise SystemExit(f"Level 7 tower block missing: {block_id}")
        actual = block.get("parts", [])
        expected = [f"{TOWER}/Kit_RealTower_{name}.fbx" for name in names]
        if actual != expected:
            raise SystemExit(f"{block_id} authored parts mismatch: {actual!r}")
        if abs(float(block.get("rotY", 0)) + 90.0) > 0.01:
            raise SystemExit(f"{block_id} must rotate authored +Z frontage toward cannon (-X)")
        for asset in actual:
            if not (ROOT / asset).is_file():
                raise SystemExit(f"Missing authored tower FBX: {asset}")

    all_parts = [p for b in rb.values() for p in b.get("parts", [])]
    if len(all_parts) != len(set(all_parts)):
        raise SystemExit("A Stone Tower FBX is referenced more than once in Level 7")

    print(f"Level 7 authored tower contract passed: {len(EXPECTED)} modules, "
          f"{len(all_parts)} FBXs, unchanged {len(PHYSICS_FIELDS)}-field simulation contract.")


if __name__ == "__main__":
    main()
