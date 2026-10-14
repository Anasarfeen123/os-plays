#!/usr/bin/env python3
"""Repository hygiene for a Unity project, run by CI on every push and pull request (and fine to run locally):
every asset under Assets/ and Packages/<embedded>/ has its .meta and every .meta its asset, no file is over 50 MB,
and no build output or signing key is committed."""
import os, subprocess, sys

LIMIT = 50 * 1024 * 1024
FORBIDDEN = (".apk", ".aab", ".keystore", ".jks", ".p12")

files = subprocess.run(["git", "ls-files", "-z"], capture_output=True, text=True, check=True).stdout.split("\0")
files = [f for f in files if f]
tracked = set(files)
problems = []

for f in files:
    if f.lower().endswith(FORBIDDEN):
        problems.append(f"build output or key committed: {f}")
    if os.path.isfile(f) and os.path.getsize(f) > LIMIT:
        problems.append(f"over 50 MB: {f}")

def unity_folder(path):
    return path.startswith("Assets/") or (path.startswith("Packages/") and path.count("/") >= 2)

for f in files:
    if not unity_folder(f):
        continue
    name = os.path.basename(f)
    if name.startswith(".") or "~/" in f or f.endswith("~"):
        continue  # Unity ignores hidden files and Foo~ folders
    if f.endswith(".meta"):
        asset = f[:-5]
        if asset not in tracked and not any(t.startswith(asset + "/") for t in tracked):
            problems.append(f"orphan .meta (asset missing): {f}")
    elif f + ".meta" not in tracked:
        problems.append(f"missing .meta: {f}")
    # folders need their .meta too
    parent = os.path.dirname(f)
    while parent and parent not in ("Assets", "Packages") and parent.count("/") >= (1 if parent.startswith("Assets") else 2):
        if parent + ".meta" not in tracked:
            problems.append(f"missing folder .meta: {parent}.meta")
        parent = os.path.dirname(parent)

problems = sorted(set(problems))
for p in problems:
    print("✗", p)
print(f"{len(files)} files checked, {len(problems)} problem(s)")
sys.exit(1 if problems else 0)
