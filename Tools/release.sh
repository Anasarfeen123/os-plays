#!/usr/bin/env bash
# Builds both APKs and publishes a GitHub Release with them attached.
#
#   Tools/release.sh 0.2.0            build, commit the version, tag v0.2.0, push, create the release
#   Tools/release.sh 0.2.0 --dry-run  build only (APKs land in Builds/release/), nothing is committed or published
#
# Needs: Unity 6000.3.25f1 with Android Build Support (Unity Hub's default path), gh logged in, and the editor closed.
# Unity plays the scenes' audio while building scenes, so it runs on a silent PulseAudio sink.
set -euo pipefail

VERSION="${1:-}"
DRY_RUN="${2:-}"
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.25f1/Editor/Unity}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TAG="v$VERSION"
OUT="$ROOT/Builds/release"
LOGS="$ROOT/Logs/release"

die() { echo "release: $*" >&2; exit 1; }

[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "usage: Tools/release.sh X.Y.Z [--dry-run]"
[[ -z "$DRY_RUN" || "$DRY_RUN" == "--dry-run" ]] || die "unknown option $DRY_RUN"
cd "$ROOT"
[[ -x "$UNITY" ]] || die "Unity not found at $UNITY (set UNITY=/path/to/Editor/Unity)"
pgrep -f -- "-projectPath[= ]$ROOT( |$)" >/dev/null && die "Unity has the project open; close it first"
pgrep -fa "Editor/Unity" | grep -q -- "-projectPath \." && die "a Unity batch job is running; wait for it"

# The builds regenerate the scenes and write the version into the project, so they start from a clean tree (a dry run
# then puts it back as it was).
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || die "commit or stash your changes first"
if [[ -z "$DRY_RUN" ]]; then
    command -v gh >/dev/null || die "gh (GitHub CLI) is not installed"
    gh auth status >/dev/null 2>&1 || die "gh is not logged in (gh auth login)"
    [[ "$(git branch --show-current)" == "main" ]] || die "releases are made from main"
    git fetch -q origin main
    [[ "$(git rev-parse HEAD)" == "$(git rev-parse origin/main)" ]] || die "main is not in step with origin/main (pull or push first)"
    git rev-parse -q --verify "refs/tags/$TAG" >/dev/null && die "tag $TAG already exists"
fi

mkdir -p "$OUT" "$LOGS"
SINK=$(pactl load-module module-null-sink sink_name=osplays_release 2>/dev/null || true)
cleanup() { [[ -n "$SINK" ]] && pactl unload-module "$SINK" 2>/dev/null || true; }
trap cleanup EXIT

# Unity sometimes hangs after a finished build, so each build is stopped once it reports its result.
build() {
    local target=$1 log="$LOGS/$1.log"
    echo "release: building the $target APK (about 5 minutes)…"
    rm -f "$log"
    OSPLAYS_VERSION="$VERSION" PULSE_SINK=osplays_release "$UNITY" -batchmode -nographics -projectPath "$ROOT" \
        -buildTarget Android -executeMethod "Gridlock.Editor.Gridlock${target}Setup.ConfigureAndBuild" -logFile "$log" &
    local pid=$!
    while kill -0 $pid 2>/dev/null; do
        grep -qE "\[GRIDLOCK\] $target build (Succeeded|Failed)|error CS|BuildFailedException" "$log" 2>/dev/null && break
        sleep 3
    done
    sleep 8
    kill $pid 2>/dev/null || true
    wait $pid 2>/dev/null || true
    grep -q "\[GRIDLOCK\] $target build Succeeded" "$log" ||
        { grep -E "error CS|BuildFailedException|build Failed" "$log" | head -20 >&2; die "$target build failed (see $log)"; }
    cp "Builds/OSPlays-$target.apk" "$OUT/OSPlays-$target-$TAG.apk"
}

build Phone
build Quest # last: both builds rewrite the shared Android XR loader settings, and the project stays set up for the headset

echo "release: built"
ls -la "$OUT"/*"$TAG".apk
if [[ -n "$DRY_RUN" ]]; then
    git checkout -q -- Assets ProjectSettings # what the builds regenerated
    echo "release: dry run, nothing published"
    exit 0
fi

# The builds write the version into the project settings and regenerate the scenes: record that as the release commit.
git add -A ProjectSettings Assets
if ! git diff --cached --quiet; then
    git commit -q -m "Release $TAG"
fi
git tag -a "$TAG" -m "OS Plays $TAG"
git push -q origin main "$TAG"

NOTES=$(mktemp)
cat > "$NOTES" <<EOF
## Install

| File | Device |
|---|---|
| \`OSPlays-Quest-$TAG.apk\` | Meta Quest 2 / 3 / Pro (VR) |
| \`OSPlays-Phone-$TAG.apk\` | Android phones (AR on phones with Google Play Services for AR, touch view otherwise) |

With USB debugging on: \`adb install -r OSPlays-Quest-$TAG.apk\` (or the phone file). See the README for step-by-step
instructions, including installing on a phone without a computer.

EOF
gh release create "$TAG" "$OUT/OSPlays-Quest-$TAG.apk" "$OUT/OSPlays-Phone-$TAG.apk" \
    --title "OS Plays $TAG" --notes-file "$NOTES" --generate-notes
rm -f "$NOTES"
echo "release: published $(gh release view "$TAG" --json url -q .url)"
