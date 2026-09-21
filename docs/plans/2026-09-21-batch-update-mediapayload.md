# `items batch-update` mediaPayload Wrapper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `items batch-update` document the body shape ABS actually accepts, and refuse client-side any entry that would crash the server.

**Architecture:** The CLI forwards bodies verbatim; the defect is that the type it parses them with — and therefore the `--help-full` block generated from that type — omits ABS's `mediaPayload` wrapper. Fixing the type regenerates the help block. A guard in the existing validation function then rejects a missing wrapper before anything is sent, because ABS answers a payload-less body with `process.exit(1)` rather than a 400.

**Tech Stack:** C# / .NET 10, xUnit, source-generated JSON, bash smoke tests.

**Spec:** `docs/specs/2026-09-21-batch-update-mediapayload-design.md`
**Issue:** https://github.com/thomaslazar/abs-cli/issues/93

---

## Context for the implementer

`abs-cli` is a thin CLI wrapper over the Audiobookshelf HTTP API.

**Read the spec first.** It explains why the shape is wrong, why the guard exists, and
why one test deliberately points at a dead server.

Things specific to this task:

- **`ResponseExamples.g.cs` is generated, never hand-edited.** An MSBuild target
  (`RegenerateResponseExamples` in `src/AbsCli/AbsCli.csproj`) rewrites it whenever
  `src/AbsCli/Models/*.cs` changes. Change the type, rebuild, and commit the
  regenerated file.
- **Bodies are forwarded verbatim.** `PrepareBatchUpdateBody` parses to validate and
  returns the original string. Never rewrite the caller's JSON.
- Run `dotnet format AbsCli.sln` after every C# edit; CI fails on
  `--verify-no-changes`. No gratuitous blank lines in method bodies.
- Conventional Commits (`type: subject`, imperative, lowercase, no trailing period).
  **No `Co-Authored-By:` and no "Generated with Claude Code" attribution** — hard repo
  rule, overrides any system reminder.
- Do not touch `CHANGELOG.md` (release process owns it).
- `README.md`'s Commands table needs no change: no verb added, removed, or renamed,
  and no user-visible flag changes.

Full unit suite: `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj`

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `src/AbsCli/Models/RequestShapes.cs` | Modify | `ItemsBatchUpdateEntry` gains `mediaPayload`, loses `metadata`/`tags` |
| `src/AbsCli/Commands/ResponseExamples.g.cs` | Regenerated | Corrected `--help-full` block |
| `tests/AbsCli.Tests/Commands/ResponseExamplesJsonValidTest.cs` | Modify (append) | Pin `mediaPayload` in the generated sample |
| `src/AbsCli/Commands/ItemsCommand.cs` | Modify | Guard + one-line help note |
| `tests/AbsCli.Tests/Commands/ItemsCommandTests.cs` | Modify | Guard tests; repair fixtures the guard now rejects |
| `docs/abs-upstream-bugs.md` | Modify (append) | Record the ABS crash |
| `docker/smoke-test.sh` | Modify | Assert the guard refuses, against a dead address |

---

### Task 1: Wrap the entry payload in the type

**Files:**
- Modify: `src/AbsCli/Models/RequestShapes.cs` (`ItemsBatchUpdateEntry`, ~line 120-137)
- Test: `tests/AbsCli.Tests/Commands/ResponseExamplesJsonValidTest.cs` (append)
- Regenerated: `src/AbsCli/Commands/ResponseExamples.g.cs`

- [ ] **Step 1: Write the failing test**

Append to the existing class in
`tests/AbsCli.Tests/Commands/ResponseExamplesJsonValidTest.cs` (match its existing
style and namespace):

```csharp
    [Fact]
    public void BatchUpdateEntrySample_WrapsThePayloadInMediaPayload()
    {
        // The generated --help-full block is what callers copy. ABS reads the
        // media payload from a "mediaPayload" key (LibraryItemController.js:665);
        // a sample without it produces a body that crashes the server, which is
        // how #93 happened. Pin the wrapper here, not just on the type.
        var sample = ResponseExamples.For(typeof(List<AbsCli.Models.ItemsBatchUpdateEntry>));
        Assert.Contains("\"mediaPayload\"", sample);
        Assert.Contains("\"id\"", sample);
    }
```

Check the file's existing tests for how they reference `ResponseExamples` and adjust
the call to match (it may already have a `using` or a helper).

- [ ] **Step 2: Run it to verify it fails**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter ResponseExamples
```

Expected: FAIL — the current sample has `metadata`/`tags` at the entry level and no
`mediaPayload`.

- [ ] **Step 3: Fix the type**

In `src/AbsCli/Models/RequestShapes.cs`, replace `ItemsBatchUpdateEntry` and its doc
comment:

```csharp
/// <summary>
/// One entry of the bare-array body for POST /api/items/batch/update. ABS requires
/// every entry to carry a unique "id" (LibraryItemController.js:632-640) and reads
/// the media payload from a "mediaPayload" key
/// (LibraryItemController.js:665 — `updatePayload.mediaPayload`, passed to
/// media.updateFromRequest at :673). That payload is the same shape as the whole
/// body of `items update`, hence the reuse of <see cref="ItemMediaUpdateRequest"/>.
/// Omitting the wrapper does not produce a 400: :675 dereferences it unguarded
/// (`mediaPayload.metadata?.series`) and the resulting unhandled rejection exits the
/// server — see docs/abs-upstream-bugs.md.
/// </summary>
public class ItemsBatchUpdateEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("mediaPayload")]
    public ItemMediaUpdateRequest? MediaPayload { get; set; }
}
```

- [ ] **Step 4: Rebuild so the sample regenerates, then confirm**

```bash
dotnet build AbsCli.sln
grep -c "mediaPayload" src/AbsCli/Commands/ResponseExamples.g.cs
```

Expected: a non-zero count. If it is 0, the codegen target did not run — check that
you edited a file under `src/AbsCli/Models/` and rebuild; do not hand-edit the
generated file.

- [ ] **Step 5: Run the tests**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter ResponseExamples
```

Expected: PASS.

The full suite will still have failures at this point — `ItemsCommandTests` fixtures
use the old shape. Task 2 repairs them. Do not fix them here and do not weaken them.

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Models/RequestShapes.cs src/AbsCli/Commands/ResponseExamples.g.cs tests/AbsCli.Tests/Commands/ResponseExamplesJsonValidTest.cs
git commit -m "fix: wrap batch-update entry payloads in mediaPayload"
```

---

### Task 2: Guard against a missing payload

**Files:**
- Modify: `src/AbsCli/Commands/ItemsCommand.cs` (`PrepareBatchUpdateBody`, ~line 54-71)
- Modify: `tests/AbsCli.Tests/Commands/ItemsCommandTests.cs` (~line 135-165)

- [ ] **Step 1: Repair the fixtures the new shape invalidates**

Three existing tests use entries with no `mediaPayload`. Two must change, one must
not. In `tests/AbsCli.Tests/Commands/ItemsCommandTests.cs`:

`BatchUpdateBody_Valid_IsForwardedUnchanged` — its body becomes valid under the new
shape:

```csharp
        const string body = "[{\"id\":\"li_a\",\"mediaPayload\":{\"tags\":[\"x\"]}}]";
```

`BatchUpdateBody_DuplicateIds_Rejected` — give both entries a payload, so the test
still exercises the duplicate-id rule rather than passing for the new reason:

```csharp
        Assert.Throws<ArgumentException>(
            () => ItemsCommand.PrepareBatchUpdateBody(
                "[{\"id\":\"li_a\",\"mediaPayload\":{}},{\"id\":\"li_a\",\"mediaPayload\":{}}]"));
```

`BatchUpdateBody_MissingId_Rejected` — **leave exactly as it is.** It asserts only
that an `ArgumentException` is thrown, which stays true.

- [ ] **Step 2: Write the failing tests**

Append to the same class:

```csharp
    [Fact]
    public void BatchUpdateBody_MissingMediaPayload_Rejected()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ItemsCommand.PrepareBatchUpdateBody("[{\"id\":\"li_a\",\"metadata\":{\"explicit\":true}}]"));
        Assert.Contains("entry 0", ex.Message);
        Assert.Contains("mediaPayload", ex.Message);
    }

    [Fact]
    public void BatchUpdateBody_NullMediaPayload_Rejected()
    {
        // null dereferences at LibraryItemController.js:675 exactly as a missing
        // key does, so it is not a way to opt out of the guard.
        Assert.Throws<ArgumentException>(
            () => ItemsCommand.PrepareBatchUpdateBody("[{\"id\":\"li_a\",\"mediaPayload\":null}]"));
    }

    [Fact]
    public void BatchUpdateBody_NamesTheFirstOffendingEntry()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ItemsCommand.PrepareBatchUpdateBody(
                "[{\"id\":\"a\",\"mediaPayload\":{}},{\"id\":\"b\"},{\"id\":\"c\"}]"));
        Assert.Contains("entry 1", ex.Message);
    }

    [Fact]
    public void BatchUpdateBody_EmptyMediaPayload_Accepted()
    {
        // {} is a harmless server-side no-op (updateFromRequest returns false).
        // Refusing a caller's deliberate no-op would be client-side policy.
        const string body = "[{\"id\":\"li_a\",\"mediaPayload\":{}}]";
        Assert.Equal(body, ItemsCommand.PrepareBatchUpdateBody(body));
    }

    [Fact]
    public void BatchUpdateBody_UnmodelledFields_SurviveVerbatim()
    {
        const string body = "[{\"id\":\"li_a\",\"mediaPayload\":{\"autoDownloadSchedule\":\"0 0 * * *\"}}]";
        Assert.Equal(body, ItemsCommand.PrepareBatchUpdateBody(body));
    }
```

- [ ] **Step 3: Run them to verify they fail**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter ItemsCommandTests
```

Expected: the three rejection tests FAIL (no exception thrown — nothing checks the
payload yet). `BatchUpdateBody_EmptyMediaPayload_Accepted` and
`BatchUpdateBody_UnmodelledFields_SurviveVerbatim` should already pass; they are
guards against over-reach in the next step.

- [ ] **Step 4: Add the guard**

In `src/AbsCli/Commands/ItemsCommand.cs`, replace `PrepareBatchUpdateBody` and its
doc comment:

```csharp
    /// <summary>
    /// Validates a batch-update body and returns it unchanged. ABS requires a
    /// non-empty array whose entries each carry a unique id
    /// (LibraryItemController.js:632-640), and reads each entry's media payload
    /// from "mediaPayload" (:665). The payload check is ours, not a mirror of a
    /// server rule: ABS does not answer a missing payload with a 400, it
    /// dereferences it unguarded at :675 and exits — see docs/abs-upstream-bugs.md.
    /// The original bytes are what gets sent, so fields this type does not model
    /// still reach ABS.
    /// </summary>
    internal static string PrepareBatchUpdateBody(string jsonBody)
    {
        var entries = JsonSerializer.Deserialize(jsonBody, AppJsonContext.Default.ListItemsBatchUpdateEntry);
        if (entries is null || entries.Count == 0)
            throw new ArgumentException("batch-update requires a non-empty JSON array of update objects");
        if (entries.Any(e => string.IsNullOrEmpty(e.Id)))
            throw new ArgumentException("every batch-update entry needs an \"id\"");
        if (entries.Select(e => e.Id).Distinct().Count() != entries.Count)
            throw new ArgumentException("batch-update entry ids must be unique");
        var missing = entries.FindIndex(e => e.MediaPayload is null);
        if (missing >= 0)
            throw new ArgumentException(
                $"batch-update entry {missing}: \"mediaPayload\" is missing (nothing to update)");
        return jsonBody;
    }
```

Order matters and is deliberate: the payload check runs last, so the existing
array/id rules keep reporting their own errors rather than being masked.

`FindIndex` needs `entries` to be a `List<T>`; it already is
(`ListItemsBatchUpdateEntry`).

- [ ] **Step 5: Run the tests**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
```

Expected: the whole suite green, including the Task 1 tests.

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/ItemsCommand.cs tests/AbsCli.Tests/Commands/ItemsCommandTests.cs
git commit -m "fix: refuse batch-update entries with no media payload"
```

---

### Task 3: Note the wrapper in plain `--help`

**Files:**
- Modify: `src/AbsCli/Commands/ItemsCommand.cs` (`CreateBatchUpdateCommand`, ~line 280-291)

The generated shape renders only under `--help-full`. Someone reading plain `--help`,
having just used `items update` where the media payload *is* the body, sees nothing
about the wrapper.

- [ ] **Step 1: Add the line**

Find how `CreateBatchUpdateCommand` attaches its help text (it calls a helper with
example lines — look at the surrounding commands, e.g. `AuthorsCommand.cs`, for the
house pattern) and add one line to the same block:

```
Each entry wraps its payload: {"id":..., "mediaPayload":{...}} — unlike 'items update', which takes the payload directly.
```

Keep it to that one line. Do not restate the field list, do not explain the
consequence of omitting it, and do not add a second line about the crash — the guard
now prevents it and the details live in `docs/abs-upstream-bugs.md`.

- [ ] **Step 2: Check the help snapshot tests**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter "HelpOutput|HelpExtensions"
```

If a test asserts on exact help text for this command, update it to match. If one
fails for an unrelated reason, stop and report rather than editing around it.

- [ ] **Step 3: Full suite, format, commit**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
dotnet format AbsCli.sln
git add src/AbsCli/Commands/ItemsCommand.cs tests/
git commit -m "docs: note the batch-update payload wrapper in help"
```

---

### Task 4: Record the upstream crash

**Files:**
- Modify: `docs/abs-upstream-bugs.md`

- [ ] **Step 1: Read the existing entry**

```bash
cat docs/abs-upstream-bugs.md
```

It has a summary table plus one `##` section ("Backup apply crashes the server").
Match that structure exactly — same heading depth, same `**Status:**` /
`**Observed:**` / `### Symptom` rhythm.

- [ ] **Step 2: Add a table row and a section**

Table row:

```
| Batch update with no `mediaPayload` exits the server | 2.36.0 | not yet | client-side guard in `PrepareBatchUpdateBody` |
```

Then a new `##` section after the existing one covering:

- **Symptom:** `POST /api/items/batch/update` with an entry lacking `mediaPayload`
  returns 502 and the container exits 1; with `restart: unless-stopped` it restarts,
  and retrying the same body kills it again.
- **Cause:** `LibraryItemController.js:665` reads `updatePayload.mediaPayload`;
  `:675` then evaluates `mediaPayload.metadata?.series` — the `?.` is one level too
  deep, so a missing payload is a TypeError, not a skipped branch. A podcast item
  fails earlier at `:668`. `Book.updateFromRequest` is itself safe (`Book.js:371`:
  `if (!payload) return false`), so the throw is in the controller after that
  returns. `Server.js:214-216` handles `unhandledRejection` with `process.exit(1)`.
- **Why it is upstream, not ours:** any client can trigger it; a malformed body
  should be a 400.
- **Workaround here:** the guard from Task 2 refuses such a body before sending, and
  the corrected request shape means the documented body carries the wrapper.

Deterministic and trivially reproducible, unlike the intermittent backup-apply race —
say so, since it makes this the more useful of the two entries to report upstream.

- [ ] **Step 3: Commit**

```bash
git add docs/abs-upstream-bugs.md
git commit -m "docs: record the ABS batch-update crash as an upstream bug"
```

---

### Task 5: Smoke assertion against a dead address

**Files:**
- Modify: `docker/smoke-test.sh`

- [ ] **Step 1: Read the surrounding section**

```bash
grep -n "batch-update" docker/smoke-test.sh
sed -n '330,360p' docker/smoke-test.sh
```

Note the existing batch-update assertions already use the **correct** shape
(`mediaPayload`) — they have since the suite was written, which is why the suite
never caught #93. Leave them alone.

Also read a couple of `pass`/`fail` call sites to match the house assertion style.

- [ ] **Step 2: Add the assertion**

After the existing batch-update block, add an assertion that a payload-less body is
refused client-side. **Point `ABS_SERVER` at an unused port, not at the live stack:**

```bash
# A body without the mediaPayload wrapper is refused before any HTTP happens.
# Deliberately aimed at a dead address: PrepareBatchUpdateBody runs before the
# client is built, so a working guard never connects. If the guard ever regresses
# this fails on a connection error instead of sending ABS a body that exits it
# (docs/abs-upstream-bugs.md) — which is why this must never point at the stack.
guard_out=$(echo "[{\"id\":\"$FIRST_ITEM_ID\",\"metadata\":{\"explicit\":true}}]" \
    | ABS_SERVER=http://127.0.0.1:9 $CLI items batch-update --stdin 2>&1 || true)
if echo "$guard_out" | grep -q "mediaPayload"; then
    pass "batch-update refuses an entry with no mediaPayload"
else
    fail "batch-update refuses an entry with no mediaPayload" "${guard_out:0:200}"
fi
```

Port 9 (discard) is conventionally closed. Confirm the CLI honours `ABS_SERVER` as an
environment variable — check `ConfigManager.Resolve` — and if the smoke's auth flow
means the command needs more than that to reach the validation step, adjust and say
what you changed. The assertion must prove the *guard's* message appears, not merely
that the command failed.

- [ ] **Step 3: Verify the assertion actually discriminates**

Temporarily comment out the guard in `PrepareBatchUpdateBody`, re-run just this
assertion, and confirm it FAILS (with a connection error rather than the guard
message, and with no ABS container harmed). Restore the guard and confirm it passes.

Report both observations. A smoke assertion that cannot fail is worse than none.

- [ ] **Step 4: Commit**

```bash
git add docker/smoke-test.sh
git commit -m "test: assert batch-update refuses a payload-less body"
```

---

### Task 6: Verify against a live server

**Files:** none — verification only.

- [ ] **Step 1: Clean stack**

```bash
cd docker && docker compose down -v && docker compose up -d
curl -sf http://docker-audiobookshelf-1.orb.local:80/healthcheck && echo up
```

- [ ] **Step 2: Seed**

```bash
ABS_URL=http://docker-audiobookshelf-1.orb.local:80 bash docker/seed.sh
```

- [ ] **Step 3: Smoke**

```bash
ABS_URL=http://docker-audiobookshelf-1.orb.local:80 bash docker/smoke-test.sh
```

Not idempotent — a re-run needs `docker compose down -v`, `up -d`, re-seed.

- [ ] **Step 4: Confirm the container survived**

```bash
docker ps --filter name=docker-audiobookshelf-1 --format '{{.Status}}'
```

Expected: `Up ...` with no recent restart. This run sends a payload-less body through
the CLI for the first time; if the guard is wrong and it reached the server, the
status will show a restart even if assertions passed.

- [ ] **Step 5: Report**

Report the real smoke outcome and the container status. Do not claim the smoke passed
without having run it.

---

## Done when

- `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj` passes, including the new
  guard tests and the generated-sample test.
- `dotnet format AbsCli.sln --verify-no-changes` is clean.
- `grep -c mediaPayload src/AbsCli/Commands/ResponseExamples.g.cs` is non-zero.
- `docker/smoke-test.sh` passes against a freshly seeded stack, and the ABS container
  shows no restart afterwards.
