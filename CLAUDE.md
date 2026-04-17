# CLAUDE.md

## Git Conventions

- **Always ask the user before committing.** Do not commit automatically after making changes.
- **Conventional Commits** format required: `type: subject`
- Types: `feat`, `fix`, `docs`, `test`, `ci`, `refactor`, `chore`
- Subject line: imperative mood, lowercase, no period, max ~72 chars
- Body (optional): explain *why*, not *what*. Wrap at 72 chars.
- Do NOT include `Co-Authored-By:` lines in commit messages.
- Do NOT add "Generated with Claude Code" or similar attribution lines to PRs, commits, or any auto-generated content.
- After creating a pull request, always present the PR URL as a clickable link (plain URL on its own line or markdown link format) so the user can open it directly.

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

## ABS Source Reference

- The ABS server source is the authoritative reference for API behavior, request/response shapes, and routing — `https://api.audiobookshelf.org` is **stale** and unreliable.
- Expected location: `temp/audiobookshelf/` (gitignored). If missing, clone the currently supported version before referencing API code:
  ```bash
  # Supported version is set in src/AbsCli/Api/AbsApiClient.cs (MinSupportedVersion / MaxTestedVersion)
  git clone --depth 1 --branch v2.33.1 https://github.com/advplyr/audiobookshelf.git temp/audiobookshelf
  ```
- Replace the version tag with whatever `MinSupportedVersion` is currently set to.
- Use this checkout to verify endpoints, controllers, request/response shapes, and permission checks before designing or changing CLI commands.

