# Item File Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an `items file` subgroup — `download`, `delete`, `ffprobe` — as thin pass-throughs over the ABS per-file endpoints, keyed by item `--id` + file `--ino`.

**Architecture:** Extend `ItemsService` (3 methods) and `ApiEndpoints` (3 helpers); add `CreateFileCommand()` to `ItemsCommand`. Reuse existing patterns: `download` mirrors `items cover get` (stream → file/stdout, reusing `CoverFileSavedDescriptor`); `delete` mirrors `authors delete` (`{ "success": "true" }`); `ffprobe` mirrors `authors lookup` (raw JSON passthrough). No new models.

**Tech Stack:** C# / .NET, System.CommandLine, xUnit.

**Spec:** `docs/specs/2026-07-13-item-file-management-design.md`

**Conventions:** No unnecessary blank lines in method bodies. `dotnet format AbsCli.sln` before each commit. Conventional Commits, imperative lowercase no period; NO `Co-Authored-By`, NO "Generated with Claude Code". Do NOT edit `CHANGELOG.md`.

**Permission tag ↔ hint mirroring:** `download` ↔ `"'download' permission"`; `delete` ↔ `"'delete' permission"`; `admin` ↔ `"admin permission"` (no quotes around admin).

---

## File Structure

Modified:
- `src/AbsCli/Api/ApiEndpoints.cs` — `ItemFile`, `ItemFileDownload`, `ItemFfprobe`
- `src/AbsCli/Services/ItemsService.cs` — `DownloadFileStreamAsync`, `DeleteFileAsync`, `FfprobeAsync`
- `src/AbsCli/Commands/ItemsCommand.cs` — `CreateFileCommand()` + register in `Create()`
- `tests/AbsCli.Tests/Api/ApiEndpointsTests.cs` — 3 new assertions
- `tests/AbsCli.Tests/Commands/ItemsCommandTests.cs` — file-subgroup tests
- `README.md`, `docs/abs-api-coverage.md`, `docker/smoke-test.sh`

No new models / `JsonContext` / generator changes.

---

## Task 1: Endpoint helpers

**Files:** Modify `src/AbsCli/Api/ApiEndpoints.cs`; append to `tests/AbsCli.Tests/Api/ApiEndpointsTests.cs`.

- [ ] **Step 1: Add failing tests**

Append inside the `ApiEndpointsTests` class (before its closing brace):

```csharp
    [Fact]
    public void ItemFile_BuildsPath()
    {
        Assert.Equal("api/items/li_1/file/12345", ApiEndpoints.ItemFile("li_1", "12345"));
    }

    [Fact]
    public void ItemFileDownload_BuildsPath()
    {
        Assert.Equal("api/items/li_1/file/12345/download", ApiEndpoints.ItemFileDownload("li_1", "12345"));
    }

    [Fact]
    public void ItemFfprobe_BuildsPath()
    {
        Assert.Equal("api/items/li_1/ffprobe/12345", ApiEndpoints.ItemFfprobe("li_1", "12345"));
    }
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test tests/AbsCli.Tests --filter ApiEndpointsTests`
Expected: FAIL (members don't exist).

- [ ] **Step 3: Add the endpoints**

In `src/AbsCli/Api/ApiEndpoints.cs`, near the other `Item*` helpers (after `ItemEbookFileStatus`):

```csharp
    public static string ItemFile(string id, string ino) => $"api/items/{id}/file/{ino}";
    public static string ItemFileDownload(string id, string ino) => $"api/items/{id}/file/{ino}/download";
    public static string ItemFfprobe(string id, string ino) => $"api/items/{id}/ffprobe/{ino}";
```

(Plain interpolation of `ino`, matching the existing `ItemEbookFileStatus` precedent.)

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test tests/AbsCli.Tests --filter ApiEndpointsTests`
Expected: PASS.

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Api/ApiEndpoints.cs tests/AbsCli.Tests/Api/ApiEndpointsTests.cs
git commit -m "feat: add item file/ffprobe endpoint helpers"
```

---

## Task 2: ItemsService methods

**Files:** Modify `src/AbsCli/Services/ItemsService.cs`. No new unit tests (thin pass-through; covered by endpoint tests, command tests, live smoke).

- [ ] **Step 1: Add the three methods**

Add to `ItemsService` (confirm signatures against existing usage: `GetStreamAsync(endpoint, permissionHint?)`, `DeleteAsync(endpoint, permissionHint?)`, `GetAsync(endpoint, permissionHint?)` — all already used in this file / `CoversService`). Add `using System.IO;` only if not already available via global usings (it is — omit unless the build complains):

```csharp
    public async Task<Stream> DownloadFileStreamAsync(string id, string ino)
    {
        return await _client.GetStreamAsync(ApiEndpoints.ItemFileDownload(id, ino), "'download' permission");
    }

    public async Task DeleteFileAsync(string id, string ino)
    {
        await _client.DeleteAsync(ApiEndpoints.ItemFile(id, ino), "'delete' permission");
    }

    public async Task<string> FfprobeAsync(string id, string ino)
    {
        return await _client.GetAsync(ApiEndpoints.ItemFfprobe(id, ino), "admin permission");
    }
```

- [ ] **Step 2: Build**

Run: `dotnet build src/AbsCli`
Expected: 0 errors.

- [ ] **Step 3: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Services/ItemsService.cs
git commit -m "feat: add item file download/delete/ffprobe service methods"
```

---

## Task 3: `items file` command subgroup

**Files:** Modify `src/AbsCli/Commands/ItemsCommand.cs`; extend `tests/AbsCli.Tests/Commands/ItemsCommandTests.cs`.

- [ ] **Step 1: Add failing tests**

Append inside the `ItemsCommandTests` class:

```csharp
    private static List<string> FileSubVerbs()
    {
        var items = ItemsCommand.Create();
        var file = items.Subcommands.First(c => c.Name == "file");
        return file.Subcommands.Select(c => c.Name).ToList();
    }

    [Fact]
    public void ItemsFile_HasDownloadDeleteFfprobe()
    {
        Assert.Equal(new[] { "download", "delete", "ffprobe" }, FileSubVerbs());
    }

    [Fact]
    public void ItemsFileDownload_RequiresDownloadPermissionAndOptions()
    {
        var output = RenderHelp("items", "file", "download").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  download", output);
        Assert.Contains("--id", output);
        Assert.Contains("--ino", output);
        Assert.Contains("--output", output);
    }

    [Fact]
    public void ItemsFileDelete_RequiresDeletePermission_AndWarnsOnDiskDeletion()
    {
        var output = RenderHelp("items", "file", "delete").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  delete", output);
        Assert.Contains("disk", output.ToLowerInvariant());
    }

    [Fact]
    public void ItemsFileFfprobe_RequiresAdmin_AndDocumentsAudioOnly()
    {
        var output = RenderHelp("items", "file", "ffprobe").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  admin", output);
        Assert.Contains("audio", output.ToLowerInvariant());
    }
```

(The existing `ItemsCommandTests.cs` already has the `RenderHelp` harness — reuse it. The class already has `using System.Linq` via global usings for `.First`/`.Select`.)

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test tests/AbsCli.Tests --filter ItemsCommandTests`
Expected: FAIL (no `file` subcommand).

- [ ] **Step 3: Add `CreateFileCommand()` and register it**

In `ItemsCommand.Create()`, register alongside the other subgroups (e.g. after the `CreateProgressCommand()` line):

```csharp
        command.Subcommands.Add(CreateFileCommand());
```

Add these methods to the class:

```csharp
    private static Command CreateFileCommand()
    {
        var command = new Command("file", "Manage individual files of a library item (download, delete, ffprobe)");
        command.Subcommands.Add(CreateFileDownloadCommand());
        command.Subcommands.Add(CreateFileDeleteCommand());
        command.Subcommands.Add(CreateFileFfprobeCommand());
        return command;
    }

    private static Command CreateFileDownloadCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var inoOption = new Option<string>("--ino") { Description = "File inode (from items get --expanded → libraryFiles[].ino)", Required = true };
        var outputOption = new Option<string>("--output") { Description = "Output file path, or '-' for binary to stdout", Required = true };
        var command = new Command("download", "Download a single file of a library item") { idOption, inoOption, outputOption };
        command.AddPermissionRequired("download");
        command.AddExamples(
            "abs-cli items file download --id \"li_abc\" --ino \"12345\" --output track01.mp3",
            "abs-cli items file download --id \"li_abc\" --ino \"12345\" --output - > track01.mp3");
        command.AddResponseExample<CoverFileSavedDescriptor>();
        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var ino = parseResult.GetValue(inoOption)!;
            var output = parseResult.GetValue(outputOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            await using var stream = await service.DownloadFileStreamAsync(id, ino);
            if (output == "-")
            {
                await using var stdout = Console.OpenStandardOutput();
                await stream.CopyToAsync(stdout);
                return;
            }
            long bytes;
            await using (var fileStream = new FileStream(output, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fileStream);
                bytes = fileStream.Length;
            }
            var descriptor = new CoverFileSavedDescriptor { Path = output, Bytes = bytes };
            ConsoleOutput.WriteJson(descriptor, AppJsonContext.Default.CoverFileSavedDescriptor);
        });
        return command;
    }

    private static Command CreateFileDeleteCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var inoOption = new Option<string>("--ino") { Description = "File inode (from items get --expanded → libraryFiles[].ino)", Required = true };
        var command = new Command("delete", "Delete a single file of a library item") { idOption, inoOption };
        command.AddPermissionRequired("delete");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "DESTRUCTIVE: permanently deletes the file from disk (not just the DB",
            "record). If it is the item's last media file, the item is marked",
            "missing. No confirmation prompt.");
        command.AddExamples(
            "abs-cli items file delete --id \"li_abc\" --ino \"12345\"");
        command.AddShapeSection("Response shape",
            "{ \"success\": \"true\" }");
        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var ino = parseResult.GetValue(inoOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            await service.DeleteFileAsync(id, ino);
            ConsoleOutput.WriteJson(new Dictionary<string, string> { ["success"] = "true" });
        });
        return command;
    }

    private static Command CreateFileFfprobeCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var inoOption = new Option<string>("--ino") { Description = "Audio file inode (from items get --expanded → libraryFiles[].ino)", Required = true };
        var command = new Command("ffprobe", "Print raw ffprobe data for an audio file") { idOption, inoOption };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Admin only. Audio files only — a non-audio inode returns Not found",
            "(exit 2). Output is the raw ffprobe JSON (streams, format, chapters),",
            "passed through unmodified.");
        command.AddExamples(
            "abs-cli items file ffprobe --id \"li_abc\" --ino \"12345\"");
        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var ino = parseResult.GetValue(inoOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new ItemsService(client);
            var json = await service.FfprobeAsync(id, ino);
            ConsoleOutput.WriteRawJson(json);
        });
        return command;
    }
```

Verify against existing `ItemsCommand` code: `AddPermissionRequired`, `AddHelpSection`, `AddExamples`, `AddResponseExample<T>`, `AddShapeSection`, `CommandHelper.BuildClient()`, `ConsoleOutput.WriteJson`/`WriteRawJson`, `CoverFileSavedDescriptor` are all already used in this file (cover get uses the descriptor; authors delete uses the `{success:true}` shape). Adjust to match real signatures if any differ.

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test tests/AbsCli.Tests --filter ItemsCommandTests`
Expected: PASS (existing + 4 new).

- [ ] **Step 5: Confirm wiring**

Run: `dotnet run --project src/AbsCli -- items file --help`
Expected: shows `download`, `delete`, `ffprobe`.

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/ItemsCommand.cs tests/AbsCli.Tests/Commands/ItemsCommandTests.cs
git status --short   # if ResponseExamples.g.cs shows modified, revert: git checkout src/AbsCli/Commands/ResponseExamples.g.cs
git commit -m "feat: add items file subgroup (download, delete, ffprobe)"
```

---

## Task 4: Docs — README + coverage map

**Files:** Modify `README.md`, `docs/abs-api-coverage.md`.

- [ ] **Step 1: README Commands table**

Add three rows near the other `items …` rows (e.g. after the `items cover …` rows):

```markdown
| `items file download --id <id> --ino <ino> --output <path\|->` | Download a single file of an item (requires download) |
| `items file delete --id <id> --ino <ino>` | Delete a single file from disk (requires delete — destructive) |
| `items file ffprobe --id <id> --ino <ino>` | Print raw ffprobe data for an audio file (admin) |
```

Match the surrounding column format.

- [ ] **Step 2: Coverage doc**

In `docs/abs-api-coverage.md`, update the four `items` file/ffprobe rows:
- `| GET | \`/api/items/:id/ffprobe/:fileid\` | FFprobe data for file | | — |` → **permission column `admin`** (fix), last column `` `items file ffprobe` ✅ ``.
- `| GET | \`/api/items/:id/file/:fileid\` | Get library file | | — |` → leave permission blank, last column stays `—` (not exposed).
- `| DELETE | \`/api/items/:id/file/:fileid\` | Delete library file | delete | — |` → last column `` `items file delete` ✅ ``.
- `| GET | \`/api/items/:id/file/:fileid/download\` | Download library file | download | — |` → last column `` `items file download` ✅ ``.

- [ ] **Step 3: Verify**

```bash
rg -n "ffprobe|file/:fileid" docs/abs-api-coverage.md
rg -n "items file" README.md
```
Confirm the ffprobe row shows `admin` and download/delete/ffprobe show ✅; the plain `file/:fileid` row is unchanged (`—`).

- [ ] **Step 4: Commit**

```bash
git add README.md docs/abs-api-coverage.md
git commit -m "docs: document items file commands and fix ffprobe permission"
```

---

## Task 5: Smoke test

**Files:** Modify `docker/smoke-test.sh`. No `seed.sh` change (existing seeded audiobooks + multi-ebook fixture suffice).

- [ ] **Step 1: Help-example enumeration**

Add to the leaf-command loop (backslash-continued):
```bash
           "items file download" "items file delete" "items file ffprobe" \
```
(No parent-loop change needed; `items` is already covered. Optionally the `items file` group itself renders help — the parent loop asserts `Description:`/`Usage:` for group commands, so adding `"items file"` there is fine but not required.)

- [ ] **Step 2: Add an "Item File" smoke section**

Place it AFTER the "Items Get Expanded" and "Toggle Ebook Status" sections (both depend on the multi-ebook fixture having BOTH ebook files — the delete below removes one). Use the existing helpers (`assert_json_key`, `assert_json_expr`, `pass`, `fail`, `abs_login`). Ensure it runs as `root` (admin) for the happy-path (ffprobe needs admin); the smoke is logged in as root by default in the main body.

```bash
# ============================================================
echo ""
echo "=== Item File Management ==="
# ============================================================

# Pick a seeded audiobook and one of its audio-file inodes.
AUDIO_ITEM_ID=$($CLI items list --library "$LIB_ID" --limit 100 2>/dev/null \
    | python3 -c "
import sys, json
d = json.load(sys.stdin)
for r in d['results']:
    if r.get('media',{}).get('numAudioFiles',0) or r.get('mediaType')=='book':
        print(r['id']); break
" 2>/dev/null)
AUDIO_INO=$($CLI items get --id "$AUDIO_ITEM_ID" --expanded 2>/dev/null \
    | python3 -c "
import sys, json
d = json.load(sys.stdin)
audio = [lf for lf in d.get('libraryFiles',[]) if lf.get('fileType')=='audio']
print(audio[0]['ino'] if audio else '')
" 2>/dev/null)

# download to a temp file, assert non-empty
DL_TMP=$(mktemp)
$CLI items file download --id "$AUDIO_ITEM_ID" --ino "$AUDIO_INO" --output "$DL_TMP" 2>/dev/null > /dev/null
if [ -s "$DL_TMP" ]; then pass "items file download: wrote non-empty file"; else fail "items file download: wrote non-empty file" "empty/missing"; fi
rm -f "$DL_TMP"

# ffprobe the same audio file, assert streams + format
output=$($CLI items file ffprobe --id "$AUDIO_ITEM_ID" --ino "$AUDIO_INO" 2>&1)
assert_json_key "items file ffprobe returns streams" "streams" "$output"
assert_json_key "items file ffprobe returns format" "format" "$output"

# delete a throwaway: the supplementary ebook file of the multi-ebook fixture.
FIX_ITEM_ID=$($CLI items list --library "$LIB_ID" --limit 100 2>/dev/null \
    | python3 -c "
import sys, json
d = json.load(sys.stdin)
for r in d['results']:
    if r.get('media',{}).get('metadata',{}).get('title','')=='Multi Ebook Test':
        print(r['id']); break
" 2>/dev/null)
SUPP_INO=$($CLI items get --id "$FIX_ITEM_ID" --expanded 2>/dev/null \
    | python3 -c "
import sys, json
d = json.load(sys.stdin)
supp = [lf for lf in d.get('libraryFiles',[]) if lf.get('fileType')=='ebook' and lf.get('isSupplementary') is True]
print(supp[0]['ino'] if supp else '')
" 2>/dev/null)
output=$($CLI items file delete --id "$FIX_ITEM_ID" --ino "$SUPP_INO" 2>&1)
assert_json_expr "items file delete returns success" "d.get('success')=='true'" "$output"
```

- [ ] **Step 3: 403 assertions**

In the permission-denial area (search `abs_login readonlyuser readonlypass` / the `testuser` admin-denial group), add:

readonlyuser (delete denial):
```bash
error_output=$($CLI items file delete --id "$AUDIO_ITEM_ID" --ino "$AUDIO_INO" 2>&1 || true)
if echo "$error_output" | grep -q "'delete' permission"; then
    pass "items file delete as readonlyuser hits 'delete' permission denial"
else
    fail "items file delete as readonlyuser hits 'delete' permission denial" "got: ${error_output:0:200}"
fi
```

testuser (admin denial):
```bash
error_output=$($CLI items file ffprobe --id "$AUDIO_ITEM_ID" --ino "$AUDIO_INO" 2>&1 || true)
if echo "$error_output" | grep -qi "permission denied\|admin"; then
    pass "items file ffprobe as testuser shows admin permission denied"
else
    fail "items file ffprobe as testuser shows admin permission denied" "got: ${error_output:0:200}"
fi
```
Ensure `$AUDIO_ITEM_ID` / `$AUDIO_INO` are shell globals set earlier (Step 2 runs in the main root-authenticated body, before the permission-denial section). Keep the section's trailing `abs_login root root`.

- [ ] **Step 4: Run the smoke test**

```bash
cd docker && docker compose up -d
IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
ABS_URL=http://$IP:80 bash docker/seed.sh
ABS_URL=http://$IP:80 bash docker/smoke-test.sh
```
Expected: all pass, exit 0. If `fileType`/`isSupplementary`/`numAudioFiles` field names differ from what the accessors assume, inspect a live `items get --expanded` payload and adjust the python extraction. Only mark "smoke passed" after seeing it.

- [ ] **Step 5: Commit**

```bash
git add docker/smoke-test.sh
git commit -m "test: add items file smoke assertions and 403 coverage"
```

---

## Task 6: Full verification

- [ ] **Step 1: Full unit run** — `dotnet test AbsCli.sln` → all pass (incl. `ResponseExamplesDriftTest`).
- [ ] **Step 2: Format check** — `dotnet format AbsCli.sln --verify-no-changes` → clean (else format + commit `chore: fix formatting`).
- [ ] **Step 3: Wiring** — `dotnet run --project src/AbsCli -- items file --help` shows the three verbs.
- [ ] **Step 4: Smoke gate** — confirm Task 5's smoke passed. Gates the PR checkbox — do not check unverified.

---

## Self-Review Notes (author checklist — completed during planning)

- **Spec coverage:** download (Task 1/2/3), delete (destructive, `{success:true}`, on-disk warning), ffprobe (admin, raw JSON, audio-only), coverage-doc fix incl. ffprobe blank→admin (Task 4), smoke incl. download-bytes / ffprobe-JSON / throwaway-delete / 403s (Task 5). `getLibraryFile` intentionally not exposed. No new models.
- **Reuse:** `CoverFileSavedDescriptor` (already registered — no generator/JsonContext change), `authors delete` `{success:true}` shape, `authors lookup` raw-JSON passthrough.
- **Permission mirroring:** download↔`'download' permission`, delete↔`'delete' permission`, ffprobe↔`admin permission`. Command tests assert the full `Permission required:\n  <token>` line.
- **Smoke ordering:** the throwaway-delete of the multi-ebook fixture's supplementary file runs AFTER the get-expanded/toggle-ebook sections so it doesn't disturb them.
- **Type consistency:** `DownloadFileStreamAsync`/`DeleteFileAsync`/`FfprobeAsync` signatures match their call sites; endpoints `ItemFile`/`ItemFileDownload`/`ItemFfprobe` match service + tests.
- **CHANGELOG untouched** (release-owned).
