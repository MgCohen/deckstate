#!/usr/bin/env bash
set -euo pipefail

# Produces a directory that can be copied to Stream Deck's Plugins folder.
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
sample_dir="$(cd "$script_dir/.." && pwd)"
plugin_dir="$sample_dir/artifacts/com.snowprint.deckstate.sdPlugin"

rm -rf "$plugin_dir"
mkdir -p "$plugin_dir/bin" "$plugin_dir/imgs"

dotnet publish "$sample_dir/DeckState.ReadyDeckPlugin/DeckState.ReadyDeckPlugin.csproj" \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --output "$plugin_dir/bin" \
  -p:MSBuildEnableWorkloadResolver=false \
  -m:1

cp "$sample_dir/streamdeck-plugin/com.snowprint.deckstate.sdPlugin/manifest.json" "$plugin_dir/"
cp "$sample_dir/streamdeck-plugin/com.snowprint.deckstate.sdPlugin/imgs/"*.svg "$plugin_dir/imgs/"

echo "Stream Deck plugin package created at: $plugin_dir"
