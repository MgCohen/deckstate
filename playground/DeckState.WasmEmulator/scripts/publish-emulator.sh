#!/usr/bin/env bash
#
# Packages the DeckState WASM emulator into a static bundle ready to publish as a
# Claude Artifact. This script does everything deterministic and offline; the actual
# upload to claude.ai is done by Claude's Artifact tool (see the publish-emulator skill).
#
# What it does:
#   1. `dotnet publish -c Release` the emulator (Blazor WebAssembly -> static files).
#   2. Stage wwwroot, dropping the .gz/.br copies a static host won't content-negotiate.
#   3. Rename `_framework` -> `framework` (the Artifact service reserves leading-underscore
#      paths). Blazor loads its runtime via relative imports, so only the folder name and
#      the one <script> tag need patching.
#   4. Emit `files.json`: the file map (with correct content types) for the Artifact tool.
#
# Output: everything lands in `artifact-stage/` next to the project (gitignored).
#
# Usage: bash playground/DeckState.WasmEmulator/scripts/publish-emulator.sh
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CSPROJ="$PROJECT_DIR/DeckState.WasmEmulator.csproj"
PUBLISH_DIR="$PROJECT_DIR/publish"
STAGE_DIR="$PROJECT_DIR/artifact-stage"

echo "==> Publishing (Release) $CSPROJ"
rm -rf "$PUBLISH_DIR"
dotnet publish "$CSPROJ" -c Release -o "$PUBLISH_DIR" >/dev/null
WWWROOT="$PUBLISH_DIR/wwwroot"

echo "==> Staging into $STAGE_DIR"
rm -rf "$STAGE_DIR"
mkdir -p "$STAGE_DIR"
# Copy every published file except the pre-compressed duplicates.
(cd "$WWWROOT" && find . -type f ! -name '*.gz' ! -name '*.br' -exec cp --parents {} "$STAGE_DIR/" \;)

echo "==> Rebasing _framework -> framework (reserved-path workaround)"
mv "$STAGE_DIR/_framework" "$STAGE_DIR/framework"
sed -i 's|_framework/blazor.webassembly.js|framework/blazor.webassembly.js|' "$STAGE_DIR/index.html"
sed -i 's|_framework|framework|g' "$STAGE_DIR/framework/blazor.webassembly.js"

echo "==> Generating files.json"
python3 - "$STAGE_DIR" <<'PY'
import os, json, sys
stage = sys.argv[1]
files = {}
for root, _, names in os.walk(os.path.join(stage, "framework")):
    for name in names:
        rel = os.path.relpath(os.path.join(root, name), stage)
        if name.endswith(".wasm"):
            files[rel] = {"from": rel, "contentType": "application/wasm"}
        elif name.endswith(".js"):
            files[rel] = {"from": rel, "contentType": "text/javascript"}
        else:
            files[rel] = rel
with open(os.path.join(stage, "files.json"), "w") as fh:
    json.dump(files, fh, indent=2)
print(f"    {len(files)} supporting files mapped")
PY

COUNT=$(find "$STAGE_DIR" -type f ! -name files.json | wc -l | tr -d ' ')
SIZE=$(du -sh "$STAGE_DIR" | cut -f1)

cat <<EOF

==> Done.
    Stage dir : $STAGE_DIR
    Page      : $STAGE_DIR/index.html
    Files map : $STAGE_DIR/files.json
    Bundle    : $COUNT files, $SIZE total

Next (Claude, via the Artifact tool): publish with
    file_path = <stage>/index.html
    root      = <stage>
    files     = contents of files.json
    url       = the existing emulator artifact URL (to update in place; see the skill)
EOF
