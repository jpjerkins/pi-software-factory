#!/usr/bin/env bash
# Publish the factory CLI (framework-dependent, linux-arm64) to the live folder.
# Usage: scripts/publish.sh [output-dir]   (default: ~/apps/factory)
# Overwrites only; never deletes or cleans the output folder.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="${1:-$HOME/apps/factory}"

cd "$repo_root"
dotnet publish src/Factory.Cli -c Release -r linux-arm64 --self-contained false -o "$out" -v q
