#!/usr/bin/env python3
"""Copy another Unity project's assets into GRIDLOCK/Assets/Scenarios/<name>, keeping their GUIDs so scenes and
prefabs still resolve. Anything whose GUID GRIDLOCK already has (TextMeshPro, XRI samples, URP template settings) is
skipped, so references fall through to GRIDLOCK's copy instead of colliding.

usage: import_scenario.py <source project> <name> [excluded top-level folder ...]
"""
import os, re, shutil, sys

GUID = re.compile(r"^guid: ([0-9a-f]{32})", re.M)


def guids_under(root):
    found = set()
    for dirpath, _, files in os.walk(root):
        for f in files:
            if f.endswith(".meta"):
                with open(os.path.join(dirpath, f), errors="ignore") as fh:
                    m = GUID.search(fh.read(4096))
                    if m:
                        found.add(m.group(1))
    return found


def main():
    source, name, *excluded = sys.argv[1:]
    gridlock = os.path.expanduser("~/GRIDLOCK")
    src_assets = os.path.join(source, "Assets")
    dst_root = os.path.join(gridlock, "Assets", "Scenarios", name)
    existing = guids_under(os.path.join(gridlock, "Assets")) | guids_under(os.path.join(gridlock, "Packages"))
    existing -= guids_under(dst_root) if os.path.isdir(dst_root) else set()  # re-import overwrites our own copy

    copied = skipped = 0
    for dirpath, dirnames, files in os.walk(src_assets):
        rel = os.path.relpath(dirpath, src_assets)
        top = rel.split(os.sep)[0]
        if top in excluded:
            dirnames[:] = []
            continue
        for f in files:
            if f.endswith(".meta"):
                continue
            meta = os.path.join(dirpath, f + ".meta")
            if not os.path.exists(meta):
                continue
            with open(meta, errors="ignore") as fh:
                m = GUID.search(fh.read(4096))
            if m and m.group(1) in existing:
                skipped += 1
                continue
            out = os.path.join(dst_root, rel)
            os.makedirs(out, exist_ok=True)
            shutil.copy2(os.path.join(dirpath, f), out)
            shutil.copy2(meta, out)
            copied += 1
    # Folder metas, so folders keep their GUIDs too (skipping ones GRIDLOCK already uses).
    for dirpath, dirnames, _ in os.walk(dst_root):
        for d in dirnames:
            rel = os.path.relpath(os.path.join(dirpath, d), dst_root)
            src_meta = os.path.join(src_assets, rel + ".meta")
            if os.path.exists(src_meta):
                with open(src_meta, errors="ignore") as fh:
                    m = GUID.search(fh.read(4096))
                if not (m and m.group(1) in existing):
                    shutil.copy2(src_meta, os.path.join(dirpath, d + ".meta"))
    print(f"{name}: copied {copied} assets, skipped {skipped} already in GRIDLOCK")


if __name__ == "__main__":
    main()
