# `authors image` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add three new verbs under `authors image` (`set`, `get`, `remove`) wrapping the three ABS author image endpoints (POST/GET/DELETE `/api/authors/:id/image`). Each command is a thin pass-through to one HTTP call. `set` is URL-only because that is all ABS supports; `get` mirrors the items-cover get pattern (file output or `-` for stdout, optional `--raw`); `remove` returns the updated `AuthorItem`.

**Architecture:** Two new typed models (`AuthorImageRequest` for the POST body, `AuthorImageResponse` for set/remove responses) registered in `AppJsonContext`. Three new methods on `AuthorsService` mirroring `CoversService`'s shape. New `image` subcommand group inside `AuthorsCommand`, with `set`/`get`/`remove` factories that follow the existing items-cover patterns.

**Tech Stack:** C# / .NET 10 / `System.CommandLine` 2.0.7 / `System.Text.Json` source-gen / existing `AbsApiClient.GetStreamAsync` for binary download. No new packages.

**Spec:** [docs/specs/2026-05-08-authors-image.md](../specs/2026-05-08-authors-image.md)

---

## File Structure

**New files:**

- `src/AbsCli/Models/AuthorImageResponse.cs` — single `AuthorImageResponse` class wrapping `AuthorItem` for the `{author: {...}}` response shape.
- `tests/AbsCli.Tests/Commands/AuthorsImageCommandTests.cs` — help-render tests for the three subcommands.

**Modified files:**

- `src/AbsCli/Models/AuthorRequests.cs` — append `AuthorImageRequest` class for the POST body `{url}`.
- `src/AbsCli/Models/JsonContext.cs` — register `AuthorImageRequest` and `AuthorImageResponse`.
- `src/AbsCli/Api/ApiEndpoints.cs` — add `AuthorImage(string id)` endpoint helper.
- `src/AbsCli/Services/AuthorsService.cs` — add `SetImageAsync`, `GetImageStreamAsync`, `RemoveImageAsync` methods.
- `src/AbsCli/Commands/AuthorsCommand.cs` — add `CreateImageCommand` factory plus `CreateImageSetCommand` / `CreateImageGetCommand` / `CreateImageRemoveCommand`; register `image` as a subcommand group inside `authors`.
- `src/AbsCli/Commands/SelfTestCommand.cs` — add round-trip checks for the two new types.
- `docker/smoke-test.sh` — extend Authors section with set/get/remove cycle.

**Files explicitly NOT modified:**

- `CHANGELOG.md` — release-owned.
- `docs/roadmap.md` — feature-level abstraction.
- `src/AbsCli/Models/Author.cs` (`AuthorItem`) — already has the fields we need; reused as-is.
- `src/AbsCli/Models/CoverModels.cs` (`CoverFileSavedDescriptor`) — already has `{path, bytes}`; reused for `authors image get --output <file>`.

---

## Task 0: Commit the plan file

**Files:** none new (plan file already exists on disk).

- [ ] **Step 1: Confirm branch state**

```bash
cd /workspaces/abs-cli
git status
git log --oneline -3
```

Expected: on branch `feat/authors-image`; HEAD is `docs: spec for v0.4.0 authors image`; working tree shows the plan file as untracked.

- [ ] **Step 2: Commit the plan**

```bash
git add docs/plans/2026-05-08-authors-image.md
git commit -m "docs: plan for v0.4.0 authors image"
```

---

## Task 1: Models + JsonContext + ApiEndpoints

**Files:**
- Modify: `src/AbsCli/Models/AuthorRequests.cs`
- Create: `src/AbsCli/Models/AuthorImageResponse.cs`
- Modify: `src/AbsCli/Models/JsonContext.cs`
- Modify: `src/AbsCli/Api/ApiEndpoints.cs`

- [ ] **Step 1: Append `AuthorImageRequest` to `AuthorRequests.cs`**

Open `src/AbsCli/Models/AuthorRequests.cs`. The file currently contains `AuthorMatchRequest` only. Add this class at the bottom of the file (after `AuthorMatchRequest`, inside the same namespace):

```csharp
/// <summary>
/// Body for POST /api/authors/:id/image. ABS validates that the URL
/// starts with http:/https: and downloads the image server-side.
/// </summary>
public class AuthorImageRequest
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}
```

- [ ] **Step 2: Create `src/AbsCli/Models/AuthorImageResponse.cs`**

Create the file with this content:

```csharp
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// Response from POST /api/authors/:id/image and DELETE /api/authors/:id/image.
/// Server returns { author: { ... } }.
/// </summary>
public class AuthorImageResponse
{
    [JsonPropertyName("author")]
    public AuthorItem? Author { get; set; }
}
```

- [ ] **Step 3: Register in `src/AbsCli/Models/JsonContext.cs`**

Open `src/AbsCli/Models/JsonContext.cs`. Locate the existing `AuthorUpdateResponse` registration (it is in the author cluster of `[JsonSerializable]` attributes added in Spec B). Append two new lines immediately after `[JsonSerializable(typeof(AuthorUpdateResponse))]`:

```csharp
[JsonSerializable(typeof(AuthorImageRequest))]
[JsonSerializable(typeof(AuthorImageResponse))]
```

- [ ] **Step 4: Add `AuthorImage` endpoint helper**

In `src/AbsCli/Api/ApiEndpoints.cs`, locate the existing `AuthorMatch` line:

```csharp
public static string AuthorMatch(string id) => $"/api/authors/{id}/match";
```

Add immediately below it:

```csharp
public static string AuthorImage(string id) => $"/api/authors/{id}/image";
```

- [ ] **Step 5: Build and verify**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug 2>&1 | tail -5
```

Expected: 0 warnings, 0 errors. The source generator picks up the two new types as `AppJsonContext.Default.AuthorImageRequest` / `AppJsonContext.Default.AuthorImageResponse`.

- [ ] **Step 6: Format check**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: clean exit (no output).

- [ ] **Step 7: Commit**

```bash
git add src/AbsCli/Models/AuthorRequests.cs \
        src/AbsCli/Models/AuthorImageResponse.cs \
        src/AbsCli/Models/JsonContext.cs \
        src/AbsCli/Api/ApiEndpoints.cs
git commit -m "feat: add author image request/response models and endpoint"
```

---

## Task 2: Self-test round-trip checks

Catch any AOT serialization regression in the published binary.

**Files:**
- Modify: `src/AbsCli/Commands/SelfTestCommand.cs`

- [ ] **Step 1: Locate the Author Models section**

```bash
grep -n "=== Author Models ===" /workspaces/abs-cli/src/AbsCli/Commands/SelfTestCommand.cs
```

There is exactly one match (added in Spec B). Find the last `Check(...)` block in that section — it should be the Dictionary tri-state check (`Check("Update-body Dictionary tri-state serialization", () => { ... });`). The new checks go immediately after that block, still inside the Author Models section, before the next section's `Console.Error.WriteLine();` line.

- [ ] **Step 2: Add two `Check(...)` blocks**

Insert these two blocks at the location identified above:

```csharp
            Check("AuthorImageRequest round-trip", () =>
            {
                var obj = new AuthorImageRequest { Url = "https://example.com/img.png" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.AuthorImageRequest);
                Assert(json.Contains("\"url\": \"https://example.com/img.png\""), $"url: {json}");
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorImageRequest)!;
                Assert(back.Url == "https://example.com/img.png", $"url: {back.Url}");
            });

            Check("AuthorImageResponse round-trip", () =>
            {
                var obj = new AuthorImageResponse
                {
                    Author = new AuthorItem { Id = "aut_xyz", Name = "Brandon Sanderson", ImagePath = "/m/authors/x.jpg" }
                };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.AuthorImageResponse);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.AuthorImageResponse)!;
                Assert(back.Author?.Name == "Brandon Sanderson", $"author.name: {back.Author?.Name}");
                Assert(back.Author?.ImagePath == "/m/authors/x.jpg", $"author.imagePath: {back.Author?.ImagePath}");
            });
```

The literal substring `"\"url\": \"https://example.com/img.png\""` in the `AuthorImageRequest` check uses the colon-space form because `AppJsonContext` has `JsonSourceGenerationOptions(WriteIndented = true)` — same lesson learned in Spec B Task 2 fix-up.

- [ ] **Step 3: Build and run self-test**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug 2>&1 | tail -5
dotnet run --project /workspaces/abs-cli/src/AbsCli -c Debug -- self-test 2>&1 | grep -A 20 "Author Models"
```

Expected: build clean (0 warnings, 0 errors). Self-test output shows the two new checks (`AuthorImageRequest round-trip`, `AuthorImageResponse round-trip`) passing alongside the existing Spec-B author-model checks.

- [ ] **Step 4: Commit**

```bash
git add src/AbsCli/Commands/SelfTestCommand.cs
git commit -m "test: self-test round-trip for author image models"
```

---

## Task 3: AuthorsService image methods

**Files:**
- Modify: `src/AbsCli/Services/AuthorsService.cs`

- [ ] **Step 1: Add the three methods**

In `src/AbsCli/Services/AuthorsService.cs`, locate the existing `DeleteAsync` method (the last method in the class). Insert the three new methods immediately before `DeleteAsync` (so they sit alongside the other author-modifying methods):

```csharp
    public async Task<AuthorImageResponse> SetImageAsync(string id, string url)
    {
        var json = JsonSerializer.Serialize(
            new AuthorImageRequest { Url = url },
            AppJsonContext.Default.AuthorImageRequest);
        return await _client.PostAsync(
            ApiEndpoints.AuthorImage(id),
            json,
            AppJsonContext.Default.AuthorImageResponse,
            "'upload' permission");
    }

    public async Task<Stream> GetImageStreamAsync(string id, bool raw)
    {
        var url = ApiEndpoints.AuthorImage(id);
        if (raw) url += "?raw=1";
        return await _client.GetStreamAsync(url);
    }

    public async Task<AuthorImageResponse> RemoveImageAsync(string id)
    {
        return await _client.DeleteAsync(
            ApiEndpoints.AuthorImage(id),
            AppJsonContext.Default.AuthorImageResponse,
            "'delete' permission");
    }
```

The permission hint strings (`'upload' permission`, `'delete' permission`) feed into AbsApiClient's friendly-error path on 403 responses. Permission keys match ABS's `AuthorController.middleware` (DELETE → `canDelete`) and `uploadImage` body (`canUpload`).

- [ ] **Step 2: Build**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug 2>&1 | tail -5
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 3: Format check**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: clean.

- [ ] **Step 4: Commit**

```bash
git add src/AbsCli/Services/AuthorsService.cs
git commit -m "feat: extend AuthorsService with set/get/remove image"
```

---

## Task 4: `authors image` command + tests

**Files:**
- Modify: `src/AbsCli/Commands/AuthorsCommand.cs`
- Create: `tests/AbsCli.Tests/Commands/AuthorsImageCommandTests.cs`

- [ ] **Step 1: Create the test file with failing tests (TDD red)**

Create `tests/AbsCli.Tests/Commands/AuthorsImageCommandTests.cs`:

```csharp
using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class AuthorsImageCommandTests
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
    public void AuthorsImage_TopLevel_Help_ListsThreeVerbs()
    {
        var output = RenderHelp("authors", "image");
        Assert.Contains("set", output);
        Assert.Contains("get", output);
        Assert.Contains("remove", output);
    }

    [Fact]
    public void AuthorsImageSet_Help_DocumentsUrlOnly()
    {
        var output = RenderHelp("authors", "image", "set");
        Assert.Contains("--id", output);
        Assert.Contains("--url", output);
        Assert.DoesNotContain("--file", output);
        Assert.DoesNotContain("--server-path", output);
    }

    [Fact]
    public void AuthorsImageSet_Help_ShowsResponseShape()
    {
        var output = RenderHelp("authors", "image", "set");
        Assert.Contains("Response shape:", output);
        Assert.Contains("\"author\"", output);
    }

    [Fact]
    public void AuthorsImageGet_Help_DocumentsOutputAndRaw()
    {
        var output = RenderHelp("authors", "image", "get");
        Assert.Contains("--id", output);
        Assert.Contains("--output", output);
        Assert.Contains("--raw", output);
    }

    [Fact]
    public void AuthorsImageRemove_Help_RequiresIdOnly()
    {
        var output = RenderHelp("authors", "image", "remove");
        Assert.Contains("--id", output);
        Assert.DoesNotContain("--url", output);
        Assert.DoesNotContain("--output", output);
    }

    [Fact]
    public void AuthorsImageRemove_Help_DocumentsNoCurrentImageQuirk()
    {
        var output = RenderHelp("authors", "image", "remove");
        Assert.Contains("No current image", output);
        Assert.Contains("Bad request", output);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~AuthorsImageCommandTests" -c Debug --nologo
```

Expected: all six tests fail. The `image` subcommand group does not exist yet, so `RenderHelp("authors", "image", ...)` produces an error message rather than help output for the requested subcommand.

- [ ] **Step 3: Implement `CreateImageCommand` and its three children in `AuthorsCommand.cs`**

In `src/AbsCli/Commands/AuthorsCommand.cs`, locate the `Create()` method. Add a new subcommand registration immediately after the existing `command.Subcommands.Add(CreateDeleteCommand());` line:

```csharp
        command.Subcommands.Add(CreateImageCommand());
```

Then add the four new factory methods at the end of the `AuthorsCommand` class (before the closing `}` of the class). Place them after `CreateDeleteCommand`:

```csharp
    private static Command CreateImageCommand()
    {
        var command = new Command("image", "Manage author images (set, get, remove)");
        command.Subcommands.Add(CreateImageSetCommand());
        command.Subcommands.Add(CreateImageGetCommand());
        command.Subcommands.Add(CreateImageRemoveCommand());
        return command;
    }

    private static Command CreateImageSetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var urlOption = new Option<string>("--url") { Description = "Image URL (http or https) — ABS server downloads it", Required = true };
        var command = new Command("set", "Set the author image from a URL")
        { idOption, urlOption };
        command.AddExamples(
            "abs-cli authors image set --id \"aut_xyz\" --url \"https://example.com/author.png\"");
        command.AddResponseExample<AuthorImageResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var url = parseResult.GetValue(urlOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            var result = await service.SetImageAsync(id, url);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.AuthorImageResponse);
            return 0;
        });
        return command;
    }

    private static Command CreateImageGetCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var outputOption = new Option<string>("--output") { Description = "Output file path, or '-' for binary to stdout", Required = true };
        var rawOption = new Option<bool>("--raw") { Description = "Fetch the original unprocessed image (default: ABS-resized)" };
        var command = new Command("get", "Download the author image")
        { idOption, outputOption, rawOption };
        command.AddExamples(
            "abs-cli authors image get --id \"aut_xyz\" --output author.jpg",
            "abs-cli authors image get --id \"aut_xyz\" --output author.png --raw",
            "abs-cli authors image get --id \"aut_xyz\" --output - > author.jpg");
        command.AddResponseExample<CoverFileSavedDescriptor>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var output = parseResult.GetValue(outputOption)!;
            var raw = parseResult.GetValue(rawOption);
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            await using var stream = await service.GetImageStreamAsync(id, raw);
            if (output == "-")
            {
                await using var stdout = Console.OpenStandardOutput();
                await stream.CopyToAsync(stdout);
                return 0;
            }
            long bytes;
            await using (var fileStream = new FileStream(output, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fileStream);
                bytes = fileStream.Length;
            }
            var descriptor = new CoverFileSavedDescriptor { Path = output, Bytes = bytes };
            ConsoleOutput.WriteJson(descriptor, AppJsonContext.Default.CoverFileSavedDescriptor);
            return 0;
        });
        return command;
    }

    private static Command CreateImageRemoveCommand()
    {
        var idOption = new Option<string>("--id") { Description = "Author ID", Required = true };
        var command = new Command("remove", "Remove the author image")
        { idOption };
        command.AddHelpSection("Notes", HelpSectionPosition.Top,
            "No current image → exit 2, stderr \"Bad request. Author has no image",
            "path set\". Check imagePath via 'authors get' first if needed.");
        command.AddExamples(
            "abs-cli authors image remove --id \"aut_xyz\"");
        command.AddResponseExample<AuthorImageResponse>();
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idOption)!;
            var (client, _) = CommandHelper.BuildClient();
            var service = new AuthorsService(client);
            var result = await service.RemoveImageAsync(id);
            ConsoleOutput.WriteJson(result, AppJsonContext.Default.AuthorImageResponse);
            return 0;
        });
        return command;
    }
```

`CoverFileSavedDescriptor` lives in `AbsCli.Models`, which is already imported at the top of `AuthorsCommand.cs` (the existing usings include `AbsCli.Models`). `FileStream` and `Console.OpenStandardOutput` resolve via `System.IO` which is in the global `<ImplicitUsings>net10.0</ImplicitUsings>` set — no extra `using` directives needed.

- [ ] **Step 4: Run tests to verify they pass (TDD green)**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~AuthorsImageCommandTests" -c Debug --nologo
```

Expected: all six tests pass.

- [ ] **Step 5: Run the full test suite to confirm no regressions**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln -c Debug --nologo 2>&1 | tail -3
```

Expected: all tests pass.

- [ ] **Step 6: Format check**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: clean exit.

- [ ] **Step 7: Commit**

```bash
git add src/AbsCli/Commands/AuthorsCommand.cs \
        tests/AbsCli.Tests/Commands/AuthorsImageCommandTests.cs
git commit -m "feat: add 'authors image set/get/remove' subcommand group"
```

---

## Task 5: Smoke test extension

The dev compose stack must be up. Container IP is resolvable via `docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}'`. The test image URL is `https://placehold.co/64x64.png` — a long-standing free placeholder service. ABS resolves it from inside its own container using its own outbound network.

**Files:**
- Modify: `docker/smoke-test.sh`

- [ ] **Step 1: Locate the end of the Authors section**

```bash
grep -n "(authors delete: covered\|=== Search Command" /workspaces/abs-cli/docker/smoke-test.sh
```

Find the line `# Restore book to original authors (merge added Jim Butcher to FIRST_ITEM_ID)` followed by `$CLI items update --id "$FIRST_ITEM_ID" --input "$RESTORE_PAYLOAD" 2>/dev/null > /dev/null` (the very last block of the Authors section, restoring book authors after the merge test). The new image-cycle block goes immediately after that restore line and before the `# ====...` divider that starts the Search section.

- [ ] **Step 2: Insert the image cycle**

Add this block after the merge-test restore line and before the next `# ============================================================`:

```bash
# --- image set/get/remove ---
output=$($CLI authors image set --id "$AUTHOR_ID" --url "https://placehold.co/64x64.png" 2>/dev/null)
assert_json_key "authors image set returns author" "author" "$output"
assert_json_expr "authors image set populated imagePath" \
    "d['author'].get('imagePath') is not None and d['author']['imagePath']!=''" "$output"

IMG_TMP=$(mktemp --suffix=.png)
output=$($CLI authors image get --id "$AUTHOR_ID" --output "$IMG_TMP" 2>/dev/null)
assert_json_expr "authors image get descriptor reports bytes" "d['bytes']>0" "$output"
if [ -s "$IMG_TMP" ]; then
    pass "authors image get wrote non-empty file"
else
    fail "authors image get wrote non-empty file" "file is empty or missing"
fi
rm -f "$IMG_TMP"

IMG_TMP_RAW=$(mktemp --suffix=.png)
output=$($CLI authors image get --id "$AUTHOR_ID" --output "$IMG_TMP_RAW" --raw 2>/dev/null)
assert_json_expr "authors image get --raw descriptor reports bytes" "d['bytes']>0" "$output"
if [ -s "$IMG_TMP_RAW" ]; then
    pass "authors image get --raw wrote non-empty file"
else
    fail "authors image get --raw wrote non-empty file" "file is empty or missing"
fi
rm -f "$IMG_TMP_RAW"

output=$($CLI authors image remove --id "$AUTHOR_ID" 2>/dev/null)
assert_json_key "authors image remove returns author" "author" "$output"
assert_json_expr "authors image remove cleared imagePath" \
    "d['author'].get('imagePath') is None" "$output"

# Removing again should fail with 400 (the documented quirk)
error_output=$($CLI authors image remove --id "$AUTHOR_ID" 2>&1 || true)
if echo "$error_output" | grep -q "Bad request"; then
    pass "authors image remove on no-image surfaces as 400"
else
    fail "authors image remove on no-image surfaces as 400" "got: ${error_output:0:200}"
fi
```

The double-remove pattern uses `2>&1 || true` so the script's `set -e` does not kill the run when the CLI exits 2; this matches the existing permission-denied test pattern (see `docker/smoke-test.sh` for `upload as testuser shows permission denied` which uses the same idiom).

- [ ] **Step 3: Verify bash syntax**

```bash
bash -n /workspaces/abs-cli/docker/smoke-test.sh
```

Expected: no output (clean parse).

- [ ] **Step 4: Resolve the ABS container IP and confirm reachability**

```bash
ABS_IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
echo "ABS_IP=$ABS_IP"
curl -sf "http://$ABS_IP:80/healthcheck" && echo
```

Expected: prints `ABS_IP=<some-ip>` and `OK` from the healthcheck. If the container is not running, bring it up: `cd docker && docker compose up -d`. If the seed has been wiped, run `ABS_URL="http://$ABS_IP:80" bash /workspaces/abs-cli/docker/seed.sh` before continuing.

- [ ] **Step 5: Run the full smoke test against the dev instance**

```bash
ABS_URL="http://$ABS_IP:80" bash /workspaces/abs-cli/docker/smoke-test.sh 2>&1 | tail -3
```

The smoke script will publish a fresh AOT binary on the first run. Expected: `Results: <N> passed, 0 failed` (N is the previous count plus the seven new image-cycle assertions). If the only failure is the existing transient `metadata covers returned a URL for seeded book` (a Google API flake unrelated to this work), retry once. Do NOT proceed if anything else fails.

- [ ] **Step 6: Run a second time to confirm idempotency**

```bash
ABS_URL="http://$ABS_IP:80" bash /workspaces/abs-cli/docker/smoke-test.sh 2>&1 | tail -3
```

Expected: same `0 failed` result. The image cycle is idempotent — each run sets, gets, removes, and confirms the second remove fails. End state matches the start state (Brandon Sanderson with no image).

- [ ] **Step 7: Commit**

```bash
git add docker/smoke-test.sh
git commit -m "test: smoke coverage for authors image set/get/remove"
```

---

## Task 6: Final verification + push + PR

**Files:** none.

- [ ] **Step 1: Format check**

```bash
cd /workspaces/abs-cli
dotnet format AbsCli.sln --verify-no-changes
```

Expected: clean exit.

- [ ] **Step 2: Full test suite (Release config)**

```bash
dotnet test AbsCli.sln -c Release --nologo
```

Expected: all tests pass.

- [ ] **Step 3: AOT publish + self-test**

```bash
dotnet publish src/AbsCli -c Release -o /tmp/abs-cli-publish
/tmp/abs-cli-publish/abs-cli self-test 2>&1 | tail -10
```

Expected: AOT publish succeeds with no `IL2026`/`IL3050` trim warnings. `self-test` exits 0 and the new `AuthorImageRequest`/`AuthorImageResponse` round-trip checks pass alongside the rest.

- [ ] **Step 4: Render every new help screen**

```bash
/tmp/abs-cli-publish/abs-cli authors image --help
/tmp/abs-cli-publish/abs-cli authors image set --help
/tmp/abs-cli-publish/abs-cli authors image get --help
/tmp/abs-cli-publish/abs-cli authors image remove --help
```

Expected:
- Group help lists `set`, `get`, `remove`.
- `set --help` shows `--id` and `--url` only; no `--file` / `--server-path`.
- `get --help` shows `--id`, `--output`, `--raw` plus a `Response shape:` section showing `path`/`bytes`.
- `remove --help` shows the Notes section with the "No current image → exit 2" line and a `Response shape:` section showing `author`.

- [ ] **Step 5: Re-run the smoke test against the AOT binary**

```bash
ABS_IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
CLI=/tmp/abs-cli-publish/abs-cli ABS_URL="http://$ABS_IP:80" bash /workspaces/abs-cli/docker/smoke-test.sh 2>&1 | tail -3
```

Expected: `0 failed`. Catches AOT-only regressions Debug-mode tests miss.

- [ ] **Step 6: Push the branch**

```bash
git push -u origin feat/authors-image
```

- [ ] **Step 7: Open a pull request to main**

```bash
gh pr create --title "feat: authors image set/get/remove (v0.4.0 Spec C)" --body "$(cat <<'EOF'
## Summary

Implements [Spec C of v0.4.0](docs/specs/2026-05-08-authors-image.md):
three new verbs under \`abs-cli authors image\` — \`set\`, \`get\`,
\`remove\` — wrapping the three ABS author image endpoints
(\`POST\`/\`GET\`/\`DELETE /api/authors/:id/image\`).

- \`authors image set --id X --url <https-url>\` — ABS downloads the
  URL server-side. URL-only because that is all ABS supports for author
  images (unlike \`items cover set\` which has \`--file\` and
  \`--server-path\` modes).
- \`authors image get --id X --output <file|->\` \`[--raw]\` — same
  pattern as \`items cover get\`. \`--raw\` returns the original stored
  file; without it ABS resizes/transcodes (default webp/jpeg).
- \`authors image remove --id X\` — clears the imagePath and deletes
  the cached file. Returns 400 if the author already has no image
  (documented as exit 2 in \`--help\` Notes).

This completes the v0.4.0 author-management surface. Spec A (pagination)
shipped in #32. Spec B (modification: match/lookup/update/delete) shipped
in #31.

## Test plan

- [x] \`dotnet format --verify-no-changes\` clean
- [x] \`dotnet test\` — full suite green, including new \`AuthorsImageCommandTests\` (6 cases)
- [x] AOT publish + \`self-test\` clean — new round-trip checks pass
- [x] All four new \`--help\` screens render correctly with options, examples, Notes (where applicable), and Response shapes
- [x] \`docker/smoke-test.sh\` — full image cycle (set → get → get --raw → remove → remove-again-expect-400) passes idempotently against the dev ABS instance
EOF
)"
```

DO NOT include `Co-Authored-By` or `Generated with Claude Code` lines.

- [ ] **Step 8: Capture the PR URL**

```bash
gh pr view --json url -q .url
```

---

## Self-Review

Spec coverage check (each section/requirement of the spec is implemented by a task):

- ✅ Command surface (3 new verbs under `image` group) — Task 4 Step 3.
- ✅ ABS endpoints (POST/GET/DELETE `/api/authors/:id/image`) — Task 1 Step 4 (endpoint helper) + Task 3 Step 1 (service methods).
- ✅ JSON model (new `AuthorImageRequest`, `AuthorImageResponse`; reuse `CoverFileSavedDescriptor` for get-to-file output) — Task 1 Steps 1+2+3 + Task 4 Step 3.
- ✅ Help-render tests for all three subcommands and the group — Task 4 Step 1.
- ✅ `--file` and `--server-path` confirmed absent on `set --help` — Task 4 Step 1's `AuthorsImageSet_Help_DocumentsUrlOnly` test.
- ✅ Notes section on `remove` for the 400 quirk — Task 4 Step 3.
- ✅ Self-test round-trip for both new types — Task 2 Step 2.
- ✅ Smoke set/get/remove cycle including the double-remove-expect-400 — Task 5 Step 2.
- ✅ Final verification (format, full tests, AOT, smoke against AOT, push, PR) — Task 6.

Placeholder scan: no "TBD" / "TODO" / "implement later" / "similar to Task N" remain. Every code block is complete.

Type-consistency check:
- `AuthorImageRequest.Url` (string) — matches Task 1 declaration, Task 2 round-trip, Task 3 `SetImageAsync` body, Task 4 command-layer construction.
- `AuthorImageResponse.Author` (`AuthorItem?`) — same; round-tripped, returned by `SetImageAsync` and `RemoveImageAsync`, written via `AppJsonContext.Default.AuthorImageResponse`.
- `SetImageAsync(string id, string url)` / `GetImageStreamAsync(string id, bool raw)` / `RemoveImageAsync(string id)` — signatures consistent across declaration (Task 3) and call sites (Task 4).
- `ApiEndpoints.AuthorImage(string id)` — declared in Task 1 Step 4, referenced by all three service methods in Task 3 Step 1.
- `CoverFileSavedDescriptor` — pre-existing; used identically by `items cover get` and now `authors image get`.
