# `items cover` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add three new verbs under `abs-cli items cover` (`set`, `get`, `remove`) wrapping the four ABS cover endpoints we don't currently expose, so an agent can compose detection + provider search + cover application without leaving the CLI.

**Architecture:** New `CoversService` wraps the four endpoints. New `CreateCoverCommand()` factory in `ItemsCommand.cs` adds the `items cover` subtree. `set` is one verb with mutually-exclusive `--url` / `--file` / `--server-path` flags routing to `POST {url}`, multipart `POST <file>`, or `PATCH {cover}` respectively. `get` writes to a file (`--output <path>`) or streams binary to stdout (`--output -`); `remove` deletes and synthesises `{"success": true}` so the JSON-everywhere convention holds. New models registered in `AppJsonContext` and round-tripped in `self-test`.

**Tech Stack:** C# / .NET 10 / `System.CommandLine` 2.0.7 / `System.Text.Json` source-gen / `MultipartFormDataContent` (AOT-safe). No new packages.

**Spec:** [docs/specs/2026-04-28-items-cover-handling.md](../specs/2026-04-28-items-cover-handling.md)

---

## File Structure

**New files:**

- `src/AbsCli/Models/CoverModels.cs` — four types: `CoverApplyResponse`, `CoverApplyByUrlRequest`, `CoverLinkExistingRequest`, `CoverFileSavedDescriptor`.
- `src/AbsCli/Services/CoversService.cs` — five methods: `SetByUrlAsync`, `UploadFromFileAsync`, `LinkExistingAsync`, `GetStreamAsync`, `RemoveAsync`.
- `tests/AbsCli.Tests/Services/CoversServiceTests.cs` — JSON round-trip tests for the four model types.
- `tests/AbsCli.Tests/Commands/ItemsCoverCommandTests.cs` — mutex validation on `set`; `--output -` vs `--output <file>` routing on `get`.

**Modified files:**

- `src/AbsCli/Api/ApiEndpoints.cs` — add `ItemCover(string id)` helper.
- `src/AbsCli/Api/AbsApiClient.cs` — add two helper methods: `PostMultipartAsync<T>` (returns deserialised JSON) and `GetStreamAsync` (returns the response stream for the caller to consume).
- `src/AbsCli/Models/JsonContext.cs` — register the four new types via `[JsonSerializable]`.
- `src/AbsCli/Commands/ItemsCommand.cs` — add `CreateCoverCommand()` factory and register it as a subcommand.
- `src/AbsCli/Commands/SelfTestCommand.cs` — round-trip checks for the four new models.
- `docker/smoke-test.sh` — new `=== Cover Commands ===` section exercising the full lifecycle.
- `docs/roadmap.md` — fix the misleading "Investigate cover handling" wording; replace with the actual deliverable.

**Files explicitly NOT modified:**

- `CHANGELOG.md` — owned by release workflow.
- `metadata covers` command — pre-existing search command, unrelated to cover application.
- `BookMedia` / `BookMediaMinified` models — `CoverPath` is already there.

---

## Task 0: Branch + spec/plan commit

**Files:** none new.

- [ ] **Step 1: Confirm clean tree on main**

```bash
cd /workspaces/abs-cli
git checkout main
git pull
git status
```

Expected: `On branch main`, working tree clean (gitignored `.mempalace/` is fine).

- [ ] **Step 2: Create the branch**

```bash
git checkout -b feat/items-cover-handling
```

- [ ] **Step 3: Commit the spec and plan**

```bash
git add docs/specs/2026-04-28-items-cover-handling.md docs/plans/2026-04-28-items-cover-handling.md
git commit -m "docs: add spec and plan for items cover handling"
```

---

## Task 1: Cover models + JsonContext registration

**Files:**
- Create: `src/AbsCli/Models/CoverModels.cs`
- Modify: `src/AbsCli/Models/JsonContext.cs`

- [ ] **Step 1: Create `src/AbsCli/Models/CoverModels.cs`**

```csharp
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Response from POST /api/items/:id/cover (URL or multipart) and
/// PATCH /api/items/:id/cover (existing server-side path).
/// Server returns { success: true, cover: "<server-path>" }.
/// </summary>
public class CoverApplyResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("cover")]
    public string Cover { get; set; } = "";
}

/// <summary>
/// Body for POST /api/items/:id/cover when applying from a URL.
/// ABS server downloads the URL on receipt.
/// </summary>
public class CoverApplyByUrlRequest
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}

/// <summary>
/// Body for PATCH /api/items/:id/cover when pointing to a file already on
/// the ABS server's filesystem. Path must not start with http:/https:.
/// </summary>
public class CoverLinkExistingRequest
{
    [JsonPropertyName("cover")]
    public string Cover { get; set; } = "";
}

/// <summary>
/// Stdout descriptor written by `items cover get --output &lt;file&gt;` after
/// the bytes are saved. Not present when --output is "-" (binary-to-stdout
/// mode emits nothing else on stdout).
/// </summary>
public class CoverFileSavedDescriptor
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }
}
```

- [ ] **Step 2: Register in `src/AbsCli/Models/JsonContext.cs`**

Add four `[JsonSerializable]` attributes alongside the existing block (between the existing `[JsonSerializable(typeof(CoverSearchResponse))]` line and the `[JsonSerializable(typeof(UploadManifestEntry))]` line, in alphabetical-ish order):

```csharp
[JsonSerializable(typeof(CoverApplyResponse))]
[JsonSerializable(typeof(CoverApplyByUrlRequest))]
[JsonSerializable(typeof(CoverLinkExistingRequest))]
[JsonSerializable(typeof(CoverFileSavedDescriptor))]
```

- [ ] **Step 3: Build and verify**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
```

Expected: 0 warnings, 0 errors. Source generator picks up the new types.

- [ ] **Step 4: Commit**

```bash
git add src/AbsCli/Models/CoverModels.cs src/AbsCli/Models/JsonContext.cs
git commit -m "feat: add cover request/response models"
```

---

## Task 2: Self-test round-trip checks for cover models

Catch any AOT serialization regression in the published binary.

**Files:**
- Modify: `src/AbsCli/Commands/SelfTestCommand.cs`

- [ ] **Step 1: Add four `Check` blocks**

In `src/AbsCli/Commands/SelfTestCommand.cs`, locate the last existing model-roundtrip `Check(...)` block. Add a new section header and four checks immediately after it (before the embedded-resources section header from the changelog work):

```csharp
            Console.Error.WriteLine();
            Console.Error.WriteLine("=== Cover Models ===");

            Check("CoverApplyResponse round-trip", () =>
            {
                var obj = new CoverApplyResponse { Success = true, Cover = "/srv/abs/covers/foo.jpg" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverApplyResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverApplyResponse)!;
                Assert(back.Success == true, $"success: {back.Success}");
                Assert(back.Cover == "/srv/abs/covers/foo.jpg", $"cover: {back.Cover}");
            });

            Check("CoverApplyByUrlRequest round-trip", () =>
            {
                var obj = new CoverApplyByUrlRequest { Url = "https://example.com/cover.jpg" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverApplyByUrlRequest);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverApplyByUrlRequest)!;
                Assert(back.Url == "https://example.com/cover.jpg", $"url: {back.Url}");
            });

            Check("CoverLinkExistingRequest round-trip", () =>
            {
                var obj = new CoverLinkExistingRequest { Cover = "/srv/abs/library/Author/Title/cover.jpg" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverLinkExistingRequest);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverLinkExistingRequest)!;
                Assert(back.Cover == "/srv/abs/library/Author/Title/cover.jpg", $"cover: {back.Cover}");
            });

            Check("CoverFileSavedDescriptor round-trip", () =>
            {
                var obj = new CoverFileSavedDescriptor { Path = "/tmp/cover.jpg", Bytes = 12345 };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverFileSavedDescriptor);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverFileSavedDescriptor)!;
                Assert(back.Path == "/tmp/cover.jpg", $"path: {back.Path}");
                Assert(back.Bytes == 12345, $"bytes: {back.Bytes}");
            });
```

- [ ] **Step 2: Run self-test**

```bash
dotnet run --project /workspaces/abs-cli/src/AbsCli/AbsCli.csproj -- self-test
```

Expected: stderr ends with `Results: 38 passed, 0 failed` (was 34, added 4). Exit 0.

- [ ] **Step 3: Run full test suite**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln
```

Expected: 103 / 103 pass.

- [ ] **Step 4: Format check**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: exit 0.

- [ ] **Step 5: Commit**

```bash
git add src/AbsCli/Commands/SelfTestCommand.cs
git commit -m "test: round-trip cover models in self-test"
```

---

## Task 3: API endpoint helpers and HTTP client extensions

Add the cover endpoint helper plus two `AbsApiClient` methods needed by `CoversService`: typed multipart POST and raw stream GET.

**Files:**
- Modify: `src/AbsCli/Api/ApiEndpoints.cs`
- Modify: `src/AbsCli/Api/AbsApiClient.cs`

- [ ] **Step 1: Add cover endpoint helper to `ApiEndpoints.cs`**

In `src/AbsCli/Api/ApiEndpoints.cs`, between the existing `Item(...)` and `ItemMedia(...)` helpers (or wherever fits the local pattern), add:

```csharp
public static string ItemCover(string id) => $"/api/items/{id}/cover";
```

- [ ] **Step 2: Add typed multipart POST overload to `AbsApiClient.cs`**

The existing `PostMultipartAsync` returns void. Add a generic overload that returns the deserialised JSON. Place it directly below the existing `PostMultipartAsync` method (around line 131):

```csharp
    public async Task<T> PostMultipartAsync<T>(string endpoint, MultipartFormDataContent content,
        JsonTypeInfo<T> typeInfo, string? permissionHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.PostAsync(endpoint, content, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Post, endpoint, permissionHint);
        var json = await response.Content.ReadAsStringAsync(cts.Token);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }
```

- [ ] **Step 3: Add `GetStreamAsync` to `AbsApiClient.cs`**

Below the existing `DownloadFileAsync` method (around line 142). The caller is responsible for disposing the returned stream.

```csharp
    /// <summary>
    /// GET that returns the response body as a Stream the caller can consume
    /// (e.g. copy to a FileStream or to Console.OpenStandardOutput()). The
    /// caller MUST dispose the returned stream.
    /// </summary>
    public async Task<Stream> GetStreamAsync(string endpoint, string? permissionHint = null, TimeSpan? timeout = null)
    {
        await EnsureValidTokenAsync();
        using var cts = new CancellationTokenSource(timeout ?? DefaultRequestTimeout);
        var response = await _http.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Get, endpoint, permissionHint);
        return await response.Content.ReadAsStreamAsync(cts.Token);
    }
```

- [ ] **Step 4: Build and verify**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 5: Run full test suite**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln
```

Expected: 103 / 103 pass (no new tests yet — purely additive).

- [ ] **Step 6: Format check**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: exit 0.

- [ ] **Step 7: Commit**

```bash
git add src/AbsCli/Api/ApiEndpoints.cs src/AbsCli/Api/AbsApiClient.cs
git commit -m "feat: add cover endpoint helper and typed multipart/stream HTTP methods"
```

---

## Task 4: `CoversService`

**Files:**
- Create: `src/AbsCli/Services/CoversService.cs`

- [ ] **Step 1: Create `src/AbsCli/Services/CoversService.cs`**

```csharp
using System.Text.Json;
using AbsCli.Api;
using AbsCli.Models;

namespace AbsCli.Services;

public class CoversService
{
    private readonly AbsApiClient _client;

    public CoversService(AbsApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Apply a cover by URL. ABS server downloads from the URL.
    /// </summary>
    public async Task<CoverApplyResponse> SetByUrlAsync(string itemId, string url)
    {
        var body = JsonSerializer.Serialize(
            new CoverApplyByUrlRequest { Url = url },
            AppJsonContext.Default.CoverApplyByUrlRequest);
        return await _client.PostAsync(ApiEndpoints.ItemCover(itemId), body,
            AppJsonContext.Default.CoverApplyResponse, "'upload' permission");
    }

    /// <summary>
    /// Apply a cover by uploading a local file (multipart).
    /// </summary>
    public async Task<CoverApplyResponse> UploadFromFileAsync(string itemId, string localFilePath)
    {
        var fileBytes = await File.ReadAllBytesAsync(localFilePath);
        var fileContent = new ByteArrayContent(fileBytes);
        var content = new MultipartFormDataContent();
        content.Add(fileContent, "cover", Path.GetFileName(localFilePath));
        return await _client.PostMultipartAsync(ApiEndpoints.ItemCover(itemId), content,
            AppJsonContext.Default.CoverApplyResponse, "'upload' permission");
    }

    /// <summary>
    /// Apply a cover by pointing to an existing file on the ABS server's
    /// filesystem. The server validates the path exists and is a real file.
    /// </summary>
    public async Task<CoverApplyResponse> LinkExistingAsync(string itemId, string serverPath)
    {
        var body = JsonSerializer.Serialize(
            new CoverLinkExistingRequest { Cover = serverPath },
            AppJsonContext.Default.CoverLinkExistingRequest);
        return await _client.PatchAsync(ApiEndpoints.ItemCover(itemId), body,
            AppJsonContext.Default.CoverApplyResponse);
    }

    /// <summary>
    /// Fetch the cover bytes. Caller must dispose the returned stream.
    /// </summary>
    public async Task<Stream> GetStreamAsync(string itemId, bool raw)
    {
        var endpoint = ApiEndpoints.ItemCover(itemId);
        if (raw) endpoint += "?raw=1";
        return await _client.GetStreamAsync(endpoint);
    }

    /// <summary>
    /// Remove the cover from an item. Server returns 200 with empty body.
    /// </summary>
    public async Task RemoveAsync(string itemId)
    {
        await _client.DeleteAsync(ApiEndpoints.ItemCover(itemId));
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 3: Format check**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: exit 0.

- [ ] **Step 4: Commit**

```bash
git add src/AbsCli/Services/CoversService.cs
git commit -m "feat: add CoversService"
```

---

## Task 5: `items cover` command tree

The substantive command-wiring task. Adds `set`, `get`, `remove` subcommands under `items cover`, with mutex validation on `set` and `--output`/`--raw` handling on `get`.

**Files:**
- Modify: `src/AbsCli/Commands/ItemsCommand.cs`

- [ ] **Step 1: Add `using` for the service and models**

At the top of `ItemsCommand.cs`, ensure these usings are present (some may already be):

```csharp
using AbsCli.Models;
using AbsCli.Services;
```

- [ ] **Step 2: Register `CreateCoverCommand()` in the `Create()` method**

In `ItemsCommand.Create()`, add the cover subcommand registration after the existing scan registration. Currently:

```csharp
        command.Subcommands.Add(CreateScanCommand());
```

Change to:

```csharp
        command.Subcommands.Add(CreateScanCommand());
        command.Subcommands.Add(CreateCoverCommand());
```

- [ ] **Step 3: Append `CreateCoverCommand()` and helpers at the bottom of the class**

Add the following private methods to `ItemsCommand` (after `CreateScanCommand`):

```csharp
    private static Command CreateCoverCommand()
    {
        var command = new Command("cover", "Manage book covers (apply, fetch, remove)");
        command.Subcommands.Add(CreateCoverSetCommand());
        command.Subcommands.Add(CreateCoverGetCommand());
        command.Subcommands.Add(CreateCoverRemoveCommand());
        return command;
    }

    private static Command CreateCoverSetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var urlOption = new Option<string?>("--url") { Description = "Cover image URL — ABS server downloads it" };
        var fileOption = new Option<string?>("--file") { Description = "Local cover image file to upload" };
        var serverPathOption = new Option<string?>("--server-path") { Description = "Path to a file already on the ABS server's filesystem" };
        var command = new Command("set", "Apply a cover to a book by URL, local file, or existing server-side path") { idOption, urlOption, fileOption, serverPathOption };
        command.AddExamples(
            "abs-cli items cover set --id \"li_abc123\" --url \"https://example.com/cover.jpg\"",
            "abs-cli items cover set --id \"li_abc123\" --file ./cover.jpg",
            "abs-cli items cover set --id \"li_abc123\" --server-path /srv/abs/library/foo/cover.jpg");
        command.AddResponseExample<CoverApplyResponse>();

        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var url = parseResult.GetValue(urlOption);
            var file = parseResult.GetValue(fileOption);
            var serverPath = parseResult.GetValue(serverPathOption);

            var sources = new[] { url, file, serverPath }.Count(s => !string.IsNullOrEmpty(s));
            if (sources != 1)
            {
                ConsoleOutput.WriteError("Specify exactly one of --url, --file, --server-path");
                Environment.Exit(1);
            }

            var (client, _) = CommandHelper.BuildClient();
            var service = new CoversService(client);
            CoverApplyResponse result;
            if (!string.IsNullOrEmpty(url))
            {
                result = await service.SetByUrlAsync(id, url);
            }
            else if (!string.IsNullOrEmpty(file))
            {
                if (!File.Exists(file))
                {
                    ConsoleOutput.WriteError($"File not found: {file}");
                    Environment.Exit(1);
                }
                result = await service.UploadFromFileAsync(id, file);
            }
            else
            {
                result = await service.LinkExistingAsync(id, serverPath!);
            }
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.CoverApplyResponse);
        });
        return command;
    }

    private static Command CreateCoverGetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var outputOption = new Option<string>("--output") { Description = "Output file path, or '-' for binary to stdout", Required = true };
        var rawOption = new Option<bool>("--raw") { Description = "Fetch the original unprocessed image (default: ABS-resized)" };
        var command = new Command("get", "Download the cover image for a book") { idOption, outputOption, rawOption };
        command.AddExamples(
            "abs-cli items cover get --id \"li_abc123\" --output cover.jpg",
            "abs-cli items cover get --id \"li_abc123\" --output cover.jpg --raw",
            "abs-cli items cover get --id \"li_abc123\" --output - > cover.jpg");
        command.AddResponseExample<CoverFileSavedDescriptor>();

        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var output = parseResult.GetValue(outputOption)!;
            var raw = parseResult.GetValue(rawOption);

            var (client, _) = CommandHelper.BuildClient();
            var service = new CoversService(client);
            await using var stream = await service.GetStreamAsync(id, raw);

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

    private static Command CreateCoverRemoveCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Library item ID", Required = true };
        var command = new Command("remove", "Remove the cover from a book") { idOption };
        command.AddExamples(
            "abs-cli items cover remove --id \"li_abc123\"");

        command.SetAction(async parseResult =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new CoversService(client);
            await service.RemoveAsync(id);
            // Server returns empty 200; synthesise a JSON envelope for consistency.
            ConsoleOutput.WriteJson(
                new Dictionary<string, string> { ["success"] = "true" });
        });
        return command;
    }
```

- [ ] **Step 4: Build**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 5: Spot-check `--help`**

```bash
dotnet run --project /workspaces/abs-cli/src/AbsCli/AbsCli.csproj -- items cover --help
dotnet run --project /workspaces/abs-cli/src/AbsCli/AbsCli.csproj -- items cover set --help
dotnet run --project /workspaces/abs-cli/src/AbsCli/AbsCli.csproj -- items cover get --help
dotnet run --project /workspaces/abs-cli/src/AbsCli/AbsCli.csproj -- items cover remove --help
```

Expected: each prints the expected description, options, examples, and response shape (where applicable).

- [ ] **Step 6: Format check**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: exit 0.

- [ ] **Step 7: Commit**

```bash
git add src/AbsCli/Commands/ItemsCommand.cs
git commit -m "feat: add items cover command tree (set, get, remove)"
```

---

## Task 6: Unit tests for `items cover`

Cover the mutex validation on `set` and the model round-trips.

**Files:**
- Create: `tests/AbsCli.Tests/Services/CoversServiceTests.cs`
- Create: `tests/AbsCli.Tests/Commands/ItemsCoverCommandTests.cs`

- [ ] **Step 1: Create `tests/AbsCli.Tests/Services/CoversServiceTests.cs`**

These complement the self-test round-trips with xUnit-runnable equivalents (so a future package bump can't silently break source-gen on the test project too).

```csharp
using System.Text.Json;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Services;

public class CoversServiceTests
{
    [Fact]
    public void CoverApplyResponse_RoundTrip()
    {
        var obj = new CoverApplyResponse { Success = true, Cover = "/srv/abs/covers/foo.jpg" };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverApplyResponse);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverApplyResponse)!;
        Assert.True(back.Success);
        Assert.Equal("/srv/abs/covers/foo.jpg", back.Cover);
    }

    [Fact]
    public void CoverApplyByUrlRequest_RoundTrip()
    {
        var obj = new CoverApplyByUrlRequest { Url = "https://example.com/cover.jpg" };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverApplyByUrlRequest);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverApplyByUrlRequest)!;
        Assert.Equal("https://example.com/cover.jpg", back.Url);
    }

    [Fact]
    public void CoverLinkExistingRequest_RoundTrip()
    {
        var obj = new CoverLinkExistingRequest { Cover = "/srv/abs/library/Author/Title/cover.jpg" };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverLinkExistingRequest);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverLinkExistingRequest)!;
        Assert.Equal("/srv/abs/library/Author/Title/cover.jpg", back.Cover);
    }

    [Fact]
    public void CoverFileSavedDescriptor_RoundTrip()
    {
        var obj = new CoverFileSavedDescriptor { Path = "/tmp/cover.jpg", Bytes = 12345 };
        var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.CoverFileSavedDescriptor);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.CoverFileSavedDescriptor)!;
        Assert.Equal("/tmp/cover.jpg", back.Path);
        Assert.Equal(12345, back.Bytes);
    }
}
```

- [ ] **Step 2: Create `tests/AbsCli.Tests/Commands/ItemsCoverCommandTests.cs`**

These are help-rendering / parser tests — they verify the command tree shape without making HTTP calls. They mirror the style of `HelpOutputTests.cs` (which uses `RootCommand` + `Parse(...).Invoke(config)` against an in-memory `StringWriter`).

```csharp
using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ItemsCoverCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(ItemsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Cover_TopLevel_Help_ListsThreeVerbs()
    {
        var output = RenderHelp("items", "cover");
        Assert.Contains("set", output);
        Assert.Contains("get", output);
        Assert.Contains("remove", output);
    }

    [Fact]
    public void CoverSet_Help_ListsAllThreeSourceFlags()
    {
        var output = RenderHelp("items", "cover", "set");
        Assert.Contains("--url", output);
        Assert.Contains("--file", output);
        Assert.Contains("--server-path", output);
    }

    [Fact]
    public void CoverSet_Help_ShowsResponseShape()
    {
        var output = RenderHelp("items", "cover", "set");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"success\"", output);
        Assert.Contains("\"cover\"", output);
    }

    [Fact]
    public void CoverGet_Help_DocumentsOutputAndRaw()
    {
        var output = RenderHelp("items", "cover", "get");
        Assert.Contains("--output", output);
        Assert.Contains("--raw", output);
    }

    [Fact]
    public void CoverGet_Help_ShowsResponseShape()
    {
        var output = RenderHelp("items", "cover", "get");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"path\"", output);
        Assert.Contains("\"bytes\"", output);
    }

    [Fact]
    public void CoverRemove_Help_RequiresIdOnly()
    {
        var output = RenderHelp("items", "cover", "remove");
        Assert.Contains("--id", output);
        Assert.DoesNotContain("--url", output);
        Assert.DoesNotContain("--file", output);
    }
}
```

(Mutex validation on `set` is enforced inside the action handler via `Environment.Exit(1)`, which is hard to assert directly in xUnit without spawning a process. The smoke test in Task 8 covers the behavioural assertion; here we cover the help-rendering shape only.)

- [ ] **Step 3: Run tests**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln
```

Expected: 113 / 113 (was 103, added 4 service + 6 command tests). All pass.

- [ ] **Step 4: Format check**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: exit 0.

- [ ] **Step 5: Commit**

```bash
git add tests/AbsCli.Tests/Services/CoversServiceTests.cs tests/AbsCli.Tests/Commands/ItemsCoverCommandTests.cs
git commit -m "test: cover models round-trip and items cover command help"
```

---

## Task 7: AOT publish + offline self-test verification

Verifies AOT works under the new types. No HTTP dependency.

**Files:** none.

- [ ] **Step 1: AOT publish**

```bash
dotnet publish /workspaces/abs-cli/src/AbsCli/AbsCli.csproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true
```

Expected: completes with no errors and no new trim warnings.

- [ ] **Step 2: Run self-test on the AOT binary**

```bash
PUB=/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli
"$PUB" self-test 2>&1 | tail -4
```

Expected: `Results: 38 passed, 0 failed`.

- [ ] **Step 3: Spot-check help rendering on the AOT binary**

```bash
"$PUB" items cover --help | head -10
"$PUB" items cover set --help | head -25
```

Expected: same shape as the dev help-render — three subcommands listed; `set --help` shows `--url`, `--file`, `--server-path`, examples block, response-shape block.

This task produces no commit.

---

## Task 8: Smoke test — live ABS lifecycle

Exercises the full cover lifecycle against a real ABS instance.

**Files:**
- Modify: `docker/smoke-test.sh`

- [ ] **Step 1: Add a cover-test fixture image**

The smoke test needs a small image file to upload. Generate it inline at the start of the cover section so we don't ship binary fixtures in git. Below is the snippet to insert.

- [ ] **Step 2: Append a `=== Cover Commands ===` section to `docker/smoke-test.sh`**

Locate the final `=== Metadata Commands ===` section in `docker/smoke-test.sh`. Append the following block immediately after it (and before the trailing `Results:` summary block). This block depends on `$FIRST_ITEM_ID` (already set earlier in the smoke suite) and on `$CLI` / `$ABS_URL` / `$ABS_TOKEN`.

```bash
echo ""
echo "=== Cover Commands ==="

# Generate a tiny valid PNG (1x1 transparent pixel) as a fixture.
COVER_TMP=$(mktemp -d)
COVER_FILE="$COVER_TMP/cover.png"
python3 -c "
import struct, zlib
def chunk(t, d):
    return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t+d) & 0xffffffff)
sig = b'\x89PNG\r\n\x1a\n'
ihdr = chunk(b'IHDR', struct.pack('>IIBBBBB', 1, 1, 8, 6, 0, 0, 0))
idat_raw = b'\x00\x00\x00\x00\x00'  # filter byte + RGBA
idat = chunk(b'IDAT', zlib.compress(idat_raw))
iend = chunk(b'IEND', b'')
with open('$COVER_FILE', 'wb') as f:
    f.write(sig + ihdr + idat + iend)
"

# 1. Apply cover from local file via multipart upload
output=$($CLI items cover set --id "$FIRST_ITEM_ID" --file "$COVER_FILE" 2>/dev/null)
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['success']==True and d['cover']" 2>/dev/null; then
    pass "items cover set --file applied cover"
else
    fail "items cover set --file applied cover" "unexpected response"
    echo "    response: ${output:0:200}"
fi

# 2. Verify item now reports a non-null coverPath
output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['media'].get('coverPath')" 2>/dev/null; then
    pass "items get reports non-null coverPath after set"
else
    fail "items get reports non-null coverPath after set" "coverPath missing"
fi

# 3. Download cover to file
DOWNLOAD_FILE="$COVER_TMP/downloaded.bin"
output=$($CLI items cover get --id "$FIRST_ITEM_ID" --output "$DOWNLOAD_FILE" 2>/dev/null)
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['path']=='$DOWNLOAD_FILE' and d['bytes']>0" 2>/dev/null; then
    pass "items cover get --output writes file and reports descriptor"
else
    fail "items cover get --output writes file and reports descriptor" "unexpected descriptor"
    echo "    response: ${output:0:200}"
fi
if [ -s "$DOWNLOAD_FILE" ]; then
    pass "downloaded cover file is non-empty"
else
    fail "downloaded cover file is non-empty" "file missing or zero-byte"
fi

# 4. Stream cover bytes to stdout (capture via wc -c)
bytes=$($CLI items cover get --id "$FIRST_ITEM_ID" --output - 2>/dev/null | wc -c)
if [ "$bytes" -gt 0 ]; then
    pass "items cover get --output - streams non-zero bytes to stdout"
else
    fail "items cover get --output - streams non-zero bytes to stdout" "zero bytes"
fi

# 5. Remove cover
output=$($CLI items cover remove --id "$FIRST_ITEM_ID" 2>/dev/null)
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['success']" 2>/dev/null; then
    pass "items cover remove returns success"
else
    fail "items cover remove returns success" "unexpected response"
fi

# 6. Verify item now has null coverPath
output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['media'].get('coverPath') is None" 2>/dev/null; then
    pass "items get reports null coverPath after remove"
else
    fail "items get reports null coverPath after remove" "coverPath still set"
fi

# Cleanup
rm -rf "$COVER_TMP"
```

- [ ] **Step 3: Bring up ABS for live testing**

Per the project's docker-host-IP guidance:

```bash
docker compose -f /workspaces/abs-cli/docker/docker-compose.yml up -d
ABS_IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
for i in $(seq 1 30); do
    curl -sf "http://$ABS_IP/healthcheck" > /dev/null && echo "ready after ${i}s" && break
    sleep 1
done
ABS_URL=http://$ABS_IP bash /workspaces/abs-cli/docker/seed.sh
```

- [ ] **Step 4: Run the smoke suite against the AOT binary**

```bash
ABS_URL=http://$ABS_IP \
CLI=/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli \
bash /workspaces/abs-cli/docker/smoke-test.sh > /tmp/smoke.log 2>&1
tail -6 /tmp/smoke.log
grep -c "FAIL" /tmp/smoke.log
```

Expected: tail summary `Results: 122 passed, 0 failed` (was 116, added 6). FAIL count is 0.

- [ ] **Step 5: Tear down ABS**

```bash
docker compose -f /workspaces/abs-cli/docker/docker-compose.yml down -v
```

- [ ] **Step 6: Commit**

```bash
git add docker/smoke-test.sh
git commit -m "test: smoke coverage for items cover lifecycle"
```

---

## Task 9: Roadmap update + push + CI

Fix the misleading "Investigate cover handling" wording and ship.

**Files:**
- Modify: `docs/roadmap.md`

- [ ] **Step 1: Update the v0.3.0 cover-handling bullet in `docs/roadmap.md`**

Find the existing bullet:

```markdown
- **Investigate cover handling** — How cover metadata upload works end-to-end:
  which endpoints, request shapes, where ABS stores the image, how this
  interacts with `metadata covers` and `items update`. Scoping exercise
  before any command design — outcome may be a `covers` command or a
  documentation update, decided after investigation.
```

Replace with:

```markdown
- **`items cover` command** — Apply, fetch, and remove book covers via the
  ABS API. Three primitives (`set` with `--url` / `--file` / `--server-path`,
  `get` to file or stdout, `remove`) that the agent composes with
  `items list --filter "missing=cover"` and `metadata covers` to build a
  cover-handling workflow.
  Spec: [docs/specs/2026-04-28-items-cover-handling.md](specs/2026-04-28-items-cover-handling.md).
  Plan: [docs/plans/2026-04-28-items-cover-handling.md](plans/2026-04-28-items-cover-handling.md).
```

- [ ] **Step 2: Commit**

```bash
git add docs/roadmap.md
git commit -m "docs: update v0.3.0 cover-handling entry to reflect deliverable"
```

- [ ] **Step 3: Push**

```bash
git push -u origin feat/items-cover-handling
```

- [ ] **Step 4: Open the PR**

```bash
gh pr create --title "feat: add items cover command (set, get, remove)" --body "$(cat <<'EOF'
## Summary

- New `abs-cli items cover` subcommand tree with three verbs:
  - `set --url|--file|--server-path` (mutually exclusive) → POST/PATCH to ABS cover endpoints
  - `get --output <file|->` (file save with JSON descriptor, or binary to stdout)
  - `remove` → DELETE
- Wraps the four ABS endpoints we previously didn't expose (`POST`, `PATCH`, `GET`, `DELETE` on `/api/items/:id/cover`).
- Detection (`items list --filter "missing=cover"`) and provider search (`metadata covers`) already worked; this PR closes the gap so an agent can compose the full workflow.
- Four new models (`CoverApplyResponse`, `CoverApplyByUrlRequest`, `CoverLinkExistingRequest`, `CoverFileSavedDescriptor`), all source-gen registered in `AppJsonContext`. Self-test count goes 34 → 38.

Spec: `docs/specs/2026-04-28-items-cover-handling.md` · Plan: `docs/plans/2026-04-28-items-cover-handling.md`.

## Test plan

- [x] Local Debug build clean: 0 warnings, 0 errors
- [x] Full unit test suite green: 113 / 113 (was 103, added 10)
- [x] `dotnet format --verify-no-changes` exit 0
- [x] AOT Release publish succeeds; binary runs `self-test` 38 / 38
- [x] `--help` rendering verified for `items cover` and each subcommand
- [x] Live smoke test (`docker/smoke-test.sh`) against fresh ABS: 122 / 122 (was 116, added 6 cover-lifecycle assertions)
- [ ] CI: 6-platform AOT matrix green
EOF
)"
```

- [ ] **Step 5: Watch CI**

```bash
gh pr checks --watch
```

Expected: all 8 jobs green (`unit-test`, `smoke-test`, 6 `build` matrix entries). `update-homebrew` skipped.

This task produces no further commit beyond the roadmap edit.

---

## Self-Review

**1. Spec coverage check**

Walking the spec:

- "Add three new verbs under `items cover`" → Task 5 (`set`/`get`/`remove`).
- "Detection and provider-search already exist" → spec acknowledges; plan does not duplicate.
- ABS endpoints touched (`POST`, `POST` multipart, `PATCH`, `GET`, `DELETE`) → Task 4 (`CoversService`).
- New model types and JsonContext registration → Task 1.
- AOT discipline (multipart, stream, source-gen) → Tasks 3, 4, 5 use only AOT-safe primitives; Task 2 + Task 7 verify via self-test.
- `--output -` stdout binary mode → Task 5 Step 3 (`CreateCoverGetCommand` handler routes to `Console.OpenStandardOutput()`).
- Mutex validation on `set` → Task 5 Step 3 (`sources != 1` check + `Environment.Exit(1)`).
- Local-file existence check → Task 5 Step 3 (`File.Exists(file)` check).
- Self-test additions → Task 2.
- Smoke-test additions → Task 8.
- Roadmap update → Task 9 Step 1.
- "Files NOT modified" → no task touches `CHANGELOG.md`, `metadata covers` command, or `BookMedia*` models.
- "Out of scope" items (width/height/format query params, missing-cover shortcut, etc.) → not implemented.

No spec section is uncovered.

**2. Placeholder scan**

No "TBD" / "implement later" / "fill in details" / "appropriate error handling" phrases. Every code-changing step contains the literal code or shell command. The `git commit` messages are exact strings, not patterns.

**3. Type/name consistency**

- Branch name: `feat/items-cover-handling` — Task 0 Step 2 and Task 9 Step 3.
- Spec/plan file names: `2026-04-28-items-cover-handling.md` — used identically in Task 0 Step 3, Task 9 Step 4 PR body, Task 9 Step 1 roadmap link.
- Service name: `CoversService` — Task 4 file, Task 5 imports, Task 6 test file naming.
- Model names: `CoverApplyResponse`, `CoverApplyByUrlRequest`, `CoverLinkExistingRequest`, `CoverFileSavedDescriptor` — same spelling across Tasks 1, 2, 3, 4, 5, 6.
- Method names: `SetByUrlAsync`, `UploadFromFileAsync`, `LinkExistingAsync`, `GetStreamAsync`, `RemoveAsync` — same across Task 4 and Task 5 callers.
- Endpoint helper: `ApiEndpoints.ItemCover(id)` — Task 3 introduces, Task 4 uses.
- Test counts: 103 → 113 unit tests (added 4 service round-trip + 6 command help — confirmed in Task 6); 34 → 38 self-test (Task 2); 116 → 122 smoke (Task 8).
- Flag names: `--id`, `--url`, `--file`, `--server-path`, `--output`, `--raw` — consistent across Task 5 and Task 8.
- Exact command spelling `items cover set` (3 tokens, with space, not hyphenated) — consistent.
