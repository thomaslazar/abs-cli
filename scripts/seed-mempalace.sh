#!/bin/bash
# Seed the MemPalace from committed documentation.
#
# Run this after a fresh checkout or container rebuild to populate
# the project-local palace (.mempalace/) from docs/.
#
# Usage: bash scripts/seed-mempalace.sh [--dry-run]
set -euo pipefail

PALACE_PATH="$(cd "$(dirname "$0")/.." && pwd)/.mempalace"
DOCS_DIR="$(cd "$(dirname "$0")/.." && pwd)/docs"

if [ ! -d "$DOCS_DIR" ]; then
  echo "Error: docs/ directory not found"
  exit 1
fi

DRY_RUN=""
if [ "${1:-}" = "--dry-run" ]; then
  DRY_RUN="--dry-run"
  echo "Dry run — showing what would be indexed"
  echo
fi

echo "Palace: $PALACE_PATH"
echo "Source: $DOCS_DIR"
echo

mempalace --palace "$PALACE_PATH" mine "$DOCS_DIR" \
  --wing abs-cli \
  $DRY_RUN

echo
echo "Done. Run 'mempalace --palace $PALACE_PATH status' to verify."
