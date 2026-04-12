# CLAUDE.md

## Git Conventions

- **Conventional Commits** format required: `type: subject`
- Types: `feat`, `fix`, `docs`, `test`, `ci`, `refactor`, `chore`
- Subject line: imperative mood, lowercase, no period, max ~72 chars
- Body (optional): explain *why*, not *what*. Wrap at 72 chars.
- Do NOT include `Co-Authored-By:` lines in commit messages.
- Do NOT add "Generated with Claude Code" or similar attribution lines to PRs, commits, or any auto-generated content.

Examples:
```
feat: add backup create and restore commands
fix: use accessToken instead of legacy user.token
docs: update testing strategy for AOT validation
test: add metadata update assertion to smoke tests
```

## Code Formatting

- `.editorconfig` (from dotnet/runtime) enforces style. CI checks with `dotnet format --verify-no-changes`.
- Run `dotnet format AbsCli.sln` after writing or modifying C# files.
- If formatting check fails in CI, run the format command and commit the fix.
- **No unnecessary blank lines** inside method bodies: no blanks between consecutive `AddCommand`/`AddOption` calls, no blank before `return` after setup calls, no blanks between consecutive variable declarations of the same kind. Keep methods compact — see `AuthorsCommand.cs` as reference.

## MemPalace

- A project-local memory palace is available via MCP at `.mempalace/`
- Wing: `abs-cli`. Rooms: `architecture`, `cli-design`, `testing`, `decisions` (add more as needed)
- **Query the palace first** when you need context on architecture, past decisions, AOT quirks, testing strategy, or any implementation rationale. The palace contains hard-won knowledge from the initial build (e.g., AOT source-gen requirements, macOS cross-compilation, testing architecture decisions).
- Store significant new findings, decisions, and workarounds as drawers
- Use `mempalace_search` for broad queries, `mempalace_kg_query` for entity relationships
