# Remove `abs-cli items search` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the `abs-cli items search` subcommand and its backing `ItemsService.SearchAsync` method for the 0.4.0 release. Top-level `abs-cli search` becomes the single entry point to `GET /api/libraries/{id}/search`.

**Architecture:** Hard removal — no transitional alias, no runtime warning. The help-text-level deprecation has been in place through 0.2.x and 0.3.0 per `docs/roadmap.md`. The change touches one command file, one service file, the smoke script, and two doc files. CHANGELOG is owned by the release workflow and is not touched here.

**Tech Stack:** C# / .NET 10 / System.CommandLine 2.0.7 / xUnit / bash smoke test against the local docker-compose ABS dev stack.

---

## File Map

- Modify: `src/AbsCli/Commands/ItemsCommand.cs` — drop `CreateSearchCommand()` and its registration in `Create()`.
- Modify: `src/AbsCli/Services/ItemsService.cs` — drop `SearchAsync(...)`.
- Modify: `docker/smoke-test.sh` — remove `items search` leaf-help entry and the two `items search` assertion blocks.
- Modify: `docs/cli-design.md` — drop the `abs-cli items search` table row.
- Modify: `README.md` — drop the `items search` table row and update the agent-fixes-metadata narrative to use top-level `search`.
- Modify: `docs/roadmap.md` — drop the "Remove `abs-cli items search`" row from "Planned breaking changes"; since it's the only row, drop the section header and lead-in too.

## Testing strategy note

This is the removal of a duplicate subcommand. There is no useful unit-level "failing test first" — current unit tests do not assert the *presence* of `items search`, and adding a unit test that asserts its *absence* leaves long-term noise. The real regression gate is the smoke test (which today exercises both `items search` and top-level `search`) plus a manual `--help` eyeball. Verification at the end of the plan runs unit tests, `dotnet format`, the full smoke against the docker dev stack, and the manual `--help` checks.

Per `CLAUDE.md`, every `git commit` step below is gated on user approval — do not run `git commit` without asking first.

---

### Task 1: Create implementation branch and commit spec + plan

**Files:**
- Existing (untracked): `docs/specs/2026-05-11-remove-items-search-subcommand.md`
- Existing (untracked): `docs/plans/2026-05-11-remove-items-search-subcommand.md` (this file)

- [ ] **Step 1: Confirm working tree state**

Run: `git status`
Expected: branch `main`; the only untracked files are the new spec and plan. No other uncommitted changes.

If anything else is uncommitted, stop and resolve before continuing.

- [ ] **Step 2: Create the feature branch**

Run: `git switch -c feat/remove-items-search`
Expected: `Switched to a new branch 'feat/remove-items-search'`.

- [ ] **Step 3: Stage spec and plan**

Run:
```bash
git add docs/specs/2026-05-11-remove-items-search-subcommand.md \
        docs/plans/2026-05-11-remove-items-search-subcommand.md
```

- [ ] **Step 4: Commit (ASK USER FIRST)**

Run:
```bash
git commit -m "docs: spec and plan for removing 'items search' subcommand"
```

---

### Task 2: Pre-flight grep guard

Re-verify, against the live tree, that nothing outside the documented touch-list references the symbols we are about to delete. Catches anything that may have landed after the spec was written.

- [ ] **Step 1: Confirm only one caller of `ItemsService.SearchAsync`**

Run: `grep -rn "\.SearchAsync\b" --include="*.cs" src tests tools`
Expected: hits in `src/AbsCli/Commands/SearchCommand.cs` (uses `SearchService.SearchAsync` — stays), `src/AbsCli/Commands/ItemsCommand.cs` (uses `ItemsService.SearchAsync` inside `CreateSearchCommand` — the one we are deleting), and the two service declarations themselves in `src/AbsCli/Services/ItemsService.cs` and `src/AbsCli/Services/SearchService.cs`.

If any other file (especially a test) references `ItemsService.SearchAsync`, stop and re-plan.

- [ ] **Step 2: Confirm no source/test mentions of the string "items search"**

Run: `grep -rn "items search" --include="*.cs" src tests tools`
Expected: no output.

If any hits surface, stop and re-plan — the spec assumed no test asserts on `items search` and that assumption is the basis for the lack of unit-test edits.

- [ ] **Step 3: Inventory the doc/script hits we DO plan to touch**

Run: `grep -rn "items search" --include="*.sh" --include="*.md" .`
Expected hits, and only these:
- `docker/smoke-test.sh` — leaf-help loop and two assertion blocks
- `docs/cli-design.md` — one table row
- `docs/roadmap.md` — one breaking-change row
- `docs/specs/2026-05-11-remove-items-search-subcommand.md` — the spec (do not edit)
- `docs/plans/2026-05-11-remove-items-search-subcommand.md` — this plan (do not edit)
- `docs/plans/2026-04-12-v0.2.0-implementation.md` — historical plan, leave alone
- `docs/plans/2026-04-17-help-response-shapes-and-derived-notices.md` — historical plan, leave alone

Any other hit means something landed that the spec did not anticipate; stop and re-plan.

---

### Task 3: Remove `items search` from `ItemsCommand.cs`

**Files:**
- Modify: `src/AbsCli/Commands/ItemsCommand.cs`

- [ ] **Step 1: Remove the registration line in `Create()`**

Edit `src/AbsCli/Commands/ItemsCommand.cs`. In the `Create()` method (top of the class), find the line (currently line 15):
```csharp
        command.Subcommands.Add(CreateSearchCommand());
```
Delete that single line. The surrounding registrations should now read:
```csharp
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateGetCommand());
        command.Subcommands.Add(CreateUpdateCommand());
        command.Subcommands.Add(CreateBatchUpdateCommand());
        command.Subcommands.Add(CreateBatchGetCommand());
        command.Subcommands.Add(CreateScanCommand());
        command.Subcommands.Add(CreateCoverCommand());
```

- [ ] **Step 2: Remove the `CreateSearchCommand()` method**

In the same file, delete the entire `private static Command CreateSearchCommand()` method (currently lines ~105–150) — from the method signature through its closing brace. It is the method whose body builds `queryOption`/`libraryOption`/`limitOption`, calls `new ItemsService(client)`, and invokes `service.SearchAsync(...)`.

- [ ] **Step 3: Leave `using AbsCli.Models;` alone**

Other parts of `ItemsCommand.cs` still reference types from `AbsCli.Models` (`LibraryItemMinified`, `PaginatedResponse`, `UpdateMediaResponse`, `BatchUpdateResponse`, `BatchGetResponse`, `ScanResult`, and the cover-related response types). Do not remove the `using AbsCli.Models;` directive. (If a build warning surprises you here, investigate before deleting.)

- [ ] **Step 4: Build**

Run: `dotnet build src/AbsCli/AbsCli.csproj`
Expected: build succeeds, no new warnings.

- [ ] **Step 5: Format**

Run: `dotnet format AbsCli.sln`
Expected: exit 0.

- [ ] **Step 6: Commit (ASK USER FIRST)**

Run:
```bash
git add src/AbsCli/Commands/ItemsCommand.cs
git commit -m "refactor: drop 'items search' subcommand from items command tree"
```

---

### Task 4: Remove `ItemsService.SearchAsync`

**Files:**
- Modify: `src/AbsCli/Services/ItemsService.cs`

- [ ] **Step 1: Delete the `SearchAsync` method**

Edit `src/AbsCli/Services/ItemsService.cs`. Find and delete the entire method (currently lines 37–43):
```csharp
    public async Task<SearchResult> SearchAsync(string libraryId, string query, int? limit)
    {
        var qs = HttpUtility.ParseQueryString("");
        qs["q"] = query;
        if (limit.HasValue) qs["limit"] = limit.Value.ToString();
        return await _client.GetAsync(ApiEndpoints.LibrarySearch(libraryId) + "?" + qs, AppJsonContext.Default.SearchResult);
    }
```
Delete the preceding blank line as well so there isn't a double-blank scar between the surviving methods.

- [ ] **Step 2: Leave the `using` directives alone**

`ItemsService.cs` still uses `System.Web` (for `HttpUtility.ParseQueryString` in `ListAsync`), `AbsCli.Api` (for `AbsApiClient`, `ApiEndpoints`), and `AbsCli.Models` (for `PaginatedResponse`, `LibraryItemMinified`, `UpdateMediaResponse`, `BatchUpdateResponse`, `BatchGetResponse`, `ScanResult`). Do not remove any of the existing `using` directives.

- [ ] **Step 3: Build**

Run: `dotnet build src/AbsCli/AbsCli.csproj`
Expected: build succeeds. Any compile error here is a surprise — investigate before continuing.

- [ ] **Step 4: Run unit tests**

Run: `dotnet test`
Expected: all tests pass. (Pre-flight Task 2 Step 2 already confirmed no test asserts on `items search` or `ItemsService.SearchAsync`.)

- [ ] **Step 5: Format**

Run: `dotnet format AbsCli.sln`
Expected: exit 0.

- [ ] **Step 6: Commit (ASK USER FIRST)**

Run:
```bash
git add src/AbsCli/Services/ItemsService.cs
git commit -m "refactor: drop unused ItemsService.SearchAsync"
```

---

### Task 5: Update the smoke test

**Files:**
- Modify: `docker/smoke-test.sh`

- [ ] **Step 1: Remove `"items search"` from the leaf-help loop**

In `docker/smoke-test.sh`, find the multi-line `for cmd in ...` array (currently around line 102):
```bash
for cmd in "login" "config get" "config set" \
           "libraries list" "libraries get" "libraries scan" \
           "items list" "items get" "items search" \
           "items update" "items batch-update" "items batch-get" "items scan" \
```
Remove the `"items search"` token. The third line of the array should now be:
```bash
           "items list" "items get" \
```
Keep the trailing backslash and the indentation. The `items cover` line further down stays as-is.

- [ ] **Step 2: Delete the two `items search` assertion blocks**

In the same file, find the block (currently lines 204–211):
```bash
# Search — find a known book by title
output=$($CLI items search --query "Final Empire" 2>/dev/null)
assert_json_key "items search has book key" "book" "$output"
assert_json_expr "items search finds Final Empire" "len(d.get('book',[]))>0" "$output"

# Search — no results
output=$($CLI items search --query "zzz_nonexistent_xyz" 2>/dev/null)
assert_json_expr "items search empty for garbage query" "len(d.get('book',[]))==0" "$output"
```
Delete the entire block, including both `# Search — ...` comment lines and the blank line between them. The surrounding lines (`assert_json_key "items get has media" ...` immediately above, and `# Update metadata — change title ...` immediately below) should now sit next to each other with a single blank line between them.

Top-level `abs-cli search` coverage already exists further down (the "Storm Front" / "Mistborn" block around line 492) and is not touched.

- [ ] **Step 3: Scan for stragglers**

Run: `grep -n "items search" docker/smoke-test.sh`
Expected: no output.

- [ ] **Step 4: Commit (ASK USER FIRST)**

Run:
```bash
git add docker/smoke-test.sh
git commit -m "test: drop 'items search' smoke assertions"
```

---

### Task 6: Update README, CLI reference, and roadmap

**Files:**
- Modify: `docs/cli-design.md`
- Modify: `README.md`
- Modify: `docs/roadmap.md`

- [ ] **Step 1: Remove the `cli-design.md` row**

In `docs/cli-design.md`, find and delete the table row (currently line 20):
```
| `abs-cli items search --query <text>` | `GET /api/libraries/{id}/search?q=` | Search items in library |
```
The corresponding top-level row (`| \`abs-cli search --query <text>\` | ...`) further down in the same file stays.

- [ ] **Step 1b: Remove the README command-reference row**

In `README.md`, find and delete the table row (currently line 199):
```
| `items search --query <text>` | Search items in a library |
```
The top-level `search` row a few rows below (`| \`search --query <text>\` | Search across a library |`) stays.

- [ ] **Step 1c: Update the README agent-fixes-metadata example**

In `README.md`, find the bullet (currently line 163):
```
2. Searches for affected items (`abs-cli items search`, `abs-cli items list --limit 200` with pagination for broader checks)
```
Replace `abs-cli items search` with `abs-cli search`. The rest of the sentence (including the `abs-cli items list ...` reference) stays unchanged.

- [ ] **Step 2: Remove the `roadmap.md` "Planned breaking changes" section**

In `docs/roadmap.md`, the section is the last block of the file (currently lines 129–135). Since the section had one row and we are removing it, drop the entire section:

Delete these lines (currently 129–135), including any trailing blank line at the end of file that may follow:
```
## Planned breaking changes

Scheduled for a future minor release with a prior deprecation window.

| Change | Reason |
|--------|--------|
| Remove `abs-cli items search` | Functional duplicate of top-level `abs-cli search` — same endpoint (`/api/libraries/:id/search`), same response shape. Kept as alias through v0.2.x with a note in its help; remove in the next minor bump. |
```

The file should now end with the previous section (the "Not in scope" table that ends with the `items files / items progress` row). Make sure the file ends with exactly one trailing newline.

- [ ] **Step 3: Verify all three files**

Run:
```bash
grep -n "items search" docs/cli-design.md docs/roadmap.md README.md
```
Expected: no output.

Run:
```bash
tail -5 docs/roadmap.md
```
Expected: the last meaningful line is the "Not in scope" table's `items files / items progress` row; no `Planned breaking changes` header remains.

- [ ] **Step 4: Commit (ASK USER FIRST)**

Run:
```bash
git add docs/cli-design.md docs/roadmap.md README.md
git commit -m "docs: remove 'items search' from README, CLI reference, and roadmap"
```

---

### Task 7: End-to-end verification

The real test gate. Do **not** open the PR until every check below passes.

- [ ] **Step 1: Unit tests**

Run: `dotnet test`
Expected: green.

- [ ] **Step 2: Formatting check**

Run: `dotnet format AbsCli.sln --verify-no-changes`
Expected: exit 0, no diff.

- [ ] **Step 3: Manual `--help` checks**

Build and run the CLI directly:
```bash
dotnet run --project src/AbsCli -- items --help
```
Expected: subcommand list contains `list`, `get`, `update`, `batch-update`, `batch-get`, `scan`, `cover` — and does **not** contain `search`.

```bash
dotnet run --project src/AbsCli -- items search --query x
```
Expected: nonzero exit; System.CommandLine prints an "unrecognized command or argument" error mentioning `search`.

```bash
dotnet run --project src/AbsCli -- search --help
```
Expected: top-level search help still renders, with `Description`, `Search behavior`, `Fields searched`, `Examples`, and `Response shape` sections unchanged.

- [ ] **Step 4: Bring up the docker dev stack**

```bash
cd docker && docker compose up -d
```

Resolve the container IP (the `host.docker.internal` default does not work from inside the dev container):
```bash
docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}'
```
Note the IP for the next steps.

If the stack was freshly created, seed it:
```bash
ABS_URL=http://<container-ip>:80 bash docker/seed.sh
```

- [ ] **Step 5: Run the full smoke**

```bash
ABS_URL=http://<container-ip>:80 bash docker/smoke-test.sh
```
Expected: every assertion passes. The leaf-help loop now iterates one fewer command (`items search` is gone), the two `items search` assertion blocks no longer run, and the existing top-level `search` assertions (Storm Front / Mistborn) still pass.

Do **not** mark "smoke test passed" in the PR body until this run actually succeeds.

---

### Task 8: Open the PR

- [ ] **Step 1: Push the branch**

Run:
```bash
git push -u origin feat/remove-items-search
```

- [ ] **Step 2: Create the PR (ASK USER FIRST)**

Run:
```bash
gh pr create --title "refactor: remove deprecated 'items search' subcommand" --body "$(cat <<'EOF'
## Summary
- Removes `abs-cli items search` and its backing `ItemsService.SearchAsync`. Top-level `abs-cli search` becomes the single way to hit `GET /api/libraries/{id}/search`.
- The help-text-level deprecation has been in place through v0.2.x and v0.3.0 per `docs/roadmap.md` ("Planned breaking changes"), so this lands as a hard removal in 0.4.0.
- Smoke (`docker/smoke-test.sh`) and docs (`cli-design.md`, `roadmap.md`) updated to match.

## Test plan
- [x] `dotnet test`
- [x] `dotnet format AbsCli.sln --verify-no-changes`
- [x] `bash docker/smoke-test.sh` against the local docker-compose dev stack
- [x] `abs-cli items --help` no longer lists `search`; `abs-cli items search ...` returns the standard "unknown command" error; `abs-cli search ...` is unchanged.
EOF
)"
```

- [ ] **Step 3: Present the PR URL**

After `gh pr create` returns, paste the URL as a plain line so the user can click it (per `CLAUDE.md`).

---

## Self-Review

**Spec coverage:**
- Source removal — Task 3 (ItemsCommand), Task 4 (ItemsService). ✔
- Smoke updates (leaf-list + two assertion blocks) — Task 5. ✔
- Doc updates (cli-design row, roadmap row) — Task 6. ✔
- "Not touched" items in the spec (CHANGELOG, `SearchResult`/`AppJsonContext.Default.SearchResult`/`ResponseExamples.g.cs`/`SelfTestCommand.cs`, `HelpOutputTests.cs`, top-level `SearchCommand`/`SearchService`) — no task touches them, and Task 2 verifies nothing else needs to. ✔
- Verification (unit, format, manual `--help`, smoke) — Task 7. ✔
- Risk mitigation: "anyone scripting against items search" — already mitigated by the shipped deprecation; "hidden coupling we missed" — Task 2 is the guard. ✔

**Placeholders:** none. Every step has a concrete command, file, or edit.

**Type/symbol consistency:** the only symbols named across tasks are `ItemsService.SearchAsync`, `SearchResult`, `SearchService`, and the command names `items search` / `search` — used identically throughout.
