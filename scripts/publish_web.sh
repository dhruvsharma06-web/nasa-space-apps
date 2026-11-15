#!/usr/bin/env bash
# Publish the Unity WebGL build to the gh-pages branch (GitHub Pages).
#
#   scripts/publish_web.sh [BUILD_DIR] [--push]
#
#   BUILD_DIR  Unity WebGL output folder (default: Builds/WebGL). Build it UNCOMPRESSED
#              (Player > Publishing Settings > Compression Format = Disabled): GitHub Pages
#              cannot send the Content-Encoding headers that .br/.gz builds need.
#   --push     Push gh-pages to origin. Only use this after the team lead says "approved".
#              Without it, the script only commits to the local gh-pages branch.
#
# Uses a temporary git worktree, so your current branch and working tree are never touched,
# and nothing is ever written to main.
set -euo pipefail

BUILD_DIR="Builds/WebGL"
PUSH=false
for arg in "$@"; do
  case "$arg" in
    --push) PUSH=true ;;
    -h|--help) sed -n '2,15p' "$0"; exit 0 ;;
    -*) echo "Unknown option: $arg" >&2; exit 2 ;;
    *) BUILD_DIR="$arg" ;;
  esac
done

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"
TEMPLATE="$ROOT/scripts/publish_web.landing.html"
BUILD_ABS="$(cd "$BUILD_DIR" 2>/dev/null && pwd)" || { echo "Build folder not found: $BUILD_DIR" >&2; exit 1; }

# ---- Check the build -------------------------------------------------------------------
[ -f "$BUILD_ABS/index.html" ] || { echo "No index.html in $BUILD_ABS: is this a Unity WebGL build?" >&2; exit 1; }
[ -d "$BUILD_ABS/Build" ] || { echo "No Build/ folder in $BUILD_ABS: is this a Unity WebGL build?" >&2; exit 1; }
if find "$BUILD_ABS/Build" -type f \( -name '*.br' -o -name '*.gz' \) | grep -q .; then
  echo "Compressed build files (.br/.gz) found. GitHub Pages can't serve them." >&2
  echo "Rebuild with Compression Format = Disabled (or enable Decompression Fallback)." >&2
  exit 1
fi
BIG="$(find "$BUILD_ABS" -type f -size +95M)"
if [ -n "$BIG" ]; then
  echo "These files are over 95 MB (GitHub's limit is 100 MB per file):" >&2
  echo "$BIG" >&2
  exit 1
fi
[ -f "$TEMPLATE" ] || { echo "Missing landing template: $TEMPLATE" >&2; exit 1; }

# ---- Links for the landing page ---------------------------------------------------------
ORIGIN="$(git remote get-url origin)"
REPO_URL="$(printf '%s' "$ORIGIN" | sed -E 's#^git@github\.com:#https://github.com/#; s#\.git$##')"
APK_URL="${APK_URL:-$REPO_URL/releases/latest}"
COMMIT="$(git rev-parse --short HEAD)"
BUILD_DATE="$(date -u +%Y-%m-%d)"
OWNER_REPO="$(printf '%s' "$REPO_URL" | sed -E 's#^https://github\.com/##')"
PAGES_URL="https://${OWNER_REPO%%/*}.github.io/${OWNER_REPO#*/}/"

# ---- Worktree on gh-pages ---------------------------------------------------------------
WT="$(mktemp -d "${TMPDIR:-/tmp}/astra-pages.XXXXXX")"
cleanup() { git -C "$ROOT" worktree remove --force "$WT" >/dev/null 2>&1 || rm -rf "$WT"; }
trap cleanup EXIT

git fetch --quiet origin gh-pages 2>/dev/null || true
if git show-ref --verify --quiet refs/remotes/origin/gh-pages; then
  git worktree add --quiet -B gh-pages "$WT" origin/gh-pages
elif git show-ref --verify --quiet refs/heads/gh-pages; then
  git worktree add --quiet "$WT" gh-pages
else
  git worktree add --quiet --no-checkout --detach "$WT" HEAD
  git -C "$WT" checkout --quiet --orphan gh-pages
  git -C "$WT" rm -rq --cached . >/dev/null 2>&1 || true
fi

# Replace everything on gh-pages with the new build.
find "$WT" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
mkdir -p "$WT/play"
cp -R "$BUILD_ABS/." "$WT/play/"
# Loading notice on Unity's loading screen: it sits inside #unity-loading-bar, so it shows and hides with the bar.
LOAD_NOTE='<div id="astra-load-note" style="position:absolute;top:100%;left:50%;transform:translateX(-50%);margin-top:16px;width:min(86vw,440px);color:#e8eefc;font:14px/1.45 system-ui,-apple-system,Segoe UI,Roboto,sans-serif;text-align:center"><b style="color:#ffd166">Loading about 80\&nbsp;MB.</b> This usually takes 1\&ndash;3 minutes, and up to 15 minutes on a slow connection. Please keep this tab open; the bar fills as it downloads.</div>'
if ! grep -q 'astra-load-note' "$WT/play/index.html"; then
  sed -i.bak -e "s|<div id=\"unity-progress-bar-empty\">|${LOAD_NOTE}<div id=\"unity-progress-bar-empty\">|" "$WT/play/index.html" && rm -f "$WT/play/index.html.bak"
fi
sed -e "s|{{REPO_URL}}|$REPO_URL|g" \
    -e "s|{{APK_URL}}|$APK_URL|g" \
    -e "s|{{COMMIT}}|$COMMIT|g" \
    -e "s|{{BUILD_DATE}}|$BUILD_DATE|g" \
    "$TEMPLATE" > "$WT/index.html"
: > "$WT/.nojekyll"
# Plain files only on gh-pages: Pages does not serve Git LFS objects.
printf '* -filter -diff -merge -text\n' > "$WT/.gitattributes"

git -C "$WT" add -A
if git -C "$WT" diff --cached --quiet; then
  echo "gh-pages already matches this build. Nothing to commit."
else
  git -C "$WT" commit --quiet -m "Publish web build from $COMMIT"
  echo "Committed to local gh-pages: $(git -C "$WT" log -1 --format='%h %s')"
fi

if [ "$PUSH" = true ]; then
  git -C "$WT" push origin gh-pages
  echo "Pushed. Live in about a minute at: $PAGES_URL"
else
  echo "Not pushed. After the team lead says \"approved\", run:"
  echo "  git push origin gh-pages"
  echo "Then the site is at: $PAGES_URL"
fi
