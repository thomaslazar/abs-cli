# CLAUDE.md

## Git Conventions

- Do NOT include `Co-Authored-By:` lines in commit messages.
- Do NOT add "Generated with Claude Code" or similar attribution lines to PRs, commits, or any auto-generated content.

## MemPalace

- A project-local memory palace is available via MCP at `.mempalace/`
- Wing: `abs-cli`. Rooms: `architecture`, `cli-design`, `testing`, `decisions` (add more as needed)
- **Query the palace first** when you need context on architecture, past decisions, AOT quirks, testing strategy, or any implementation rationale. The palace contains hard-won knowledge from the initial build (e.g., AOT source-gen requirements, macOS cross-compilation, testing architecture decisions).
- Store significant new findings, decisions, and workarounds as drawers
- Use `mempalace_search` for broad queries, `mempalace_kg_query` for entity relationships
