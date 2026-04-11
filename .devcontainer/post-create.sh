#!/bin/bash
# Post-create setup for the abs-cli devcontainer.
set -euo pipefail

# --- Claude Code session path symlink ---
# Claude Code indexes sessions by project path. The host path differs from
# the container path (/workspaces/abs-cli), so we symlink.
CONTAINER_KEY=$(pwd | sed 's|/|-|g')
ln -sfn ~/.claude/projects/-Users-ibn-Development-abs-cli \
  ~/.claude/projects/"$CONTAINER_KEY" 2>/dev/null || true

# Ensure directories Claude Code expects exist
mkdir -p ~/.claude/plugins/cache

# Set peon-ping to use the frieren pack (matching the Mac's config)
python3 -c "
import json, os
cfg_path = os.path.expanduser('~/.claude/hooks/peon-ping/config.json')
with open(cfg_path) as f:
    cfg = json.load(f)
cfg['default_pack'] = 'frieren'
cfg['desktop_notifications'] = False
with open(cfg_path, 'w') as f:
    json.dump(cfg, f, indent=2)
" 2>/dev/null || true

# --- MemPalace setup ---
# Register MCP server with Claude Code, pointing to repo-local palace.
# The palace directory is created automatically on first write.
claude mcp add mempalace -- python3 -m mempalace.mcp_server --palace "$(pwd)/.mempalace" 2>/dev/null || true

# --- Caveman setup ---
# Token optimization plugin. Auto-activates via SessionStart hook.
claude plugin marketplace add JuliusBrussee/caveman 2>/dev/null || true
claude plugin install caveman@caveman 2>/dev/null || true

# --- Superpowers setup ---
# Structured development workflow (brainstorming, planning, TDD, debugging, code review).
claude plugin marketplace add obra/superpowers 2>/dev/null || true
claude plugin install superpowers@superpowers-dev 2>/dev/null || true
