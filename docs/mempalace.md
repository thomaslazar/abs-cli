# MemPalace

## Overview

This project uses [MemPalace](https://github.com/milla-jovovich/mempalace) as a local,
project-scoped AI memory system. It stores design decisions, architecture rationale, and
project knowledge in a ChromaDB vector database that Claude Code can search via MCP.

## Setup

MemPalace is installed automatically when the dev container builds. The MCP server is
registered in `.devcontainer/post-create.sh`, pointing to `.mempalace/` at the repo root.

No manual setup is needed — it works out of the box inside the dev container.

## Palace Structure

| Wing | Description |
|------|-------------|
| `abs-cli` | Main project wing |

| Room | Content |
|------|---------|
| `architecture` | Language, stack, layers, rules, build targets |
| `cli-design` | Command pattern, resources, I/O, filtering, pipelines |
| `testing` | Testing strategy, Docker integration tests, CI/CD |

Add new rooms as the project grows (e.g. `deployment`, `api-quirks`, `troubleshooting`).

## Seeding from Documentation

The palace data lives in `.mempalace/` which is gitignored. After a fresh checkout or
container rebuild, reseed it from the committed documentation:

```bash
bash scripts/seed-mempalace.sh
```

This script uses `mempalace mine` to index the `docs/` directory into the palace.

## How It's Used

Claude Code has 19 MCP tools available. In practice:

- **Search** — Claude calls `mempalace_search` when you ask about past decisions
- **Store** — Claude calls `mempalace_add_drawer` when significant decisions are made
- **Knowledge graph** — Tracks entity relationships with temporal validity

See the [CLAUDE.md](../CLAUDE.md) MemPalace section for the guidelines Claude follows.

## Manual Operations

```bash
# Check palace status
mempalace status --palace .mempalace

# Search manually
mempalace search "CLI framework" --palace .mempalace

# Re-index docs after changes
bash scripts/seed-mempalace.sh
```
