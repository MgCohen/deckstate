#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
plugin="$root/streamdeck-plugin/com.snowprint.deckstate.session-board.sdPlugin"
output="$root/artifacts/com.snowprint.deckstate.session-board.sdPlugin"
project="$root/DeckState.SessionBoardPlugin/DeckState.SessionBoardPlugin.csproj"

rm -rf "$output"
mkdir -p "$output/bin" "$output/imgs"
cp "$plugin/manifest.json" "$output/"
cp -R "$plugin/imgs/." "$output/imgs/"
dotnet build "$project" -c Release --self-contained false -o "$output/bin" -p:MSBuildEnableWorkloadResolver=false -m:1
