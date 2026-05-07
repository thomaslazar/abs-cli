# Authors Modification (match, lookup, update, delete) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add four new verbs under `abs-cli authors` (`match`, `lookup`, `update`, `delete`) that wrap the four ABS endpoints we don't currently expose, so an agent can identify unmatched authors, probe Audnexus, apply matches, edit metadata, and delete authors — without leaving the CLI.

**Architecture:** Each command is a thin pass-through to a single ABS API call (no smart defaults that pre-fetch, no response decoration). New `AuthorMatchRequest`, `AuthorMatchResponse`, and `AuthorUpdateResponse` models register in `AppJsonContext`. Existing `AuthorsService` gains four methods (`MatchAsync`, `LookupAsync`, `UpdateAsync`, `DeleteAsync`). `update` builds its body as `Dictionary<string, string>` (already registered) so it can express the tri-state of "field absent / field cleared / field set" the PATCH endpoint distinguishes. Caveats (merge-on-rename, single-result reduction, destructive overwrite) are surfaced explicitly in each command's `--help` per project convention. `lookup` uses `WriteRawJson` because the upstream Audnexus shape is provider-defined.

**Tech Stack:** C# / .NET 10 / `System.CommandLine` 2.0.7 / `System.Text.Json` source-gen. No new packages.

**Spec:** [docs/specs/2026-05-07-authors-modification.md](../specs/2026-05-07-authors-modification.md)

---

## File Structure

**New files:**

- `src/AbsCli/Models/AuthorRequests.cs` — `AuthorMatchRequest`.
- `src/AbsCli/Models/AuthorResponses.cs` — `AuthorMatchResponse`, `AuthorUpdateResponse`.
- `tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs` — help-rendering and arg-validation tests for the four new subcommands.

**Modified files:**

- `src/AbsCli/Models/JsonContext.cs` — register the three new models via `[JsonSerializable]`.
- `src/AbsCli/Api/ApiEndpoints.cs` — add `AuthorMatch(string id)` and `SearchAuthors` constants/helpers.
- `src/AbsCli/Services/AuthorsService.cs` — add `MatchAsync`, `LookupAsync`, `UpdateAsync`, `DeleteAsync`.
- `src/AbsCli/Commands/AuthorsCommand.cs` — extend group-level "Notes" section; register four new subcommands via factories.
- `src/AbsCli/Commands/SelfTestCommand.cs` — round-trip checks for the three new models.
- `docker/smoke-test.sh` — extend the `=== Authors Commands ===` section with lookup/match/update/delete.

**Files explicitly NOT modified:**

- `CHANGELOG.md` — owned by release workflow.
- `docs/roadmap.md` — kept at feature-level abstraction (no spec-mapping additions).
- Existing `AuthorsService.ListAsync` / `GetAsync`, `AuthorItem` / `AuthorListResponse` models — pre-existing and unchanged.

---

## Task 0: Commit the plan file

**Files:** none new (plan file already exists on disk).

- [ ] **Step 1: Confirm branch state**

```bash
cd /workspaces/abs-cli
git status
git log --oneline -3
```

Expected: on branch `feat/authors-modification`; HEAD includes `docs: spec for v0.4.0 authors modification`; working tree shows the plan file as untracked.

- [ ] **Step 2: Commit the plan**

```bash
git add docs/plans/2026-05-07-authors-modification.md
git commit -m "docs: plan for v0.4.0 authors modification"
```

---

## Task 1: Author request and response models + JsonContext

**Files:**
- Create: `src/AbsCli/Models/AuthorRequests.cs`
- Create: `src/AbsCli/Models/AuthorResponses.cs`
- Modify: `src/AbsCli/Models/JsonContext.cs`

- [ ] **Step 1: Create `src/AbsCli/Models/AuthorRequests.cs`**

```csharp
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Body for POST /api/authors/:id/match. Either <see cref="Q"/> (name) or
/// <see cref="Asin"/> is supplied; never both. <see cref="Region"/> is
/// optional — ABS defaults to "us" when absent.
/// </summary>
public class AuthorMatchRequest
{
    [JsonPropertyName("q")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Q { get; set; }

    [JsonPropertyName("asin")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Asin { get; set; }

    [JsonPropertyName("region")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Region { get; set; }
}
```

- [ ] **Step 2: Create `src/AbsCli/Models/AuthorResponses.cs`**

```csharp
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Response from POST /api/authors/:id/match.
/// Server returns { updated: bool, author: { ... } }.
/// </summary>
public class AuthorMatchResponse
{
    [JsonPropertyName("updated")]
    public bool Updated { get; set; }

    [JsonPropertyName("author")]
    public AuthorItem? Author { get; set; }
}

/// <summary>
/// Response from PATCH /api/authors/:id. Two response shapes share this
/// type because the merge path returns a different discriminator:
///   normal: { updated: bool, author: {...} }
///   merge:  { merged: true, author: <existingAuthor> }
/// One of <see cref="Updated"/> / <see cref="Merged"/> is present per
/// response. Consumers read whichever bool is non-null.
/// </summary>
public class AuthorUpdateResponse
{
    [JsonPropertyName("updated")]
    public bool? Updated { get; set; }

    [JsonPropertyName("merged")]
    public bool? Merged { get; set; }

    [JsonPropertyName("author")]
    public AuthorItem? Author { get; set; }
}
```

- [ ] **Step 3: Register in `src/AbsCli/Models/JsonContext.cs`**

Add three `[JsonSerializable]` attributes alongside the existing `AuthorItem` / `AuthorListResponse` lines (currently `JsonContext.cs:21-22`). Insert them immediately after `[JsonSerializable(typeof(AuthorListResponse))]`:

```csharp
[JsonSerializable(typeof(AuthorMatchRequest))]
[JsonSerializable(typeof(AuthorMatchResponse))]
[JsonSerializable(typeof(AuthorUpdateResponse))]
```

- [ ] **Step 4: Build and verify**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
```

Expected: 0 warnings, 0 errors. The source generator picks up the new types and produces accessors at `AppJsonContext.Default.AuthorMatchRequest`, `AppJsonContext.Default.AuthorMatchResponse`, `AppJsonContext.Default.AuthorUpdateResponse`.

- [ ] **Step 5: Commit**

```bash
git add src/AbsCli/Models/AuthorRequests.cs src/AbsCli/Models/AuthorResponses.cs src/AbsCli/Models/JsonContext.cs
git commit -m "feat: add author match/update request and response models"
```

---

## Task 2: Self-test round-trip checks

Catch any AOT serialization regression in the published binary.

**Files:**
- Modify: `src/AbsCli/Commands/SelfTestCommand.cs`

- [ ] **Step 1: Locate the cover-models section in `SelfTestCommand.cs`**

Run to find the insertion point:

```bash
grep -n "=== Cover Models ===\|=== Author Models ===\|=== Embedded" /workspaces/abs-cli/src/AbsCli/Commands/SelfTestCommand.cs
```

The new section goes immediately after the `=== Cover Models ===` block (and its associated checks) and before whatever follows it (likely `=== Embedded Resources ===` or end-of-file).

- [ ] **Step 2: Add `=== Author Models ===` section**

Add this block immediately after the last `Check(...)` call in the cover models section:

```csharp
            Console.Error.WriteLine();
            Console.Error.WriteLine("=== Author Models ===");

            Check("AuthorMatchRequest round-trip", () =>
            {
                var obj = new AuthorMatchRequest
                {
                    Q = "Brandon Sanderson",
                    Region = "us"
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.AuthorMatchRequest);
                Assert(json.Contains("\"q\""), $"q present: {json}");
                Assert(json.Contains("\"region\""), $"region present: {json}");
                Assert(!json.Contains("\"asin\""), $"asin absent (WhenWritingNull): {json}");
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorMatchRequest)!;
                Assert(back.Q == "Brandon Sanderson", $"q: {back.Q}");
                Assert(back.Region == "us", $"region: {back.Region}");
                Assert(back.Asin is null, $"asin: {back.Asin}");
            });

            Check("AuthorMatchResponse round-trip", () =>
            {
                var obj = new AuthorMatchResponse
                {
                    Updated = true,
                    Author = new AuthorItem { Id = "aut_xyz", Name = "Brandon Sanderson", Asin = "B000AP9DSU" }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.AuthorMatchResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorMatchResponse)!;
                Assert(back.Updated == true, $"updated: {back.Updated}");
                Assert(back.Author?.Name == "Brandon Sanderson", $"author.name: {back.Author?.Name}");
                Assert(back.Author?.Asin == "B000AP9DSU", $"author.asin: {back.Author?.Asin}");
            });

            Check("AuthorUpdateResponse normal-shape round-trip", () =>
            {
                var obj = new AuthorUpdateResponse
                {
                    Updated = true,
                    Author = new AuthorItem { Id = "aut_xyz", Name = "Brandon Sanderson" }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.AuthorUpdateResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorUpdateResponse)!;
                Assert(back.Updated == true, $"updated: {back.Updated}");
                Assert(back.Merged is null, $"merged: {back.Merged}");
                Assert(back.Author?.Name == "Brandon Sanderson", $"author.name: {back.Author?.Name}");
            });

            Check("AuthorUpdateResponse merge-shape round-trip", () =>
            {
                var json = "{\"merged\":true,\"author\":{\"id\":\"aut_existing\",\"name\":\"Brandon Sanderson\"}}";
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorUpdateResponse)!;
                Assert(back.Merged == true, $"merged: {back.Merged}");
                Assert(back.Updated is null, $"updated: {back.Updated}");
                Assert(back.Author?.Id == "aut_existing", $"author.id: {back.Author?.Id}");
            });

            Check("Update-body Dictionary tri-state serialization", () =>
            {
                var body = new Dictionary<string, string>
                {
                    ["name"] = "Brandon Sanderson",
                    ["description"] = null!,
                    ["asin"] = "B000AP9DSU"
                };
                var json = JsonSerializer.Serialize(body, AppJsonContext.Default.DictionaryStringString);
                Assert(json.Contains("\"name\":\"Brandon Sanderson\""), $"name: {json}");
                Assert(json.Contains("\"description\":null"), $"description null: {json}");
                Assert(json.Contains("\"asin\":\"B000AP9DSU\""), $"asin: {json}");
            });
```

- [ ] **Step 3: Build and run self-test**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
dotnet run --project /workspaces/abs-cli/src/AbsCli -c Debug -- self-test
```

Expected: all checks pass; the new "=== Author Models ===" section prints to stderr with `OK` next to each of the five new checks.

- [ ] **Step 4: Commit**

```bash
git add src/AbsCli/Commands/SelfTestCommand.cs
git commit -m "test: self-test round-trip for author match/update models"
```

---

## Task 3: API endpoints

**Files:**
- Modify: `src/AbsCli/Api/ApiEndpoints.cs`

- [ ] **Step 1: Add the two new endpoints**

In `src/AbsCli/Api/ApiEndpoints.cs`, locate the existing `AuthorById` line:

```csharp
    public static string AuthorById(string id) => $"/api/authors/{id}";
```

Add immediately below it:

```csharp
    public static string AuthorMatch(string id) => $"/api/authors/{id}/match";
```

Then locate the metadata search constants block (currently around line 39-41 with `SearchBooks`, `SearchProviders`, `SearchCovers`). Add a new constant alongside them:

```csharp
    public const string SearchAuthors = "/api/search/authors";
```

- [ ] **Step 2: Build**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/AbsCli/Api/ApiEndpoints.cs
git commit -m "feat: add author match and search endpoints"
```

---

## Task 4: AuthorsService — match, lookup, update, delete

**Files:**
- Modify: `src/AbsCli/Services/AuthorsService.cs`

- [ ] **Step 1: Replace the file contents**

Open `src/AbsCli/Services/AuthorsService.cs` and replace its contents with:

```csharp
using System.Text.Json;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class AuthorsService
{
    private readonly AbsApiClient _client;

    public AuthorsService(AbsApiClient client)
    {
        _client = client;
    }

    public async Task<AuthorListResponse> ListAsync(string libraryId)
    {
        return await _client.GetAsync(ApiEndpoints.LibraryAuthors(libraryId), AppJsonContext.Default.AuthorListResponse);
    }

    public async Task<AuthorItem> GetAsync(string id)
    {
        return await _client.GetAsync(ApiEndpoints.AuthorById(id) + "?include=items", AppJsonContext.Default.AuthorItem);
    }

    public async Task<AuthorMatchResponse> MatchAsync(string id, AuthorMatchRequest request)
    {
        var json = JsonSerializer.Serialize(request, AppJsonContext.Default.AuthorMatchRequest);
        return await _client.PostAsync(
            ApiEndpoints.AuthorMatch(id),
            json,
            AppJsonContext.Default.AuthorMatchResponse,
            "'update' permission");
    }

    public async Task<string> LookupAsync(string name)
    {
        var endpoint = ApiEndpoints.SearchAuthors + "?q=" + Uri.EscapeDataString(name);
        return await _client.GetAsync(endpoint);
    }

    public async Task<AuthorUpdateResponse> UpdateAsync(string id, Dictionary<string, string> body)
    {
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.DictionaryStringString);
        return await _client.PatchAsync(
            ApiEndpoints.AuthorById(id),
            json,
            AppJsonContext.Default.AuthorUpdateResponse,
            "'update' permission");
    }

    public async Task DeleteAsync(string id)
    {
        await _client.DeleteAsync(ApiEndpoints.AuthorById(id), "'delete' permission");
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
```

Expected: 0 errors. (Note: `LookupAsync` returns `string` — raw JSON pass-through. `DeleteAsync` returns `Task` — caller doesn't use the empty 200 body.)

- [ ] **Step 3: Commit**

```bash
git add src/AbsCli/Services/AuthorsService.cs
git commit -m "feat: extend AuthorsService with match/lookup/update/delete"
```

---

## Task 5: Group-level help "Notes" extension

**Files:**
- Modify: `src/AbsCli/Commands/AuthorsCommand.cs`

- [ ] **Step 1: Extend the existing "Notes" help section**

In `src/AbsCli/Commands/AuthorsCommand.cs`, locate the existing `AddHelpSection("Notes", HelpSectionPosition.Top, ...)` call (currently `AuthorsCommand.cs:13-18`). Replace it with this expanded version that adds two extra paragraphs about provider behavior:

```csharp
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Authors are derived from book metadata. An author record exists while at",
            "least one library item references it. When the last referencing item is",
            "removed or re-tagged, the scanner deletes the author on its next run",
            "(unless a custom image is set). To remove an author, update the books",
            "that reference it.",
            "",
            "Author matching uses the Audnexus provider (audnex.us), the same backend",
            "the ABS web UI uses. 'match' writes ASIN/description/image onto the ABS",
            "author record; 'lookup' is a read-only probe that does not touch ABS",
            "state.");
```

- [ ] **Step 2: Build and render help**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
dotnet run --project /workspaces/abs-cli/src/AbsCli -c Debug -- authors --help
```

Expected: the help shows both the existing scanner-lifecycle paragraph and the new Audnexus-provider paragraph.

- [ ] **Step 3: Commit**

```bash
git add src/AbsCli/Commands/AuthorsCommand.cs
git commit -m "docs: extend authors --help with Audnexus provider note"
```

---

## Task 6: `authors match` command + tests

**Files:**
- Modify: `src/AbsCli/Commands/AuthorsCommand.cs`
- Create: `tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs`

- [ ] **Step 1: Create the test file with failing help-render tests**

Create `tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs` with the following content:

```csharp
using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class AuthorsCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(AuthorsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Authors_TopLevel_Help_ListsAllSixVerbs()
    {
        var output = RenderHelp("authors");
        Assert.Contains("list", output);
        Assert.Contains("get", output);
        Assert.Contains("match", output);
        Assert.Contains("lookup", output);
        Assert.Contains("update", output);
        Assert.Contains("delete", output);
    }

    [Fact]
    public void AuthorsMatch_Help_DocumentsArgs()
    {
        var output = RenderHelp("authors", "match");
        Assert.Contains("--id", output);
        Assert.Contains("--name", output);
        Assert.Contains("--asin", output);
        Assert.Contains("--region", output);
    }

    [Fact]
    public void AuthorsMatch_Help_DocumentsDestructiveAndAmbiguityCaveats()
    {
        var output = RenderHelp("authors", "match");
        Assert.Contains("Destructive", output);
        Assert.Contains("Levenshtein", output);
        Assert.Contains("ASIN", output);
    }

    [Fact]
    public void AuthorsMatch_Help_ShowsResponseShape()
    {
        var output = RenderHelp("authors", "match");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"updated\"", output);
        Assert.Contains("\"author\"", output);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~AuthorsCommandTests" -c Debug --nologo
```

Expected: four tests fail. `Authors_TopLevel_Help_ListsAllSixVerbs` fails because `match`/`lookup`/`update`/`delete` aren't registered yet. The three `AuthorsMatch_Help_*` tests fail because the subcommand doesn't exist.

- [ ] **Step 3: Implement `CreateMatchCommand` in `AuthorsCommand.cs`**

Add the following private method to `AuthorsCommand` (place it after the existing `CreateGetCommand`):

```csharp
    private static Command CreateMatchCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var nameOption = new Option<string?>("--name") { Description = "Search Audnexus by name (mutually exclusive with --asin)" };
        var asinOption = new Option<string?>("--asin") { Description = "Look up Audnexus author by ASIN (mutually exclusive with --name)" };
        var regionOption = new Option<string?>("--region") { Description = "Audnexus region override (defaults to 'us' server-side)" };
        var command = new Command("match", "Apply Audnexus author data to an existing ABS author")
        {
            idOption, nameOption, asinOption, regionOption
        };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Destructive on hit: writes ASIN, description, and image onto the author",
            "and emits an 'author_updated' socket event. Image is written only when",
            "the author had no prior image or the ASIN changed.",
            "",
            "Audnexus may return multiple candidates for a name. ABS picks the closest",
            "Levenshtein match and silently discards alternatives — for two real-world",
            "authors with the same name, the wrong one may be picked. Pass --asin to",
            "disambiguate.",
            "",
            "404 means 'no upstream match found' — useful when scanning for unmatched",
            "authors. The ABS author record is untouched on 404.");
        command.AddExamples(
            "abs-cli authors match --id \"aut_xyz\" --name \"Brandon Sanderson\"",
            "abs-cli authors match --id \"aut_xyz\" --asin \"B000AP9DSU\"",
            "abs-cli authors match --id \"aut_xyz\" --name \"Bob Bunyon\" --region \"uk\"");
        command.AddResponseExample<AuthorMatchResponse>();
        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var name = parseResult.GetValue(nameOption);
            var asin = parseResult.GetValue(asinOption);
            var region = parseResult.GetValue(regionOption);
            var sources = new[] { name, asin }.Count(s => !string.IsNullOrEmpty(s));
            if (sources != 1)
            {
                ConsoleOutput.WriteError("Specify exactly one of --name or --asin");
                Environment.Exit(1);
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            var request = new AuthorMatchRequest
            {
                Q = string.IsNullOrEmpty(name) ? null : name,
                Asin = string.IsNullOrEmpty(asin) ? null : asin,
                Region = string.IsNullOrEmpty(region) ? null : region
            };
            var result = await service.MatchAsync(id, request);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.AuthorMatchResponse);
        });
        return command;
    }
```

- [ ] **Step 4: Register the subcommand**

In the existing `Create()` method body, add:

```csharp
        command.Subcommands.Add(CreateMatchCommand());
```

immediately after the `command.Subcommands.Add(CreateGetCommand());` line.

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~AuthorsCommandTests" -c Debug --nologo
```

Expected: all four match tests pass. (`Authors_TopLevel_Help_ListsAllSixVerbs` may still fail if the other three subcommands haven't been added — that's fine; we'll fix it in Tasks 7-9.)

- [ ] **Step 6: Commit**

```bash
git add src/AbsCli/Commands/AuthorsCommand.cs tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs
git commit -m "feat: add 'authors match' command"
```

---

## Task 7: `authors lookup` command + tests

**Files:**
- Modify: `src/AbsCli/Commands/AuthorsCommand.cs`
- Modify: `tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs`

- [ ] **Step 1: Add failing help tests for `lookup`**

Append to `AuthorsCommandTests` (immediately before the closing `}` of the class):

```csharp
    [Fact]
    public void AuthorsLookup_Help_DocumentsNameOnly()
    {
        var output = RenderHelp("authors", "lookup");
        Assert.Contains("--name", output);
        Assert.DoesNotContain("--id", output);
        Assert.DoesNotContain("--asin", output);
        Assert.DoesNotContain("--region", output);
    }

    [Fact]
    public void AuthorsLookup_Help_DocumentsReadOnlyAndNullCaveats()
    {
        var output = RenderHelp("authors", "lookup");
        Assert.Contains("Read-only", output);
        Assert.Contains("null", output);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~AuthorsLookup_Help" -c Debug --nologo
```

Expected: both tests fail (subcommand not yet registered).

- [ ] **Step 3: Implement `CreateLookupCommand` in `AuthorsCommand.cs`**

Add this private method after `CreateMatchCommand`:

```csharp
    private static Command CreateLookupCommand()
    {
        var nameOption = new Option<string>("--name") { Description = "Author name to search Audnexus", Required = true };
        var command = new Command("lookup", "Read-only Audnexus probe by author name") { nameOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Read-only Audnexus probe. Does not touch any ABS author record.",
            "",
            "ABS reduces multiple Audnexus candidates to the closest-Levenshtein single",
            "match. The candidate list is not exposed.",
            "",
            "Returns the literal JSON 'null' (HTTP 200) when no match is found — agents",
            "check the JSON value, not the HTTP status. The CLI does not exit non-zero",
            "for this case.",
            "",
            "No --region (the underlying endpoint does not accept one); no ASIN lookup",
            "path (this is a name-only search).");
        command.AddExamples(
            "abs-cli authors lookup --name \"Brandon Sanderson\"");
        command.SetAction(async parseResult =>
        {
            var name = parseResult.GetValue(nameOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            var json = await service.LookupAsync(name);
            ConsoleOutput.WriteRawJson(json);
        });
        return command;
    }
```

- [ ] **Step 4: Register the subcommand**

In `Create()`, add immediately after the `CreateMatchCommand()` line:

```csharp
        command.Subcommands.Add(CreateLookupCommand());
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~AuthorsLookup_Help" -c Debug --nologo
```

Expected: both lookup tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/AbsCli/Commands/AuthorsCommand.cs tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs
git commit -m "feat: add 'authors lookup' command"
```

---

## Task 8: `authors update` command + tests

The most intricate of the four — must build the body as `Dictionary<string, string>` to express the tri-state of "field absent / cleared / set". An additional unit test covers the body-building logic so a regression there is caught without a live ABS call.

**Files:**
- Modify: `src/AbsCli/Commands/AuthorsCommand.cs`
- Modify: `tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs`

- [ ] **Step 1: Add failing help and body-building tests**

Append to `AuthorsCommandTests`:

```csharp
    [Fact]
    public void AuthorsUpdate_Help_DocumentsAllThreeFields()
    {
        var output = RenderHelp("authors", "update");
        Assert.Contains("--id", output);
        Assert.Contains("--name", output);
        Assert.Contains("--description", output);
        Assert.Contains("--asin", output);
    }

    [Fact]
    public void AuthorsUpdate_Help_DocumentsMergeOnRenameCaveat()
    {
        var output = RenderHelp("authors", "update");
        Assert.Contains("merge", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rename", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuthorsUpdate_Help_DocumentsClearSemantics()
    {
        var output = RenderHelp("authors", "update");
        Assert.Contains("Empty string", output);
        Assert.Contains("clear", output);
    }

    [Fact]
    public void AuthorsUpdate_Help_ShowsResponseShape()
    {
        var output = RenderHelp("authors", "update");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"updated\"", output);
        Assert.Contains("\"author\"", output);
    }

    [Theory]
    [InlineData("Foo",  null,  null,  "{\"name\":\"Foo\"}")]
    [InlineData(null,   "",    null,  "{\"description\":null}")]
    [InlineData(null,   null,  "",    "{\"asin\":null}")]
    [InlineData(null,   "Bio", null,  "{\"description\":\"Bio\"}")]
    [InlineData("Foo",  "",    "",    "{\"name\":\"Foo\",\"description\":null,\"asin\":null}")]
    public void AuthorsUpdate_BuildBody_TriState(string? name, string? description, string? asin, string expected)
    {
        var body = AuthorsCommand.BuildUpdateBodyForTesting(name, description, asin);
        var json = System.Text.Json.JsonSerializer.Serialize(
            body, AbsCli.Models.AppJsonContext.Default.DictionaryStringString);
        // STJ doesn't promise key order across runtimes, but the dictionary is
        // small and we insert in a fixed order, so a substring check on each
        // expected key/value pair is the robust assertion.
        var expectedFragments = expected.Trim('{', '}').Split(',');
        foreach (var fragment in expectedFragments)
        {
            if (!string.IsNullOrEmpty(fragment))
                Assert.Contains(fragment, json);
        }
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~AuthorsUpdate" -c Debug --nologo
```

Expected: all five tests fail. The four help tests fail because the subcommand isn't registered. The theory fails to compile because `BuildUpdateBodyForTesting` doesn't exist (build error reported in test runner output).

- [ ] **Step 3: Implement `CreateUpdateCommand` in `AuthorsCommand.cs`**

Add this private method after `CreateLookupCommand`. Note the `internal` accessor on the body-building helper so the test project (which references the assembly) can call it:

```csharp
    private static Command CreateUpdateCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var nameOption = new Option<string?>("--name") { Description = "New name (renaming to an existing name in the same library merges authors — see Notes)" };
        var descriptionOption = new Option<string?>("--description") { Description = "New description; empty string clears the field" };
        var asinOption = new Option<string?>("--asin") { Description = "New ASIN; empty string clears the field" };
        var command = new Command("update", "Edit an author's name, description, and/or ASIN")
        {
            idOption, nameOption, descriptionOption, asinOption
        };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Merge-on-rename: if --name is set to a name that already exists in the",
            "same library, ABS auto-merges the two authors. All books of the source",
            "author are reassigned to the existing author and the source author is",
            "deleted. Books are not lost; the operation is recoverable by re-editing",
            "books. The response becomes { merged: true, author: <existingAuthor> }",
            "instead of the usual { updated, author }.",
            "",
            "When the merge path runs, any also-supplied --description / --asin is",
            "silently dropped. The merged-into author keeps its original description",
            "and asin.",
            "",
            "Empty string for --description or --asin clears the field on the server",
            "(JSON null). Empty --name is rejected client-side. At least one of",
            "--name / --description / --asin must be supplied.");
        command.AddExamples(
            "abs-cli authors update --id \"aut_xyz\" --name \"Brandon Sanderson\"",
            "abs-cli authors update --id \"aut_xyz\" --description \"American author of high fantasy\"",
            "abs-cli authors update --id \"aut_xyz\" --asin \"\"",
            "abs-cli authors update --id \"aut_xyz\" --name \"Brandon Sanderson\" --description \"American author of high fantasy\" --asin \"B000AP9DSU\"");
        command.AddResponseExample<AuthorUpdateResponse>();
        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var name = parseResult.GetValue(nameOption);
            var description = parseResult.GetValue(descriptionOption);
            var asin = parseResult.GetValue(asinOption);
            if (name is not null && string.IsNullOrEmpty(name))
            {
                ConsoleOutput.WriteError("--name cannot be empty");
                Environment.Exit(1);
            }
            var body = BuildUpdateBodyForTesting(name, description, asin);
            if (body.Count == 0)
            {
                ConsoleOutput.WriteError("Specify at least one of --name, --description, --asin");
                Environment.Exit(1);
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            var result = await service.UpdateAsync(id, body);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.AuthorUpdateResponse);
        });
        return command;
    }

    /// <summary>
    /// Build the PATCH body honouring the tri-state semantics: null = field
    /// absent (omit from JSON), empty string = clear (send JSON null),
    /// non-empty = set value. Exposed internally for unit testing.
    /// </summary>
    internal static Dictionary<string, string> BuildUpdateBodyForTesting(string? name, string? description, string? asin)
    {
        var body = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(name))
            body["name"] = name;
        if (description is not null)
            body["description"] = description == "" ? null! : description;
        if (asin is not null)
            body["asin"] = asin == "" ? null! : asin;
        return body;
    }
```

- [ ] **Step 4: Register the subcommand**

In `Create()`, add immediately after the `CreateLookupCommand()` line:

```csharp
        command.Subcommands.Add(CreateUpdateCommand());
```

- [ ] **Step 5: Add `InternalsVisibleTo` so the test project can see `BuildUpdateBodyForTesting`**

Check whether the project already exposes internals to the test assembly:

```bash
grep -rn "InternalsVisibleTo\|<InternalsVisibleTo>" /workspaces/abs-cli/src/AbsCli/
```

If the search returns nothing, add the following to `src/AbsCli/AbsCli.csproj` inside any existing `<ItemGroup>` (or as a new `<ItemGroup>`):

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="AbsCli.Tests" />
  </ItemGroup>
```

If the search already shows `AbsCli.Tests` exposed, skip this sub-step.

- [ ] **Step 6: Run tests to verify they pass**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~AuthorsUpdate" -c Debug --nologo
```

Expected: all five update tests pass (four help + one theory with five inline cases).

- [ ] **Step 7: Commit**

```bash
git add src/AbsCli/Commands/AuthorsCommand.cs tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs src/AbsCli/AbsCli.csproj
git commit -m "feat: add 'authors update' command with tri-state body"
```

---

## Task 9: `authors delete` command + tests

**Files:**
- Modify: `src/AbsCli/Commands/AuthorsCommand.cs`
- Modify: `tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs`

- [ ] **Step 1: Add failing help tests**

Append to `AuthorsCommandTests`:

```csharp
    [Fact]
    public void AuthorsDelete_Help_RequiresIdOnly()
    {
        var output = RenderHelp("authors", "delete");
        Assert.Contains("--id", output);
        Assert.DoesNotContain("--name", output);
        Assert.DoesNotContain("--asin", output);
        Assert.DoesNotContain("--description", output);
    }

    [Fact]
    public void AuthorsDelete_Help_DocumentsHardDeleteCaveat()
    {
        var output = RenderHelp("authors", "delete");
        Assert.Contains("removes the author from all books", output, StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~AuthorsDelete" -c Debug --nologo
```

Expected: both tests fail (subcommand not registered).

- [ ] **Step 3: Implement `CreateDeleteCommand`**

Add to `AuthorsCommand` after `CreateUpdateCommand`:

```csharp
    private static Command CreateDeleteCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var command = new Command("delete", "Delete an author and unlink it from all books") { idOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Removes the author from all books and deletes the record. Books lose",
            "their author tag; the scanner may re-derive the author on its next run",
            "if file metadata still references the name (see the group-level lifecycle",
            "note).",
            "",
            "No confirmation prompt — consistent with 'backup delete' and 'items cover",
            "remove'. Mirrors the ABS web UI's delete behaviour.");
        command.AddExamples(
            "abs-cli authors delete --id \"aut_xyz\"");
        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            await service.DeleteAsync(id);
            ConsoleOutput.WriteJson(new Dictionary<string, string> { ["success"] = "true" });
        });
        return command;
    }
```

- [ ] **Step 4: Register the subcommand**

In `Create()`, add immediately after the `CreateUpdateCommand()` line:

```csharp
        command.Subcommands.Add(CreateDeleteCommand());
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~AuthorsCommandTests" -c Debug --nologo
```

Expected: every test in `AuthorsCommandTests` passes — including the earlier `Authors_TopLevel_Help_ListsAllSixVerbs` that has been waiting for all four subcommands to be registered.

- [ ] **Step 6: Commit**

```bash
git add src/AbsCli/Commands/AuthorsCommand.cs tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs
git commit -m "feat: add 'authors delete' command"
```

---

## Task 10: Smoke-test integration

**Files:**
- Modify: `docker/smoke-test.sh`

- [ ] **Step 1: Locate the Authors section**

```bash
grep -n "=== Authors Commands ===" /workspaces/abs-cli/docker/smoke-test.sh
```

- [ ] **Step 2: Extend the Authors section**

In `docker/smoke-test.sh`, the existing Authors section ends after the `authors get` block (around line 327-330). Replace the contents of the Authors section (between `=== Authors Commands ===` and the next `============================================================` block) with the version below. It keeps the existing list/get assertions intact and appends lookup/match/update/delete coverage.

```bash
# ============================================================
echo ""
echo "=== Authors Commands ==="
# ============================================================

output=$($CLI authors list 2>/dev/null)
assert_json_key "authors list has authors" "authors" "$output"
assert_json_expr "authors list has 6 authors" "len(d['authors'])==6" "$output"
assert_json_expr "authors list contains Brandon Sanderson" \
    "any(a['name']=='Brandon Sanderson' for a in d['authors'])" "$output"

AUTHOR_ID=$(echo "$output" | python3 -c "
import sys,json
authors = json.load(sys.stdin)['authors']
bs = next(a for a in authors if a['name']=='Brandon Sanderson')
print(bs['id'])
")

output=$($CLI authors get --id "$AUTHOR_ID" 2>/dev/null)
assert_json_key "authors get has id" "id" "$output"
assert_json_expr "authors get is Brandon Sanderson" "d['name']=='Brandon Sanderson'" "$output"

# --- lookup ---
output=$($CLI authors lookup --name "Brandon Sanderson" 2>/dev/null)
assert_json_expr "authors lookup returns object for known author" \
    "isinstance(d, dict) and d.get('name')" "$output"

output=$($CLI authors lookup --name "ZzzNotARealAuthorXyz" 2>/dev/null)
# null body deserialises to Python None
assert_test "authors lookup returns null for missing author" \
    "[ \"$output\" = \"null\" ]"

# --- match ---
output=$($CLI authors match --id "$AUTHOR_ID" --name "Brandon Sanderson" 2>/dev/null)
assert_json_key "authors match returns updated key" "updated" "$output"
assert_json_key "authors match returns author key" "author" "$output"

# --- update (description set, then clear) ---
output=$($CLI authors update --id "$AUTHOR_ID" --description "Smoke-test description" 2>/dev/null)
assert_json_key "authors update returns updated key" "updated" "$output"
assert_json_expr "authors update set description" \
    "d['author']['description']=='Smoke-test description'" "$output"

output=$($CLI authors update --id "$AUTHOR_ID" --description "" 2>/dev/null)
assert_json_expr "authors update cleared description" \
    "d['author'].get('description') in (None, '')" "$output"

# --- delete (against a throwaway author created via PATCH side-effect of merge,
#     skipped here to avoid mutating the seeded library; covered in
#     integration tests with a dedicated test user / library)
echo "(authors delete: covered in integration suite, not smoke)"
```

- [ ] **Step 3: Run the smoke test**

```bash
cd /workspaces/abs-cli/docker
./smoke-test.sh
```

Expected: every new assertion passes. Note: this requires the dev ABS instance to be running and reachable per the existing smoke-test prerequisites.

- [ ] **Step 4: Commit**

```bash
git add docker/smoke-test.sh
git commit -m "test: smoke coverage for authors lookup/match/update"
```

---

## Task 11: Final verification

**Files:** none.

- [ ] **Step 1: Format**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: clean. If anything is reported, run `dotnet format /workspaces/abs-cli/AbsCli.sln`, review the diff, and amend the most recent relevant commit.

- [ ] **Step 2: Full test suite**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln -c Release --nologo
```

Expected: all tests pass, including the existing suite plus the new `AuthorsCommandTests`.

- [ ] **Step 3: AOT publish smoke**

```bash
dotnet publish /workspaces/abs-cli/src/AbsCli -c Release -o /tmp/abs-cli-publish
/tmp/abs-cli-publish/abs-cli self-test 2>&1 | tail -30
```

Expected: AOT publish succeeds with no `IL2026`/`IL3050` trim warnings. `self-test` exits 0 and the new "=== Author Models ===" section reports OK on every check.

- [ ] **Step 4: Render every new help screen**

```bash
/tmp/abs-cli-publish/abs-cli authors --help
/tmp/abs-cli-publish/abs-cli authors match --help
/tmp/abs-cli-publish/abs-cli authors lookup --help
/tmp/abs-cli-publish/abs-cli authors update --help
/tmp/abs-cli-publish/abs-cli authors delete --help
```

Expected:
- Group-level help shows the extended Notes section (lifecycle + Audnexus paragraphs).
- Each subcommand renders its Notes, options, examples, and (for match/update) Response shape.
- The merge-on-rename caveat is visible on `authors update --help`.
- The "Levenshtein"/"ASIN" disambiguation note is visible on `authors match --help`.

- [ ] **Step 5: Push the branch**

```bash
git push -u origin feat/authors-modification
```

- [ ] **Step 6: Open a pull request to main**

```bash
gh pr create --title "feat: authors match/lookup/update/delete (v0.4.0 Spec B)" --body "$(cat <<'EOF'
## Summary

Implements [Spec B of v0.4.0](docs/specs/2026-05-07-authors-modification.md):
four new verbs under \`abs-cli authors\` — \`match\`, \`lookup\`, \`update\`,
\`delete\` — wrapping the four ABS endpoints we did not previously expose.

- \`authors match --id X (--name … | --asin …) [--region …]\` — apply Audnexus author data.
- \`authors lookup --name "…"\` — read-only Audnexus probe.
- \`authors update --id X [--name …] [--description …] [--asin …]\` — patch editable fields, with empty-string clearing and the merge-on-rename behaviour faithfully surfaced.
- \`authors delete --id X\` — hard delete.

Each command is a thin pass-through to a single ABS API call. Every API caveat
(destructive overwrite, single-result Levenshtein reduction, merge-on-rename,
hard-delete cascade) is documented in the relevant \`--help\` per project
convention. \`update\` builds its body as \`Dictionary<string, string>\` to
express the tri-state of "field absent / cleared / set" the PATCH endpoint
distinguishes.

Pagination on \`authors list\` (Spec A) and image management (Spec C) ship in
separate PRs as part of v0.4.0.

## Test plan

- [x] \`dotnet format --verify-no-changes\` clean
- [x] \`dotnet test\` — full suite green, including new \`AuthorsCommandTests\`
- [x] AOT publish + \`self-test\` — new \`=== Author Models ===\` section passes
- [x] All five new \`--help\` screens render with Notes, options, examples, and Response shapes
- [x] \`docker/smoke-test.sh\` — extended Authors section passes against the dev ABS instance
EOF
)"
```

- [ ] **Step 7: Verify the PR opens cleanly**

```bash
gh pr view --web
```

Expected: PR shows the spec, plan, and 11 task commits in order.

---

## Self-Review (run after writing the plan, before handing off)

Spec-coverage check (each spec section has a task that implements it):

- ✅ Command surface (4 verbs, flag shapes) — Tasks 6–9.
- ✅ ABS endpoints touched — Tasks 3 (endpoints helper) and 4 (service).
- ✅ JSON handling (typed models, Dictionary tri-state, raw pass-through for lookup) — Tasks 1, 4, 8.
- ✅ Caveats in --help (group-level Audnexus note + per-command Notes) — Tasks 5–9.
- ✅ Permissions (canUpdate / canDelete) — Task 4 (passed via `permissionHint` strings to `AbsApiClient`).
- ✅ Tests (model round-trip + help + body-builder) — Tasks 2, 6–9.
- ✅ Smoke-test integration — Task 10.
- ✅ Final verification (format + tests + AOT + help render) — Task 11.

Placeholder scan: no "TBD" / "TODO" / "implement later" / "similar to Task N" remain. Every code block is complete; every command shows the full invocation.

Type-consistency check:

- `AuthorMatchRequest` properties (`Q`, `Asin`, `Region`) match between Task 1 (declaration), Task 2 (round-trip), Task 4 (service usage), Task 6 (command).
- `AuthorMatchResponse` properties (`Updated`, `Author`) — same.
- `AuthorUpdateResponse` properties (`Updated?`, `Merged?`, `Author?`) — same.
- `BuildUpdateBodyForTesting(string?, string?, string?)` signature matches between Task 8 declaration and the Theory call site.
- Service method names (`MatchAsync`, `LookupAsync`, `UpdateAsync`, `DeleteAsync`) match between Task 4 and Tasks 6–9.
