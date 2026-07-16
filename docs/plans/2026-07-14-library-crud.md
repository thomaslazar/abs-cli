# Library CRUD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `libraries create`, `update`, `delete`, and `reorder` — the four admin library-management endpoints — to the existing `libraries` command.

**Architecture:** Flags + repeatable `--folder` for create (precedents: `collections update` flags, `upload --files`); scalar flags for update; `--id` for delete (no prompt); `--input`/`--stdin` for reorder (precedent: `collections reorder`). New request models (`LibraryCreateRequest`/`LibraryFolderRequest`/`LibraryUpdateRequest`); responses reuse existing `Library` / `LibraryListResponse`. All four gated `admin`.

**Tech Stack:** C# / .NET, System.CommandLine, System.Text.Json source-gen (`AppJsonContext`), xUnit.

**Spec:** `docs/specs/2026-07-14-library-crud-design.md`

**Conventions:** No unnecessary blank lines in method bodies. `dotnet format AbsCli.sln` before each commit. Conventional Commits, imperative lowercase no period; NO `Co-Authored-By`, NO "Generated with Claude Code". Do NOT edit `CHANGELOG.md`. Permission tag `admin` ↔ hint `"admin permission"` (no quotes around admin).

---

## File Structure

New:
- `src/AbsCli/Models/LibraryRequests.cs` — `LibraryFolderRequest`, `LibraryCreateRequest`, `LibraryUpdateRequest`
- `tests/AbsCli.Tests/Services/LibrariesServiceTests.cs` — request round-trip tests

Modified:
- `src/AbsCli/Models/JsonContext.cs` — register the 3 request types
- `tools/GenerateResponseExamples/Program.cs` — exclude the 3 request types
- `src/AbsCli/Commands/ResponseExamples.g.cs` — regenerated
- `src/AbsCli/Api/ApiEndpoints.cs` — `LibrariesOrder`
- `src/AbsCli/Services/LibrariesService.cs` — `CreateAsync`/`UpdateAsync`/`DeleteAsync`/`ReorderAsync`
- `src/AbsCli/Commands/LibrariesCommand.cs` — 4 subcommands + logger field
- `tests/AbsCli.Tests/Commands/LibrariesCommandTests.cs` — new-verb tests (+ update the existing subcommand-set assertion)
- `tests/AbsCli.Tests/Api/ApiEndpointsTests.cs` — `LibrariesOrder` assertion
- `README.md`, `docs/abs-api-coverage.md`, `docker/smoke-test.sh`

---

## Task 1: Request models + JSON context

**Files:** Create `src/AbsCli/Models/LibraryRequests.cs`, `tests/AbsCli.Tests/Services/LibrariesServiceTests.cs`; modify `src/AbsCli/Models/JsonContext.cs`.

- [ ] **Step 1: Write failing tests**

Create `tests/AbsCli.Tests/Services/LibrariesServiceTests.cs`:

```csharp
using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class LibrariesServiceTests
{
    [Fact]
    public void LibraryCreateRequest_SerializesFoldersAndName()
    {
        var req = new LibraryCreateRequest
        {
            Name = "Audiobooks",
            Folders = new List<LibraryFolderRequest> { new() { FullPath = "/audiobooks" } }
        };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.LibraryCreateRequest);
        Assert.Contains("\"name\": \"Audiobooks\"", json);
        Assert.Contains("\"fullPath\": \"/audiobooks\"", json);
        // optional fields omitted when null
        Assert.DoesNotContain("mediaType", json);
        Assert.DoesNotContain("provider", json);
        Assert.DoesNotContain("icon", json);
    }

    [Fact]
    public void LibraryCreateRequest_IncludesOptionalsWhenSet()
    {
        var req = new LibraryCreateRequest
        {
            Name = "Pods",
            Folders = new List<LibraryFolderRequest> { new() { FullPath = "/pods" } },
            MediaType = "podcast",
            Provider = "itunes",
            Icon = "podcast"
        };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.LibraryCreateRequest);
        Assert.Contains("\"mediaType\": \"podcast\"", json);
        Assert.Contains("\"provider\": \"itunes\"", json);
        Assert.Contains("\"icon\": \"podcast\"", json);
    }

    [Fact]
    public void LibraryUpdateRequest_OmitsNullFields()
    {
        var req = new LibraryUpdateRequest { Name = "Renamed" };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.LibraryUpdateRequest);
        Assert.Contains("\"name\": \"Renamed\"", json);
        Assert.DoesNotContain("mediaType", json);
        Assert.DoesNotContain("displayOrder", json);
    }

    [Fact]
    public void LibraryUpdateRequest_IncludesDisplayOrder()
    {
        var req = new LibraryUpdateRequest { DisplayOrder = 3 };
        var json = JsonSerializer.Serialize(req, AppJsonContext.Default.LibraryUpdateRequest);
        Assert.Contains("\"displayOrder\": 3", json);
        Assert.DoesNotContain("\"name\"", json);
    }
}
```

Note: `AppJsonContext` uses `WriteIndented = true` (space after colon).

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test tests/AbsCli.Tests --filter LibrariesServiceTests`
Expected: FAIL (types missing).

- [ ] **Step 3: Create the models**

`src/AbsCli/Models/LibraryRequests.cs`:

```csharp
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>Folder entry for POST /api/libraries (server-side path).</summary>
public class LibraryFolderRequest
{
    [JsonPropertyName("fullPath")]
    public string FullPath { get; set; } = "";
}

/// <summary>Request body for POST /api/libraries. Null optionals are omitted (server defaults apply).</summary>
public class LibraryCreateRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("folders")]
    public List<LibraryFolderRequest> Folders { get; set; } = new();

    [JsonPropertyName("mediaType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaType { get; set; }

    [JsonPropertyName("provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Provider { get; set; }

    [JsonPropertyName("icon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; set; }
}

/// <summary>Request body for PATCH /api/libraries/:id. Null fields are omitted.</summary>
public class LibraryUpdateRequest
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("mediaType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaType { get; set; }

    [JsonPropertyName("provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Provider { get; set; }

    [JsonPropertyName("icon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; set; }

    [JsonPropertyName("displayOrder")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DisplayOrder { get; set; }
}
```

- [ ] **Step 4: Register in `JsonContext.cs`**

Add to the `[JsonSerializable(...)]` block on `AppJsonContext`:

```csharp
[JsonSerializable(typeof(LibraryFolderRequest))]
[JsonSerializable(typeof(LibraryCreateRequest))]
[JsonSerializable(typeof(LibraryUpdateRequest))]
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test tests/AbsCli.Tests --filter LibrariesServiceTests`
Expected: PASS (4 tests).

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Models/LibraryRequests.cs src/AbsCli/Models/JsonContext.cs tests/AbsCli.Tests/Services/LibrariesServiceTests.cs
git status --short   # if ResponseExamples.g.cs shows modified, do NOT commit it yet (Task 2 handles it): git checkout src/AbsCli/Commands/ResponseExamples.g.cs
git commit -m "feat: add library create/update request models"
```

---

## Task 2: Exclude request types from generator, regenerate

**Files:** Modify `tools/GenerateResponseExamples/Program.cs`, `src/AbsCli/Commands/ResponseExamples.g.cs`.

- [ ] **Step 1: Add to the exclusion set**

In `tools/GenerateResponseExamples/Program.cs`, in the `excluded` `HashSet<Type>` (already has `TagRenameRequest`/`GenreRenameRequest`/`NarratorRenameRequest`), add after those:

```csharp
            typeof(LibraryFolderRequest),
            typeof(LibraryCreateRequest),
            typeof(LibraryUpdateRequest),
```

- [ ] **Step 2: Regenerate**

Run: `dotnet run --project tools/GenerateResponseExamples -- src/AbsCli/Commands/ResponseExamples.g.cs`
Expected: exit 0.

- [ ] **Step 3: Verify**

```bash
grep -c "LibraryCreateRequest\|LibraryUpdateRequest\|LibraryFolderRequest" src/AbsCli/Commands/ResponseExamples.g.cs   # expect 0
```

- [ ] **Step 4: Drift test**

Run: `dotnet test tests/AbsCli.Tests --filter ResponseExamplesDriftTest`
Expected: PASS.

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add tools/GenerateResponseExamples/Program.cs src/AbsCli/Commands/ResponseExamples.g.cs
git commit -m "chore: exclude library request types from response examples"
```

---

## Task 3: Endpoint + service methods

**Files:** Modify `src/AbsCli/Api/ApiEndpoints.cs` (+ test), `src/AbsCli/Services/LibrariesService.cs`.

- [ ] **Step 1: Add failing endpoint test**

Append inside `ApiEndpointsTests`:

```csharp
    [Fact]
    public void LibrariesOrder_IsStable()
    {
        Assert.Equal("api/libraries/order", ApiEndpoints.LibrariesOrder);
    }
```

Run `dotnet test tests/AbsCli.Tests --filter ApiEndpointsTests` → FAIL.

- [ ] **Step 2: Add the endpoint**

In `src/AbsCli/Api/ApiEndpoints.cs`, near the other library consts (after `Libraries`):

```csharp
    public const string LibrariesOrder = "api/libraries/order";
```

Run `dotnet test tests/AbsCli.Tests --filter ApiEndpointsTests` → PASS.

- [ ] **Step 3: Add service methods**

Add `using System.Text.Json;` to `src/AbsCli/Services/LibrariesService.cs`, then add:

```csharp
    public async Task<Library> CreateAsync(LibraryCreateRequest body)
    {
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.LibraryCreateRequest);
        return await _client.PostAsync(ApiEndpoints.Libraries, json, AppJsonContext.Default.Library, "admin permission");
    }

    public async Task<Library> UpdateAsync(string id, LibraryUpdateRequest body)
    {
        var json = JsonSerializer.Serialize(body, AppJsonContext.Default.LibraryUpdateRequest);
        return await _client.PatchAsync(ApiEndpoints.Library(id), json, AppJsonContext.Default.Library, "admin permission");
    }

    public async Task<Library> DeleteAsync(string id)
    {
        return await _client.DeleteAsync(ApiEndpoints.Library(id), AppJsonContext.Default.Library, "admin permission");
    }

    public async Task<LibraryListResponse> ReorderAsync(string orderJson)
    {
        return await _client.PostAsync(ApiEndpoints.LibrariesOrder, orderJson, AppJsonContext.Default.LibraryListResponse, "admin permission");
    }
```

Verify the generic `PostAsync<T>`/`PatchAsync<T>`/`DeleteAsync<T>(endpoint, [jsonBody,] typeInfo, permissionHint?)` signatures against `AuthorsService`/`ItemsService` usage; adjust if the order differs.

- [ ] **Step 4: Build**

Run: `dotnet build src/AbsCli`
Expected: 0 errors.

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Api/ApiEndpoints.cs src/AbsCli/Services/LibrariesService.cs tests/AbsCli.Tests/Api/ApiEndpointsTests.cs
git commit -m "feat: add library create/update/delete/reorder service methods"
```

---

## Task 4: `libraries` subcommands + tests

**Files:** Modify `src/AbsCli/Commands/LibrariesCommand.cs`, `tests/AbsCli.Tests/Commands/LibrariesCommandTests.cs`.

- [ ] **Step 1: Update + add failing tests**

In `tests/AbsCli.Tests/Commands/LibrariesCommandTests.cs`, REPLACE the existing `Libraries_HasListGetScan` test's expected array with the full set, and add new tests:

```csharp
    [Fact]
    public void Libraries_HasAllSubcommands()
    {
        var verbs = LibrariesCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "list", "get", "scan", "create", "update", "delete", "reorder" }, verbs);
    }

    [Fact]
    public void LibrariesCreate_RequiresAdmin_AndHasFolderAndNameOptions()
    {
        var output = RenderHelp("libraries", "create").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  admin", output);
        Assert.Contains("--name", output);
        Assert.Contains("--folder", output);
        Assert.Contains("--media-type", output);
    }

    [Fact]
    public void LibrariesUpdate_RequiresAdmin()
    {
        var output = RenderHelp("libraries", "update").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  admin", output);
        Assert.Contains("--id", output);
        Assert.Contains("--display-order", output);
    }

    [Fact]
    public void LibrariesDelete_RequiresAdmin_AndWarnsCascade()
    {
        var output = RenderHelp("libraries", "delete").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  admin", output);
        Assert.Contains("cascade", output.ToLowerInvariant());
    }

    [Fact]
    public void LibrariesReorder_RequiresAdmin_AndHasInputStdin()
    {
        var output = RenderHelp("libraries", "reorder").Replace("\r\n", "\n");
        Assert.Contains("Permission required:\n  admin", output);
        Assert.Contains("--input", output);
        Assert.Contains("--stdin", output);
    }

    [Fact]
    public void BuildUpdateBody_OmitsUnsetIncludesSet()
    {
        var body = LibrariesCommand.BuildUpdateBodyForTesting("New", null, null, null, 2);
        Assert.Equal("New", body.Name);
        Assert.Null(body.MediaType);
        Assert.Equal(2, body.DisplayOrder);
    }
```

Delete the now-replaced `Libraries_HasListGetScan` test if it still exists (its assertion is superseded by `Libraries_HasAllSubcommands`).

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test tests/AbsCli.Tests --filter LibrariesCommandTests`
Expected: FAIL.

- [ ] **Step 3: Implement**

In `src/AbsCli/Commands/LibrariesCommand.cs`: add a logger field as the first line inside the class:

```csharp
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
```

Register the four subcommands in `Create()` after the `scan` registration:

```csharp
        command.Subcommands.Add(CreateCreateCommand());
        command.Subcommands.Add(CreateUpdateCommand());
        command.Subcommands.Add(CreateDeleteCommand());
        command.Subcommands.Add(CreateReorderCommand());
```

Add these methods (confirm `AddPermissionRequired`/`AddHelpSection`/`AddExamples`/`AddResponseExample<T>`/`CommandHelper.BuildClient`/`ConsoleOutput.WriteJson`/`CommandHelper.ReadJsonInput` against `CollectionsCommand.cs`/`ItemsCommand.cs`):

```csharp
    private static Command CreateCreateCommand()
    {
        var nameOption = new Option<string>("--name") { Description = "Library name", Required = true };
        var folderOption = new Option<string[]>("--folder") { Description = "Server-side folder path (repeatable; created if missing)", AllowMultipleArgumentsPerToken = true };
        var mediaTypeOption = new Option<string?>("--media-type") { Description = "book | podcast (default book)" };
        var providerOption = new Option<string?>("--provider") { Description = "Metadata provider (default google)" };
        var iconOption = new Option<string?>("--icon") { Description = "Library icon (default database)" };
        var command = new Command("create", "Create a new library")
        { nameOption, folderOption, mediaTypeOption, providerOption, iconOption };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Folder paths are SERVER-SIDE and are created on the server if missing.",
            "At least one --folder is required. Library settings are not",
            "configurable here.");
        command.AddExamples(
            "abs-cli libraries create --name \"Audiobooks\" --folder /audiobooks",
            "abs-cli libraries create --name \"Pods\" --folder /pods --media-type podcast");
        command.AddResponseExample<Library>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameOption)!;
            var folders = parseResult.GetValue(folderOption) ?? Array.Empty<string>();
            var mediaType = parseResult.GetValue(mediaTypeOption);
            var provider = parseResult.GetValue(providerOption);
            var icon = parseResult.GetValue(iconOption);
            if (folders.Length == 0)
            {
                _logger.Error("At least one --folder is required.");
                Environment.Exit(1);
                return 1;
            }
            var body = new LibraryCreateRequest
            {
                Name = name,
                Folders = folders.Select(f => new LibraryFolderRequest { FullPath = f }).ToList(),
                MediaType = string.IsNullOrEmpty(mediaType) ? null : mediaType,
                Provider = string.IsNullOrEmpty(provider) ? null : provider,
                Icon = string.IsNullOrEmpty(icon) ? null : icon
            };
            var (client, _) = CommandHelper.BuildClient();
            var service = new LibrariesService(client);
            var result = await service.CreateAsync(body);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Library);
            return 0;
        });
        return command;
    }

    private static Command CreateUpdateCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library ID", Required = true };
        var nameOption = new Option<string?>("--name") { Description = "New name" };
        var mediaTypeOption = new Option<string?>("--media-type") { Description = "New media type (book | podcast)" };
        var providerOption = new Option<string?>("--provider") { Description = "New metadata provider" };
        var iconOption = new Option<string?>("--icon") { Description = "New icon" };
        var displayOrderOption = new Option<int?>("--display-order") { Description = "New display order (number)" };
        var command = new Command("update", "Edit a library's name, media type, provider, icon, or display order")
        { idOption, nameOption, mediaTypeOption, providerOption, iconOption, displayOrderOption };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Folders are NOT editable here. Empty --name is rejected. At least",
            "one edit flag is required.");
        command.AddExamples(
            "abs-cli libraries update --id \"lib_1\" --name \"Renamed\"",
            "abs-cli libraries update --id \"lib_1\" --display-order 2");
        command.AddResponseExample<Library>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var name = parseResult.GetValue(nameOption);
            var mediaType = parseResult.GetValue(mediaTypeOption);
            var provider = parseResult.GetValue(providerOption);
            var icon = parseResult.GetValue(iconOption);
            var displayOrder = parseResult.GetValue(displayOrderOption);
            if (name is not null && string.IsNullOrEmpty(name))
            {
                _logger.Error("--name cannot be empty");
                Environment.Exit(1);
                return 1;
            }
            var body = BuildUpdateBodyForTesting(name, mediaType, provider, icon, displayOrder);
            if (body.Name == null && body.MediaType == null && body.Provider == null && body.Icon == null && body.DisplayOrder == null)
            {
                _logger.Error("Specify at least one of --name, --media-type, --provider, --icon, --display-order");
                Environment.Exit(1);
                return 1;
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new LibrariesService(client);
            var result = await service.UpdateAsync(id, body);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Library);
            return 0;
        });
        return command;
    }

    /// <summary>
    /// Build the PATCH body from the flags, coercing empty strings to null so
    /// they are omitted. Exposed internally for unit testing.
    /// </summary>
    internal static LibraryUpdateRequest BuildUpdateBodyForTesting(string? name, string? mediaType, string? provider, string? icon, int? displayOrder)
    {
        return new LibraryUpdateRequest
        {
            Name = string.IsNullOrEmpty(name) ? null : name,
            MediaType = string.IsNullOrEmpty(mediaType) ? null : mediaType,
            Provider = string.IsNullOrEmpty(provider) ? null : provider,
            Icon = string.IsNullOrEmpty(icon) ? null : icon,
            DisplayOrder = displayOrder
        };
    }

    private static Command CreateDeleteCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library ID", Required = true };
        var command = new Command("delete", "Delete a library and ALL its contents") { idOption };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "DESTRUCTIVE CASCADE: permanently deletes the library AND every item",
            "in it, all collections for the library, and removes it from playlists",
            "and playback sessions. No confirmation prompt. Returns the deleted",
            "library.");
        command.AddExamples(
            "abs-cli libraries delete --id \"lib_1\"");
        command.AddResponseExample<Library>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new LibrariesService(client);
            var result = await service.DeleteAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.Library);
            return 0;
        });
        return command;
    }

    private static Command CreateReorderCommand()
    {
        var inputOption = new Option<string?>("--input") { Description = "JSON file with an array of {id, newOrder}" };
        var stdinOption = new Option<bool>("--stdin") { Description = "Read the reorder JSON array from stdin" };
        var command = new Command("reorder", "Reorder libraries by display order")
        { inputOption, stdinOption };
        command.AddPermissionRequired("admin");
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "Body is a JSON array of objects: [{\"id\":\"lib_1\",\"newOrder\":1}, ...].");
        command.AddExamples(
            "abs-cli libraries reorder --input order.json",
            "echo '[{\"id\":\"lib_1\",\"newOrder\":1}]' | abs-cli libraries reorder --stdin");
        command.AddResponseExample<LibraryListResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var input = parseResult.GetValue(inputOption);
            var stdin = parseResult.GetValue(stdinOption);
            if (stdin && input != null)
            {
                _logger.Error("Provide --input or --stdin, not both.");
                Environment.Exit(1);
                return 1;
            }
            string orderJson;
            if (stdin) orderJson = await Console.In.ReadToEndAsync(cancellationToken);
            else if (input != null) orderJson = CommandHelper.ReadJsonInput(input);
            else
            {
                _logger.Error("Provide --input <file> or --stdin.");
                Environment.Exit(1);
                return 1;
            }
            var (client, _) = CommandHelper.BuildClient();
            var service = new LibrariesService(client);
            var result = await service.ReorderAsync(orderJson);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.LibraryListResponse);
            return 0;
        });
        return command;
    }
```

Verify `LibrariesCommand.cs` has `using System.CommandLine; using AbsCli.Models; using AbsCli.Output; using AbsCli.Services;` (add any missing). `Library`/`LibraryListResponse`/`LibraryCreateRequest`/`LibraryUpdateRequest`/`LibraryFolderRequest` are all in `AbsCli.Models`.

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test tests/AbsCli.Tests --filter LibrariesCommandTests`
Expected: PASS.

- [ ] **Step 5: Confirm wiring**

Run: `dotnet run --project src/AbsCli -- libraries --help`
Expected: lists create/update/delete/reorder.

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/LibrariesCommand.cs tests/AbsCli.Tests/Commands/LibrariesCommandTests.cs
git status --short   # if ResponseExamples.g.cs shows modified, revert: git checkout src/AbsCli/Commands/ResponseExamples.g.cs
git commit -m "feat: add libraries create, update, delete, reorder commands"
```

---

## Task 5: Docs — README + coverage map

**Files:** Modify `README.md`, `docs/abs-api-coverage.md`.

- [ ] **Step 1: README Commands table**

Add four rows near the other `libraries …` rows (after `libraries scan`):

```markdown
| `libraries create --name <n> --folder <path>... [--media-type] [--provider] [--icon]` | Create a library (admin; folders are server-side, created if missing) |
| `libraries update --id <id> [--name] [--media-type] [--provider] [--icon] [--display-order]` | Edit library fields (admin; folders not editable) |
| `libraries delete --id <id>` | Delete a library and ALL its contents (admin; destructive cascade) |
| `libraries reorder {--input <file> \| --stdin}` | Reorder libraries by display order (admin) |
```

- [ ] **Step 2: Coverage doc**

In `docs/abs-api-coverage.md`, find the four rows and update last column to the mapped command with ✅, and set the permission column to `admin` where it is blank or `?`:
- `POST /api/libraries` → `admin`, `` `libraries create` ✅ ``
- `PATCH /api/libraries/:id` → `admin`, `` `libraries update` ✅ ``
- `DELETE /api/libraries/:id` → `admin`, `` `libraries delete` ✅ ``
- `POST /api/libraries/order` → **fix permission `?` → `admin`**, `` `libraries reorder` ✅ ``

Run to confirm:
```bash
rg -n "POST \| .api/libraries|PATCH \| .api/libraries/:id|DELETE \| .api/libraries/:id|libraries/order" docs/abs-api-coverage.md
rg -n "libraries create|libraries reorder" README.md
```

- [ ] **Step 3: Commit**

```bash
git add README.md docs/abs-api-coverage.md
git commit -m "docs: document library CRUD commands and fix reorder permission"
```

---

## Task 6: Smoke test

**Files:** Modify `docker/smoke-test.sh`. No `seed.sh` change (the section creates + deletes its own throwaway library).

- [ ] **Step 1: Help-example enumeration**

Add to the leaf-command loop (backslash-continued):
```bash
           "libraries create" "libraries update" "libraries delete" "libraries reorder" \
```

- [ ] **Step 2: Add a "Library CRUD" section**

Place it in the main root-authenticated body (root is admin), AFTER the existing "Libraries" section. It creates a throwaway library, updates it, reorders it, then deletes it (self-cleaning so the stack returns to 1 library):

```bash
# ============================================================
echo ""
echo "=== Library CRUD (admin) ==="
# ============================================================

# create a throwaway library (server creates /tmp/smoke-crud-lib)
output=$($CLI libraries create --name "Smoke CRUD Lib" --folder /tmp/smoke-crud-lib --media-type book 2>&1)
assert_json_key "libraries create returns id" "id" "$output"
NEW_LIB_ID=$(json_get "$output" "['id']")
assert_json_expr "libraries create set name" "d['name']=='Smoke CRUD Lib'" "$output"

# update its name
output=$($CLI libraries update --id "$NEW_LIB_ID" --name "Smoke CRUD Renamed" 2>&1)
assert_json_expr "libraries update changed name" "d['name']=='Smoke CRUD Renamed'" "$output"

# reorder (send the new lib to a high display order)
output=$(echo "[{\"id\":\"$NEW_LIB_ID\",\"newOrder\":99}]" | $CLI libraries reorder --stdin 2>&1)
assert_json_key "libraries reorder returns libraries" "libraries" "$output"

# delete it (returns the deleted library)
output=$($CLI libraries delete --id "$NEW_LIB_ID" 2>&1)
assert_json_expr "libraries delete returns deleted id" "d['id']=='$NEW_LIB_ID'" "$output"

# confirm it's gone from the list
output=$($CLI libraries list 2>&1)
assert_json_expr "libraries list no longer has the deleted lib" "'$NEW_LIB_ID' not in [l['id'] for l in d['libraries']]" "$output"
```

- [ ] **Step 3: 403 assertions**

Add to the `testuser` (non-admin) admin-denial group:
```bash
error_output=$($CLI libraries create --name "Nope" --folder /tmp/nope 2>&1 || true)
if echo "$error_output" | grep -qi "permission denied\|admin"; then
    pass "libraries create as testuser shows admin permission denied"
else
    fail "libraries create as testuser shows admin permission denied" "got: ${error_output:0:200}"
fi
```
(If the section runs after root context switched away, ensure the create/update/delete happy-path block ran earlier under root; the 403 block does its own `abs_login testuser testpass` and the section restores `abs_login root root` afterward — match the existing pattern.)

- [ ] **Step 4: Run the smoke test**

```bash
cd docker && docker compose up -d
IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
ABS_URL=http://$IP:80 bash docker/seed.sh
ABS_URL=http://$IP:80 bash docker/smoke-test.sh
```
Expected: all pass, exit 0. If a field name or the reorder response differs, inspect a live response and adjust. Only mark "smoke passed" after seeing it.

- [ ] **Step 5: Commit**

```bash
git add docker/smoke-test.sh
git commit -m "test: add library CRUD smoke assertions and 403 coverage"
```

---

## Task 7: Full verification

- [ ] **Step 1: Full unit run** — `dotnet test AbsCli.sln` → all pass (incl. `ResponseExamplesDriftTest`).
- [ ] **Step 2: Format check** — `dotnet format AbsCli.sln --verify-no-changes` → clean (else format + commit `chore: fix formatting`).
- [ ] **Step 3: Wiring** — `dotnet run --project src/AbsCli -- libraries --help` shows create/update/delete/reorder.
- [ ] **Step 4: Smoke gate** — confirm Task 6's smoke passed. Gates the PR checkbox.

---

## Self-Review Notes (author checklist — completed during planning)

- **Spec coverage:** create (flags + repeatable --folder, ≥1 required, settings dropped), update (scalar flags, empty-name rejected, at-least-one required, folders-not-editable help), delete (admin, cascade warning, no prompt, returns deleted Library), reorder (--input/--stdin raw array), coverage-doc fixes incl. order `?`→admin, smoke incl. self-cleaning create→update→reorder→delete + 403.
- **Reuse:** responses reuse existing `Library`/`LibraryListResponse` (no new response models). New request types registered + generator-excluded (Task 2), like the tag/genre/narrator request types.
- **Delete returns the deleted library** (verified: `res.json(libraryJson)`), so the command prints the `Library` — not `{success:true}`.
- **Existing test update:** Task 4 replaces the `Libraries_HasListGetScan` assertion with the full subcommand set (adding 4 verbs changes it) — called out to avoid a surprise red.
- **Permission hint** `"admin permission"` (per CLAUDE.md), consistent with the recent tags/genres/narrators work; note the pre-existing `ScanAsync` uses the older `"'admin' access"` string — left untouched.
- **Type consistency:** `LibraryCreateRequest`/`LibraryUpdateRequest`/`LibraryFolderRequest` fields match service serialization + tests; `BuildUpdateBodyForTesting` signature matches its test and call site.
- **CHANGELOG untouched.**
