# Authors List Pagination Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert `abs-cli authors list` from its current unpaginated `{authors: [...]}` shape to the paginated `PaginatedResponse` shape used by `items list` and `series list`, with `--limit`, `--page`, `--sort`, and `--desc` flags. **Breaking change.**

**Architecture:** Always send numeric `limit` and `page` query params to ABS so the response is always the paginated shape (the dual-shape behaviour at `LibraryController.js:1022` is hidden from CLI users). Reuse the existing `PaginatedResponse` model and `AuthorItem` element type. Drop the now-unused `AuthorListResponse` model and its `[JsonSerializable]` registration. Update smoke test to consume the new shape and exercise pagination plus reverse sort.

**Tech Stack:** C# / .NET 10 / `System.CommandLine` 2.0.7 / `System.Text.Json` source-gen. No new packages.

**Spec:** [docs/specs/2026-05-08-authors-list-pagination.md](../specs/2026-05-08-authors-list-pagination.md)

---

## File Structure

**Modified:**

- `src/AbsCli/Services/AuthorsService.cs` — `ListAsync` signature changes from `(string libraryId)` to `(string libraryId, int limit, int? page, string? sort, bool desc)`; builds query string from the new params; returns `PaginatedResponse` instead of `AuthorListResponse`.
- `src/AbsCli/Commands/AuthorsCommand.cs` — `CreateListCommand` gains four options (`--limit`, `--page`, `--sort`, `--desc`); `AddResponseExample<AuthorListResponse>()` becomes `AddResponseExample(typeof(PaginatedResponse), typeof(AuthorItem))`; jq-free examples updated to show pagination usage; help description updated to drop the "no pagination" caveat that no longer applies.
- `src/AbsCli/Models/JsonContext.cs` — remove the `[JsonSerializable(typeof(AuthorListResponse))]` line.
- `src/AbsCli/Models/Author.cs` — remove the `AuthorListResponse` class.
- `tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs` — extend with help-render tests for the new flags and the paginated `Response shape`.
- `docker/smoke-test.sh` — replace the `d['authors']` assertions with `d['results']`; add pagination round-trip and a reverse-sort check.

**Files explicitly NOT modified:**

- `CHANGELOG.md` — release-owned (the breaking change goes into the release skill's manual CHANGELOG step).
- `docs/roadmap.md` — feature-level abstraction; no spec-mapping additions.
- `src/AbsCli/Models/PaginatedResponse.cs` — already generic enough to reuse.
- `src/AbsCli/Api/ApiEndpoints.cs` — `LibraryAuthors(string id)` is the right endpoint; unchanged.

---

## Task 0: Commit the plan file

**Files:** none new (plan file already exists on disk).

- [ ] **Step 1: Confirm branch state**

```bash
cd /workspaces/abs-cli
git status
git log --oneline -3
```

Expected: on branch `feat/authors-list-pagination`; HEAD is `docs: spec for v0.4.0 authors list pagination`; working tree shows the plan file as untracked.

- [ ] **Step 2: Commit the plan**

```bash
git add docs/plans/2026-05-08-authors-list-pagination.md
git commit -m "docs: plan for v0.4.0 authors list pagination"
```

---

## Task 1: Failing help-render tests for the new flag surface

The existing `AuthorsCommandTests` does not exercise `authors list` help. We add two `[Fact]` tests that pin the flag set and the response shape. They MUST be observed failing before any source change so the red→green cycle is real.

**Files:**
- Modify: `tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs`

- [ ] **Step 1: Add two `[Fact]` tests**

Append the following two methods inside the `AuthorsCommandTests` class, immediately before the closing `}` of the class:

```csharp
    [Fact]
    public void AuthorsList_Help_DocumentsPaginationFlags()
    {
        var output = RenderHelp("authors", "list");
        Assert.Contains("--limit", output);
        Assert.Contains("--page", output);
        Assert.Contains("--sort", output);
        Assert.Contains("--desc", output);
    }

    [Fact]
    public void AuthorsList_Help_ShowsPaginatedResponseShape()
    {
        var output = RenderHelp("authors", "list");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"results\"", output);
        Assert.Contains("\"total\"", output);
        Assert.Contains("\"limit\"", output);
        Assert.Contains("\"page\"", output);
    }
```

- [ ] **Step 2: Run the tests, observe red**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~AuthorsList_Help" -c Debug --nologo
```

Expected: both tests fail. The first fails because the new flags do not exist on the command. The second fails because the current `AddResponseExample<AuthorListResponse>()` produces a sample with `"authors"` (the old shape), not `"results"`/`"total"`/`"limit"`/`"page"`.

- [ ] **Step 3: Do not commit yet**

The implementation in Task 2 will turn these green, and they will be committed together with the source changes (one coherent commit).

---

## Task 2: Implement pagination — service, command, model cleanup

This task lands the entire user-visible behaviour change in one commit:
- `AuthorsService.ListAsync` gains pagination params and returns `PaginatedResponse`.
- `AuthorsCommand.CreateListCommand` gains the four flags and uses the new service signature.
- `AuthorListResponse` model and its `[JsonSerializable]` registration are removed (now unused).

Splitting these into separate commits would leave intermediate compile errors (the command would reference a deleted method, or the model would still be registered after deletion). Single coherent commit is the right granularity.

**Files:**
- Modify: `src/AbsCli/Services/AuthorsService.cs`
- Modify: `src/AbsCli/Commands/AuthorsCommand.cs`
- Modify: `src/AbsCli/Models/JsonContext.cs`
- Modify: `src/AbsCli/Models/Author.cs`

- [ ] **Step 1: Update `AuthorsService.ListAsync`**

In `src/AbsCli/Services/AuthorsService.cs`, replace the existing `ListAsync` method with this version (keep all other methods unchanged):

```csharp
    public async Task<PaginatedResponse> ListAsync(string libraryId, int limit, int? page, string? sort, bool desc)
    {
        var query = HttpUtility.ParseQueryString("");
        // Always send numeric limit and page so ABS returns the paginated
        // shape unconditionally. ABS's authors endpoint switches between
        // {authors:[...]} and {results, total, ...} based on whether both
        // are present and numeric — see LibraryController.js:1022.
        query["limit"] = limit.ToString();
        query["page"] = (page ?? 0).ToString();
        if (sort != null) query["sort"] = sort;
        if (desc) query["desc"] = "1";

        var url = ApiEndpoints.LibraryAuthors(libraryId) + "?" + query;
        return await _client.GetAsync(url, AppJsonContext.Default.PaginatedResponse);
    }
```

Then add the `using System.Web;` import at the top of the file (alongside the existing usings) so `HttpUtility.ParseQueryString` resolves. The full top of the file becomes:

```csharp
using System.Text.Json;
using System.Web;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;
```

- [ ] **Step 2: Update `AuthorsCommand.CreateListCommand`**

In `src/AbsCli/Commands/AuthorsCommand.cs`, replace the entire `CreateListCommand` private method with:

```csharp
    private static Command CreateListCommand()
    {
        var libraryOption = new Option<string?>("--library") { Description = "Library ID" };
        var limitOption = new Option<int>("--limit") { Description = "Results per page (default 50)", DefaultValueFactory = _ => 50 };
        var pageOption = new Option<int?>("--page") { Description = "Page number (0-indexed)" };
        var sortOption = new Option<string?>("--sort") { Description = "Sort field (name | lastFirst | addedAt | updatedAt | numBooks); default name" };
        var descOption = new Option<bool>("--desc") { Description = "Sort descending" };
        var command = new Command("list", "List authors in a library (paginated)")
        { libraryOption, limitOption, pageOption, sortOption, descOption };
        command.AddExamples(
            "abs-cli authors list",
            "abs-cli authors list --limit 100 --page 0",
            "abs-cli authors list --sort numBooks --desc --limit 10");
        command.AddResponseExample(typeof(PaginatedResponse), typeof(AuthorItem));
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var library = parseResult.GetValue(libraryOption);
            var limit = parseResult.GetValue(limitOption);
            var page = parseResult.GetValue(pageOption);
            var sort = parseResult.GetValue(sortOption) ?? "name";
            var desc = parseResult.GetValue(descOption);
            var (client, config) = CommandHelper.BuildClient(libraryOverride: library);
            var libraryId = CommandHelper.RequireLibrary(config);
            var service = new AuthorsService(client);
            var result = await service.ListAsync(libraryId, limit, page, sort, desc);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.PaginatedResponse);
            return 0;
        });
        return command;
    }
```

Notes on the rewrite:
- The previous "(returns all, no pagination)" wording in the description is dropped; the new description says "(paginated)".
- Default sort is `name`. The CLI applies the default in the action (`?? "name"`) rather than `DefaultValueFactory`, so the option's nullable type is preserved for "user did not pass" detection if needed later, and the help still shows the default in the option description.
- `LibraryAuthors(libraryId)` (the existing endpoint helper) is reused — no change to `ApiEndpoints.cs`.
- Examples are jq-free per project convention.

- [ ] **Step 3: Drop the `AuthorListResponse` registration**

In `src/AbsCli/Models/JsonContext.cs`, locate this line (around line 22):

```csharp
[JsonSerializable(typeof(AuthorListResponse))]
```

Delete the entire line. The neighbouring `[JsonSerializable(typeof(AuthorItem))]` stays.

- [ ] **Step 4: Drop the `AuthorListResponse` class**

In `src/AbsCli/Models/Author.cs`, the file currently contains both `AuthorItem` and `AuthorListResponse`. Delete the `AuthorListResponse` class (it is the second class in the file). The `AuthorItem` class above it stays. The final file should look like:

```csharp
using System.Text.Json.Serialization;

namespace AbsCli.Models;

public class AuthorItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("asin")]
    public string? Asin { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("imagePath")]
    public string? ImagePath { get; set; }

    [JsonPropertyName("libraryId")]
    public string LibraryId { get; set; } = "";

    [JsonPropertyName("addedAt")]
    public long AddedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public long UpdatedAt { get; set; }

    [JsonPropertyName("numBooks")]
    public int NumBooks { get; set; }

    [JsonPropertyName("lastFirst")]
    public string? LastFirst { get; set; }
}
```

- [ ] **Step 5: Build and run tests**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug 2>&1 | tail -5
dotnet test /workspaces/abs-cli/AbsCli.sln -c Debug --nologo 2>&1 | tail -3
```

Expected build: `0 Warning(s)`, `0 Error(s)`. Expected tests: all pass, including the two new `AuthorsList_Help_*` tests added in Task 1.

- [ ] **Step 6: Format check**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: clean exit (no output).

- [ ] **Step 7: Commit (BREAKING)**

```bash
git add src/AbsCli/Services/AuthorsService.cs \
        src/AbsCli/Commands/AuthorsCommand.cs \
        src/AbsCli/Models/JsonContext.cs \
        src/AbsCli/Models/Author.cs \
        tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs
git commit -m "$(cat <<'EOF'
feat!: paginate authors list

'authors list' now returns the paginated shape used by 'items list'
and 'series list': { results, total, limit, page }. New flags --limit
(default 50), --page (0-indexed), --sort (name | lastFirst | addedAt |
updatedAt | numBooks; default name), and --desc. The dual-shape
behaviour ABS exposes at GET /api/libraries/:id/authors is now hidden:
the CLI always sends numeric limit and page so the server consistently
returns the paginated shape.

The unused AuthorListResponse model and its source-gen registration
are removed; PaginatedResponse + AuthorItem cover the new shape.

BREAKING CHANGE: 'authors list' no longer returns {authors: [...]} and
no longer returns every author by default. Pass --limit 999 (or any
sufficiently high value) to restore the previous "all in one shot"
behaviour. Scripts reading d['authors'] should switch to d['results'].
EOF
)"
```

---

## Task 3: Smoke test update

The existing smoke test asserts `d['authors']` and counts 6 authors. After Task 2 those assertions are wrong shape. Replace them with paginated-shape equivalents and add coverage for the new pagination + sort flags so the breaking change is exercised end-to-end against the live ABS instance.

**Files:**
- Modify: `docker/smoke-test.sh`

- [ ] **Step 1: Find the Authors section header**

```bash
grep -n "=== Authors Commands ===" /workspaces/abs-cli/docker/smoke-test.sh
```

There is exactly one match. The block immediately after is what we replace.

- [ ] **Step 2: Replace the start of the Authors section**

Open `docker/smoke-test.sh` and locate the block starting with the line that reads `output=$($CLI authors list 2>/dev/null)` directly after the `=== Authors Commands ===` header (the first authors-list invocation). Replace from that line through (and including) the `output=$($CLI authors get --id "$AUTHOR_ID" 2>/dev/null)` block (the get-by-id assertions) with this version. All later lines in the Authors section (lookup, match, update, delete, merge) stay unchanged.

```bash
output=$($CLI authors list 2>/dev/null)
assert_json_key "authors list returns paginated shape (results)" "results" "$output"
assert_json_key "authors list returns paginated shape (total)" "total" "$output"
assert_json_expr "authors list has 6 authors" "d['total']==6 and len(d['results'])==6" "$output"
assert_json_expr "authors list contains Brandon Sanderson" \
    "any(a['name']=='Brandon Sanderson' for a in d['results'])" "$output"

AUTHOR_ID=$(echo "$output" | python3 -c "
import sys,json
authors = json.load(sys.stdin)['results']
bs = next(a for a in authors if a['name']=='Brandon Sanderson')
print(bs['id'])
")

# Pagination round-trip
output=$($CLI authors list --limit 3 --page 0 2>/dev/null)
assert_json_expr "authors list page 0 returns 3 results" "len(d['results'])==3" "$output"
assert_json_expr "authors list page 0 reports total 6" "d['total']==6" "$output"
PAGE0_NAMES=$(echo "$output" | python3 -c "import sys,json; print(','.join(sorted(a['name'] for a in json.load(sys.stdin)['results'])))")

output=$($CLI authors list --limit 3 --page 1 2>/dev/null)
assert_json_expr "authors list page 1 returns 3 results" "len(d['results'])==3" "$output"
assert_json_expr "authors list page 1 reports total 6" "d['total']==6" "$output"
PAGE1_NAMES=$(echo "$output" | python3 -c "import sys,json; print(','.join(sorted(a['name'] for a in json.load(sys.stdin)['results'])))")

if [ "$PAGE0_NAMES" != "$PAGE1_NAMES" ]; then
    pass "authors list pages do not overlap"
else
    fail "authors list pages do not overlap" "page 0 and page 1 returned the same names"
fi

# Reverse sort by name — first result should NOT be the alphabetically-first name
output=$($CLI authors list --sort name --desc 2>/dev/null)
FIRST_NAME=$(echo "$output" | python3 -c "import sys,json; print(json.load(sys.stdin)['results'][0]['name'])")
ALPHA_FIRST=$(echo "$output" | python3 -c "import sys,json; print(sorted(a['name'] for a in json.load(sys.stdin)['results'])[0])")
if [ "$FIRST_NAME" != "$ALPHA_FIRST" ]; then
    pass "authors list --sort name --desc starts with last name alphabetically"
else
    fail "authors list --sort name --desc starts with last name alphabetically" "first result was the alphabetically-first name"
fi

output=$($CLI authors get --id "$AUTHOR_ID" 2>/dev/null)
assert_json_key "authors get has id" "id" "$output"
assert_json_expr "authors get is Brandon Sanderson" "d['name']=='Brandon Sanderson'" "$output"
```

The remainder of the Authors section (the `# --- lookup ---` block onwards) needs no changes.

- [ ] **Step 3: Verify bash syntax**

```bash
bash -n /workspaces/abs-cli/docker/smoke-test.sh
```

Expected: no output (clean parse).

- [ ] **Step 4: Run the smoke test against the live dev instance**

The dev compose stack must be up (per CLAUDE.md "Pre-PR verification"). The container IP is reachable from inside the dev container; resolve it freshly to handle restarts:

```bash
ABS_IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
echo "ABS_IP=$ABS_IP"
ABS_URL="http://$ABS_IP:80" bash /workspaces/abs-cli/docker/smoke-test.sh 2>&1 | tail -3
```

If the smoke script reports a build is needed (no `$CLI` env var set), it will publish an AOT binary first — that is fine. Expected: `Results: 145 passed, 0 failed` (the existing 143 plus 2 new pagination/sort assertions; the new authors-list assertions replace the previous shape assertions one-for-one for the count). If the actual count differs slightly because of how the assertions are grouped, the requirement is `0 failed`.

- [ ] **Step 5: Run a second time to confirm idempotency**

```bash
ABS_URL="http://$ABS_IP:80" bash /workspaces/abs-cli/docker/smoke-test.sh 2>&1 | tail -3
```

Expected: same `0 failed` result. The new assertions are read-only; idempotency is automatic.

- [ ] **Step 6: Commit**

```bash
git add docker/smoke-test.sh
git commit -m "test: smoke coverage for paginated authors list"
```

---

## Task 4: Final verification + push + PR

**Files:** none.

- [ ] **Step 1: Format check**

```bash
cd /workspaces/abs-cli
dotnet format AbsCli.sln --verify-no-changes
```

Expected: clean exit. If anything is reported, run `dotnet format AbsCli.sln`, review the diff, and amend the most recent relevant commit with `git commit --amend --no-edit` only if the only change is whitespace; otherwise create a new commit.

- [ ] **Step 2: Full test suite (Release config)**

```bash
dotnet test AbsCli.sln -c Release --nologo
```

Expected: all tests pass, including the new `AuthorsList_Help_*` cases.

- [ ] **Step 3: AOT publish + self-test**

```bash
dotnet publish src/AbsCli -c Release -o /tmp/abs-cli-publish
/tmp/abs-cli-publish/abs-cli self-test 2>&1 | tail -10
```

Expected: AOT publish succeeds with no `IL2026`/`IL3050` trim warnings. `self-test` exits 0. Author Models section still passes (it was added in Spec B and is unaffected by this change).

- [ ] **Step 4: Render the new help screen**

```bash
/tmp/abs-cli-publish/abs-cli authors list --help
```

Expected output includes:
- All four new options (`--limit`, `--page`, `--sort`, `--desc`) with sensible descriptions.
- `--library` description still present (no "or name" wording).
- Three examples (basic, paginated, sorted).
- A `Response shape:` section showing `"results"`, `"total"`, `"limit"`, `"page"` with one `AuthorItem` inside `results`.

- [ ] **Step 5: Re-run the smoke test against the AOT binary**

```bash
CLI=/tmp/abs-cli-publish/abs-cli ABS_URL="http://$ABS_IP:80" bash /workspaces/abs-cli/docker/smoke-test.sh 2>&1 | tail -3
```

Expected: `0 failed`. This catches any AOT-only regression that the Debug-mode test runner did not exercise.

- [ ] **Step 6: Push the branch**

```bash
git push -u origin feat/authors-list-pagination
```

- [ ] **Step 7: Open a pull request to main**

```bash
gh pr create --title "feat!: paginate authors list (v0.4.0 Spec A)" --body "$(cat <<'EOF'
## Summary

Implements [Spec A of v0.4.0](docs/specs/2026-05-08-authors-list-pagination.md):
\`abs-cli authors list\` switches from the unpaginated \`{authors: [...]}\`
shape to the \`PaginatedResponse\` shape used by \`items list\` and
\`series list\`, gaining \`--limit\`, \`--page\`, \`--sort\`, and \`--desc\`
flags.

The dual-shape behaviour ABS exposes at \`GET /api/libraries/:id/authors\`
(switching between \`{authors:[...]}\` and \`{results,...}\` based on
whether both \`limit\` and \`page\` are numeric) is hidden — the CLI
always sends numeric \`limit\` and \`page\` so the server consistently
returns the paginated shape.

Spec B (author modification) shipped in #31. Spec C (author image
management) is a separate PR for v0.4.0.

## Breaking change

- Response shape changes from \`{authors: [...]}\` to \`{results, total, limit, page}\`.
- Default behaviour changes from "return every author" to "return the first 50".
- Migration: pass \`--limit 999\` (or higher) to restore the previous all-in-one-shot behaviour, or update consumers to read \`d['results']\` instead of \`d['authors']\`.

The \`feat!:\` subject and \`BREAKING CHANGE:\` footer in the
implementation commit (\`feat!: paginate authors list\`) flag this for
the release skill's manual CHANGELOG step.

## Test plan

- [x] \`dotnet format --verify-no-changes\` clean
- [x] \`dotnet test\` — full suite green, including new \`AuthorsList_Help_*\` cases
- [x] AOT publish + \`self-test\` clean
- [x] \`authors list --help\` renders all four new flags with the paginated \`Response shape:\` sample
- [x] \`docker/smoke-test.sh\` against the dev ABS instance — paginated shape, page 0/1 round-trip, and reverse-sort assertions all pass
EOF
)"
```

- [ ] **Step 8: Verify the PR opens cleanly**

```bash
gh pr view --json url -q .url
```

Capture and report the PR URL.

---

## Self-Review

Spec coverage check (each section/requirement of the spec is implemented by a task):

- ✅ Command surface (4 new flags) — Task 2 Step 2.
- ✅ ABS endpoint always-paginated (numeric limit/page sent unconditionally) — Task 2 Step 1.
- ✅ JSON model (reuse PaginatedResponse, drop AuthorListResponse) — Task 2 Steps 3 + 4.
- ✅ Help-render tests for the new flags and shape — Task 1 + Task 2 Step 5.
- ✅ Smoke pagination round-trip and reverse-sort — Task 3.
- ✅ Breaking-change marker in commit (`feat!:` + `BREAKING CHANGE:` footer) — Task 2 Step 7.
- ✅ Final verification (format, full tests, AOT, smoke against AOT, push, PR) — Task 4.
- ✅ No `--filter` / `--minified` / `--include` flags — confirmed absent in Task 2 Step 2's command surface.
- ✅ No command-level Notes section — confirmed absent in Task 2 Step 2 (only the group-level Notes from Spec B remains).

Placeholder scan: no "TBD" / "TODO" / "implement later" / "similar to Task N" remain. Every code block is complete.

Type-consistency check:
- `AuthorsService.ListAsync(string libraryId, int limit, int? page, string? sort, bool desc)` — signature matches between the service implementation (Task 2 Step 1) and the action call site (Task 2 Step 2).
- `PaginatedResponse` — used identically in service return type, command's `WriteJson` typeinfo, and `AddResponseExample`.
- `AuthorItem` — referenced as the element type in `AddResponseExample(typeof(PaginatedResponse), typeof(AuthorItem))` and remains the un-deleted class in `Models/Author.cs`.
