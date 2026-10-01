# Cover Permissions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `items cover` permissions match ABS: split `--server-path` into a new `items cover link` (PATCH, `update`), tag `items cover set` (POST) as `update, upload`, and prove all of it in smoke.

**Architecture:** `AddPermissionRequired` gains multi-token support; `CoversService` hints mirror the tags; a new `items cover link` command replaces `cover set --server-path`; `seed.sh` adds `uploadonlyuser`; smoke migrates the server-path block and adds 403/positive permission checks.

**Tech Stack:** C# / .NET 10, System.CommandLine, xunit v3, bash smoke tests, docker compose.

**Spec:** `docs/specs/2026-10-01-cover-permissions-design.md`

**Branch:** `fix/cover-permissions` (checked out; spec + this plan uncommitted — they go into Task 1's commit).

**Environment notes (all tasks):**
- Commits: Conventional Commits, lowercase imperative, NO Co-Authored-By/attribution lines. Run `dotnet format AbsCli.sln` after C# edits; `--verify-no-changes` must pass. No unnecessary blank lines inside method bodies (see `AuthorsCommand.cs`).
- ABS source reference: `temp/audiobookshelf/` (v2.37.1).
- Fresh-stack smoke recipe (smoke is NOT idempotent):
  ```bash
  export ABS_URL=http://docker-audiobookshelf-1.orb.local:80
  docker compose -f docker/docker-compose.yml down -v
  docker compose -f docker/docker-compose.yml up -d
  for i in $(seq 1 90); do curl -sf $ABS_URL/healthcheck >/dev/null && break; sleep 2; done
  bash docker/seed.sh > temp/seed.log 2>&1
  bash docker/smoke-test.sh > temp/smoke.log 2>&1; echo rc=$?
  grep FAIL temp/smoke.log; tail -3 temp/smoke.log
  ```
- `docker/smoke-test.sh` runs under `set -euo pipefail` — guard fallible substitutions. Every test block has a one-line `# Test: <action>; expect <result>.` comment directly above it; keep that convention for new/changed blocks.

---

### Task 1: Multi-token permission tags

**Files:**
- Modify: `src/AbsCli/Commands/HelpExtensions.cs:28-29`
- Test: `tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs`

- [ ] **Step 1: Failing test**

Add to `HelpExtensionsTests`:

```csharp
    [Fact]
    public void PermissionRequired_MultipleTokens_RenderCommaSeparated()
    {
        var cmd = new Command("demo", "Demo command");
        cmd.AddPermissionRequired("update", "upload");
        var output = RenderHelp(cmd);
        Assert.Contains("Permission required:", output);
        Assert.Contains("update, upload", output);
    }
```

Run: `dotnet test tests/AbsCli.Tests --filter "FullyQualifiedName~HelpExtensionsTests"` → build error (no 2-arg overload).

- [ ] **Step 2: Implement**

```csharp
    public static void AddPermissionRequired(this Command command, params string[] permissions)
        => command.AddHelpSection("Permission required", HelpSectionPosition.Top, string.Join(", ", permissions));
```

- [ ] **Step 3: Verify** — `dotnet test` → all pass (single-token callers unchanged).

- [ ] **Step 4: Commit**

```bash
dotnet format AbsCli.sln
git add docs/specs/2026-10-01-cover-permissions-design.md docs/plans/2026-10-01-cover-permissions.md \
  src/AbsCli/Commands/HelpExtensions.cs tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs
git commit -m "feat: allow multiple tokens in permission-required help tags"
```

---

### Task 2: Split `items cover link` out of `cover set`

**Files:**
- Modify: `src/AbsCli/Commands/ItemsCommand.cs` (`CreateCoverCommand`, `CreateCoverSetCommand`, new `CreateCoverLinkCommand`)
- Modify: `src/AbsCli/Services/CoversService.cs` (hints on `SetByUrlAsync`, `UploadFromFileAsync`, `LinkExistingAsync`)
- Test: `tests/AbsCli.Tests/Commands/ItemsCoverCommandTests.cs`

- [ ] **Step 1: Failing tests**

In `ItemsCoverCommandTests`:
- Replace `Cover_TopLevel_Help_ListsThreeVerbs` with:

```csharp
    [Fact]
    public void Cover_TopLevel_Help_ListsFourVerbs()
    {
        var output = RenderHelp("items", "cover");
        Assert.Contains("set", output);
        Assert.Contains("link", output);
        Assert.Contains("get", output);
        Assert.Contains("remove", output);
    }
```

- Replace `CoverSet_Help_ListsAllThreeSourceFlags` and `CoverSet_Help_DocumentsServerPathRestriction` with:

```csharp
    [Fact]
    public void CoverSet_Help_ListsUrlAndFileOnly()
    {
        var output = RenderHelp("items", "cover", "set");
        Assert.Contains("--url", output);
        Assert.Contains("--file", output);
        Assert.DoesNotContain("--server-path", output);
    }

    [Fact]
    public void CoverSet_Help_RequiresUpdateAndUpload()
    {
        Assert.Contains("Permission required:\n  update, upload", RenderHelp("items", "cover", "set"));
    }

    [Fact]
    public void CoverLink_Help_RequiresUpdateAndDocumentsPathRules()
    {
        var output = RenderHelp("items", "cover", "link");
        Assert.Contains("Permission required:\n  update", output);
        Assert.DoesNotContain("upload", output.Split("Options:")[0]);
        Assert.Contains("--path", output);
        Assert.Contains("library files", output);
        Assert.Contains("Invalid cover path", output);
        Assert.Contains("/metadata/items/", output);
    }

    [Fact]
    public void CoverLink_Help_ShowsResponseShape()
    {
        var output = RenderHelp("items", "cover", "link");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"cover\"", output);
    }
```

(If the rendered help uses `\r\n` or different indentation, adapt the `Permission required:\n  ...` literal to match how `UploadCommandTests.cs:109` asserts it.) Run the cover tests → FAIL.

- [ ] **Step 2: Implement**

`CreateCoverCommand`: register `CreateCoverLinkCommand()` after `CreateCoverSetCommand()`; description `"Manage book covers (apply, link, fetch, remove)"`.

`CreateCoverSetCommand` becomes:

```csharp
    private static Command CreateCoverSetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var urlOption = new Option<string?>("--url") { Description = "Cover image URL — ABS server downloads it" };
        var fileOption = new Option<string?>("--file") { Description = "Local cover image file to upload" };
        var command = new Command("set", "Apply a cover to a library item by URL or local file") { idOption, urlOption, fileOption };
        command.AddPermissionRequired("update", "upload");
        command.AddExamples(
            "abs-cli items cover set --id \"li_abc123\" --url \"https://example.com/cover.jpg\"",
            "abs-cli items cover set --id \"li_abc123\" --file ./cover.jpg");
        command.AddResponseExample<CoverApplyResponse>();
        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var url = parseResult.GetValue(urlOption);
            var file = parseResult.GetValue(fileOption);
            if (string.IsNullOrEmpty(url) == string.IsNullOrEmpty(file))
            {
                _logger.Error("Specify exactly one of --url, --file");
                Environment.Exit(1);
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new CoversService(client);
            CoverApplyResponse result;
            if (!string.IsNullOrEmpty(url))
            {
                result = await service.SetByUrlAsync(id, url);
            }
            else
            {
                if (!File.Exists(file))
                {
                    _logger.Error($"File not found: {file}");
                    Environment.Exit(1);
                }
                result = await service.UploadFromFileAsync(id, file!);
            }
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.CoverApplyResponse);
        });
        return command;
    }
```

New `CreateCoverLinkCommand` (place right after `CreateCoverSetCommand`):

```csharp
    private static Command CreateCoverLinkCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var pathOption = new Option<string>("--path") { Description = "Path of an image already among the item's library files", Required = true };
        var command = new Command("link", "Use an image already among the item's files as its cover") { idOption, pathOption };
        command.AddPermissionRequired("update");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "--path outside the item's libraryFiles → 500 \"Invalid cover path\" (e.g. a --file cover under /metadata).",
            "The image is copied to /metadata/items/<id>/ (unless storeCoverWithItem); response cover is the copy.");
        command.AddExamples(
            "abs-cli items cover link --id \"li_abc123\" --path \"/audiobooks/Author/Title/cover.jpg\"");
        command.AddResponseExample<CoverApplyResponse>();
        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var path = parseResult.GetValue(pathOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new CoversService(client);
            var result = await service.LinkExistingAsync(id, path);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.CoverApplyResponse);
        });
        return command;
    }
```

Copy the exact Notes wording currently on `cover set` (check `git show HEAD:src/AbsCli/Commands/ItemsCommand.cs`) if it differs from the above, adjusting `--server-path` → `--path`.

`CoversService.cs`:
- `SetByUrlAsync` and `UploadFromFileAsync`: hint `"'update' and 'upload' permission"`.
- `LinkExistingAsync`: pass `permissionHint: "'update' permission"`; doc comment → `Apply a cover from a file already among the item's libraryFiles (ABS validates and copies it into the cover dir).`

- [ ] **Step 3: Verify** — cover tests PASS; `dotnet test` all pass; `dotnet run --project src/AbsCli -- items cover link --help` shows the tag, Notes, example; `dotnet run --project src/AbsCli -- self-test` rc 0.

- [ ] **Step 4: Commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/ItemsCommand.cs src/AbsCli/Services/CoversService.cs tests/AbsCli.Tests/Commands/ItemsCoverCommandTests.cs
git commit -m "feat: move cover set --server-path to items cover link" \
  -m "BREAKING CHANGE: items cover set no longer accepts --server-path; use items cover link --path. cover set now requires update and upload."
```

---

### Task 3: Seed user and smoke coverage

**Files:**
- Modify: `docker/seed.sh` (after the read-only user block, before `# --- Upload test audiobooks ---`)
- Modify: `docker/smoke-test.sh` (Cover section "Mode 2" block)

- [ ] **Step 1: Seed `uploadonlyuser`**

Insert after the read-only user's `curl ... || true`:

```bash
# Create upload-only test user (upload but no update) so smoke tests can
# show POST /api/items/:id/cover needs 'update' as well as 'upload'.
echo "Creating upload-only test user..."
curl -sf -X POST "$ABS_URL/api/users" \
    -H "$AUTH" \
    -H 'Content-Type: application/json' \
    -d '{
        "username": "uploadonlyuser",
        "password": "uploadonlypass",
        "type": "user",
        "isActive": true,
        "permissions": {
            "download": true,
            "update": false,
            "delete": false,
            "upload": true,
            "accessAllLibraries": true,
            "accessAllTags": true,
            "accessExplicitContent": true
        }
    }' > /dev/null 2>&1 || true
```

Also update any seed summary line that lists test credentials (`grep -n "Test credentials" docker/seed.sh`) to include `uploadonlyuser/uploadonlypass`.

- [ ] **Step 2: Migrate the Mode 2 block**

In the block headed `# --- Mode 2: --server-path (PATCH, link to an item library file) ---`:
- Header → `# --- Mode 2: items cover link (PATCH, link to an item library file) ---`.
- Every `items cover set --id "$FIRST_ITEM_ID" --server-path` → `items cover link --id "$FIRST_ITEM_ID" --path`.
- Labels/`# Test:` comments: `items cover set --server-path` → `items cover link`.

- [ ] **Step 3: Add permission checks**

Insert at the end of the Mode 2 block (after the "rejects path outside item libraryFiles" check, before `# --- Mode 3`). `LIBFILE_COVER_PATH` and `COVER_FILE` are set earlier in the section:

```bash
# Permission checks: POST (set) needs update AND upload; PATCH (link) needs update only.
if [ -n "$LIBFILE_COVER_PATH" ]; then
    # Test: link the library-file cover as testuser (update, no upload); expect success=True.
    abs_login testuser testpass
    output=$($CLI items cover link --id "$FIRST_ITEM_ID" --path "$LIBFILE_COVER_PATH" 2>/dev/null || echo "{}")
    abs_login root root
    assert_json_expr "items cover link as testuser (update only) succeeds" "d.get('success')==True" "$output"
    $CLI items cover remove --id "$FIRST_ITEM_ID" > /dev/null 2>&1 || true
fi

# Test: link a cover as readonlyuser (no update); expect a 403 naming 'update' permission.
abs_login readonlyuser readonlypass
perm_err=$($CLI items cover link --id "$FIRST_ITEM_ID" --path "${LIBFILE_COVER_PATH:-/nonexistent.png}" 2>&1 >/dev/null) && perm_rc=0 || perm_rc=$?
abs_login root root
if [ "$perm_rc" -ne 0 ] && echo "$perm_err" | grep -q "'update' permission"; then
    pass "items cover link as readonlyuser hits 'update' permission denial"
else
    fail "items cover link as readonlyuser hits 'update' permission denial" "rc=$perm_rc err=${perm_err:0:200}"
fi

# Test: set a cover from file as testuser (update, no upload); expect a 403 naming 'update' and 'upload' permission.
abs_login testuser testpass
perm_err=$($CLI items cover set --id "$FIRST_ITEM_ID" --file "$COVER_FILE" 2>&1 >/dev/null) && perm_rc=0 || perm_rc=$?
abs_login root root
if [ "$perm_rc" -ne 0 ] && echo "$perm_err" | grep -q "'update' and 'upload' permission"; then
    pass "items cover set --file as testuser (no upload) is denied"
else
    fail "items cover set --file as testuser (no upload) is denied" "rc=$perm_rc err=${perm_err:0:200}"
fi

# Test: set a cover from file as uploadonlyuser (upload, no update); expect a 403 naming 'update' and 'upload' permission.
abs_login uploadonlyuser uploadonlypass
perm_err=$($CLI items cover set --id "$FIRST_ITEM_ID" --file "$COVER_FILE" 2>&1 >/dev/null) && perm_rc=0 || perm_rc=$?
abs_login root root
if [ "$perm_rc" -ne 0 ] && echo "$perm_err" | grep -q "'update' and 'upload' permission"; then
    pass "items cover set --file as uploadonlyuser (no update) is denied"
else
    fail "items cover set --file as uploadonlyuser (no update) is denied" "rc=$perm_rc err=${perm_err:0:200}"
fi
```

Check: `grep -n "testpass\|readonlypass" docker/smoke-test.sh | head -3` to confirm the credential names; grep for any remaining `--server-path` in `docker/` (must be none).

- [ ] **Step 4: Smoke on a fresh 2.37.1 stack** (recipe above). Expected rc=0, 0 failed, the 4 new assertions PASS. Clean up temp logs and `docker compose -f docker/docker-compose.yml down -v`.

- [ ] **Step 5: Commit**

```bash
bash -n docker/smoke-test.sh && bash -n docker/seed.sh
git add docker/seed.sh docker/smoke-test.sh
git commit -m "test: seed an upload-only user and smoke-test cover permissions"
```

---

### Task 4: Docs and conventions

**Files:** `README.md`, `CLAUDE.md`, `docs/abs-api-coverage.md`, `docs/cli-design.md`, `docs/testing.md`, `docs/abs-compatibility.md`

- [ ] **Step 1: Edits**

- `README.md` Commands table: replace
  `| \`items cover set --id <id> [--url \| --file \| --server-path]\` | Apply a cover image |`
  with
  `| \`items cover set --id <id> [--url \| --file]\` | Apply a cover image (requires update + upload) |`
  and add below it
  `| \`items cover link --id <id> --path <path>\` | Use an image among the item's files as its cover (requires update) |`.
  Match how other rows mention permissions (check neighbours; drop the parentheticals if rows don't usually carry them).
- `README.md` "Permission required" paragraph (line ~286): `(one of \`admin\`, …)` → `(one or more of \`admin\`, \`update\`, \`upload\`, \`download\`, \`delete\` — all listed are required)`.
- `README.md` seed line `4 users` → `5 users`.
- `CLAUDE.md` Permission tagging: after the single-token sentence add: `When the endpoint checks several, pass them all — \`command.AddPermissionRequired("update", "upload")\` renders \`update, upload\` (all required).` Hint mirroring: add `multi-token tags join with " and " — tag \`update, upload\` ↔ hint \`"'update' and 'upload' permission"\``.
- `docs/abs-api-coverage.md`: POST `/api/items/:id/cover` permission `upload` → `update, upload`; PATCH row command `items cover set --server-path` → `items cover link`.
- `docs/cli-design.md`: split the cover set row into `abs-cli items cover set --id <id> [--url \| --file]` → `POST /api/items/{id}/cover` and `abs-cli items cover link --id <id> --path <path>` → `PATCH /api/items/{id}/cover`.
- `docs/testing.md` cover bullet: `set via \`--file\` / \`--url\`, link via \`cover link --path\`, permission denials (testuser / readonlyuser / uploadonlyuser), get to file and to stdout, remove`.
- `docs/abs-compatibility.md`: row label `| 1.1.5+          |` → `| 1.2.0+          |`; in that row `(\`items cover set --server-path\`)` → `(\`items cover link\`)`.

- [ ] **Step 2: Verify** — `grep -rn "server-path" README.md CLAUDE.md docs src tests docker | grep -v "docs/specs\|docs/plans\|CHANGELOG"` → no hits (except historical text you deliberately keep — report any).

- [ ] **Step 3: Commit**

```bash
git add README.md CLAUDE.md docs/abs-api-coverage.md docs/cli-design.md docs/testing.md docs/abs-compatibility.md
git commit -m "docs: document items cover link and multi-permission tags"
```
