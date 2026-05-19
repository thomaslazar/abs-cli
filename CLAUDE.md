# CLAUDE.md

## Main rule
be brief

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

## Pre-PR verification

- Run `docker/smoke-test.sh` against the local docker-compose dev stack before opening any PR. Unit tests and `self-test` are not enough — many regressions only surface in the live HTTP path.
- The compose stack lives at `docker/docker-compose.yml`; bring it up with `cd docker && docker compose up -d`. Resolve the container IP via `docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}'` and run the smoke as `ABS_URL=http://<container-ip>:80 bash docker/smoke-test.sh` — the `host.docker.internal` default does not work from inside the dev container.
- Seed first if the stack is freshly created: `ABS_URL=http://<container-ip>:80 bash docker/seed.sh`.
- Only mark "smoke test passed" in a PR description after actually running it. Do not copy the checkbox forward unverified.

## Code Formatting

- `.editorconfig` (from dotnet/runtime) enforces style. CI checks with `dotnet format --verify-no-changes`.
- Run `dotnet format AbsCli.sln` after writing or modifying C# files.
- If formatting check fails in CI, run the format command and commit the fix.
- **No unnecessary blank lines** inside method bodies: no blanks between consecutive `AddCommand`/`AddOption` calls, no blank before `return` after setup calls, no blanks between consecutive variable declarations of the same kind. Keep methods compact — see `AuthorsCommand.cs` as reference.

## Command implementation conventions

- **Permission tagging.** Every command whose underlying ABS endpoint requires a non-default permission MUST call `command.AddPermissionRequired("<token>")` immediately after construction, where `<token>` is one of `admin`, `update`, `upload`, `download`, `delete`. Commands callable by any authenticated user (reads, lookups) get no call. The token must agree with the controller's permission check in `temp/audiobookshelf/server/controllers/`.
- **Permission hint mirroring.** When the underlying service method's HTTP call needs a `permissionHint`, the hint string MUST match the tag: tag `update` ↔ hint `"'update' permission"`; tag `delete` ↔ hint `"'delete' permission"`; tag `upload` ↔ hint `"'upload' permission"`; tag `admin` ↔ hint `"admin permission"` (NO quotes around `admin` — `admin` is a user *type* in ABS's model, not a flag in the `user.permissions` object; the quoted forms name literal flag keys like `permissions.update`). The help-section tag and the 403 error message should always agree.
- **README Commands table.** Any PR that adds, renames, or removes a CLI verb, OR adds/removes a user-visible flag on an existing command, MUST update the Commands table in `README.md` in the same change.

## ABS Source Reference

- The ABS server source is the authoritative reference for API behavior, request/response shapes, and routing — `https://api.audiobookshelf.org` is **stale** and unreliable.
- Expected location: `temp/audiobookshelf/` (gitignored). If missing, clone the currently supported version before referencing API code:
  ```bash
  # Supported version is set in src/AbsCli/Api/AbsApiClient.cs (MinSupportedVersion / MaxTestedVersion)
  git clone --depth 1 --branch v2.35.0 https://github.com/advplyr/audiobookshelf.git temp/audiobookshelf
  ```
- Replace the version tag with whatever `MaxTestedVersion` is currently set to.
- Use this checkout to verify endpoints, controllers, request/response shapes, and permission checks before designing or changing CLI commands.

