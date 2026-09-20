# Streaming Uploads Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the client-side 2 GB ceiling on `abs-cli upload`, `backup upload` and cover upload by streaming files into the multipart body instead of buffering them into a `byte[]`.

**Architecture:** Three services currently call `File.ReadAllBytesAsync` and wrap the result in `ByteArrayContent`, hitting .NET's 2 GB single-object array cap. Each swaps to `File.OpenRead` + `StreamContent`, and each starts disposing its `MultipartFormDataContent` so the now-open file handles close. `UploadService` additionally extracts multipart construction into an `internal static` method so a test can build a 2.1 GB body without an HTTP server. Two adjacent corrections ride along: `backup upload`'s 10-minute timeout becomes infinite, and the 401 retry path stops silently re-POSTing an empty body for multipart requests.

**Tech Stack:** C# / .NET 10, xUnit, `System.Net.Http` multipart, NLog.

**Spec:** `docs/specs/2026-09-18-upload-streaming-design.md`
**Issue:** https://github.com/thomaslazar/abs-cli/issues/87

---

## Context for the implementer

You are working in `abs-cli`, a thin CLI wrapper over the Audiobookshelf HTTP API.

Things that are easy to get wrong here:

- **Run `dotnet format AbsCli.sln` after every C# edit.** CI fails on
  `dotnet format --verify-no-changes`. The repo also forbids gratuitous blank
  lines inside method bodies — no blank line between consecutive statements of the
  same kind, none before a trailing `return` after setup calls.
- **Do not change the multipart wire format.** Part names and per-part
  `Content-Type` headers must stay exactly as they are. `ByteArrayContent` with no
  explicit content type sends no `Content-Type` for that part, and `StreamContent`
  behaves the same way, so simply not setting one preserves current behavior.
  `BackupService` *does* set `application/octet-stream` today — keep that line.
- **Commit messages are Conventional Commits** (`type: subject`, imperative,
  lowercase, no trailing period). Do not add `Co-Authored-By:` or any
  "Generated with" attribution.
- **Do not touch `CHANGELOG.md`** — it belongs to the release process.
- **Do not touch `README.md`** — no verb or flag changes here.

Run the full unit suite with:

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
```

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `src/AbsCli/Services/UploadService.cs` | Modify (lines 18-56) | Extract `BuildUploadContent`, stream file parts, dispose content |
| `tests/AbsCli.Tests/Services/UploadServiceContentTests.cs` | Create | Prove a >2 GB file builds and drains |
| `src/AbsCli/Services/BackupService.cs` | Modify (lines 51-61) | Stream the backup part, dispose, infinite timeout |
| `src/AbsCli/Services/CoversService.cs` | Modify (lines 31-39) | Stream the cover part, dispose |
| `src/AbsCli/Api/AbsApiClient.cs` | Modify (lines 128-147, 411-426) | Refuse to replay an unreplayable 401 |

Task order matters: Task 1 lands the seam and the test that proves the whole
approach, so if anything about streaming is wrong it surfaces before the other two
call sites are touched.

---

### Task 1: Stream upload file parts

**Files:**
- Modify: `src/AbsCli/Services/UploadService.cs:18-56`
- Test: `tests/AbsCli.Tests/Services/UploadServiceContentTests.cs` (create)

- [ ] **Step 1: Write the failing test**

Create `tests/AbsCli.Tests/Services/UploadServiceContentTests.cs`:

```csharp
using AbsCli.Services;
using Xunit;

namespace AbsCli.Tests.Services;

public class UploadServiceContentTests
{
    // .NET's byte[] cap is 2 GiB; go past it so the old File.ReadAllBytesAsync
    // path throws IO_FileTooLong2GB. Sparse via SetLength, so this allocates no
    // blocks on ext4 (unit tests run on ubuntu-latest only).
    private const long OverTwoGigabytes = 2_200_000_000L;

    [Fact]
    public async Task BuildUploadContent_StreamsFileLargerThanTwoGigabytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"abs-cli-big-{Guid.NewGuid():N}.m4b");
        try
        {
            using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                fs.SetLength(OverTwoGigabytes);

            using var content = UploadService.BuildUploadContent(
                "lib-1", "fold-1", "Big Book", "Someone", null,
                new[] { (LocalPath: path, UploadName: "big.m4b") });

            await content.CopyToAsync(Stream.Null);

            Assert.NotNull(content.Headers.ContentLength);
            Assert.True(content.Headers.ContentLength > OverTwoGigabytes,
                $"expected a body larger than the file, got {content.Headers.ContentLength}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void BuildUploadContent_AddsOnePartPerFileAndOmitsNullFields()
    {
        var a = Path.Combine(Path.GetTempPath(), $"abs-cli-a-{Guid.NewGuid():N}.mp3");
        var b = Path.Combine(Path.GetTempPath(), $"abs-cli-b-{Guid.NewGuid():N}.mp3");
        try
        {
            File.WriteAllText(a, "a");
            File.WriteAllText(b, "b");

            using var content = UploadService.BuildUploadContent(
                "lib-1", "fold-1", "Two Parter", null, null,
                new[] { (LocalPath: a, UploadName: "01.mp3"), (LocalPath: b, UploadName: "02.mp3") });

            var names = content
                .Select(p => p.Headers.ContentDisposition!.Name!.Trim('"'))
                .ToList();
            // library/folder/title always; no author or series part when null; one
            // part per file, named by index.
            Assert.Equal(new[] { "library", "folder", "title", "0", "1" }, names);
        }
        finally
        {
            File.Delete(a);
            File.Delete(b);
        }
    }
}
```

`UploadService.BuildUploadContent` is `internal`, and `src/AbsCli/AbsCli.csproj`
already declares `<InternalsVisibleTo Include="AbsCli.Tests" />`, so the test project
can call it without any project change.

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter UploadServiceContentTests
```

Expected: FAIL to compile — `UploadService` has no `BuildUploadContent`.

- [ ] **Step 3: Extract the seam and stream the parts**

In `src/AbsCli/Services/UploadService.cs`, replace the body of `UploadAsync` from
the `var uploadTitle = ...` line through the `PostMultipartAsync` call, and add the
new method directly below `UploadAsync`.

`UploadAsync` becomes:

```csharp
    public async Task<UploadReceipt> UploadAsync(string libraryId, string folderId, string title,
        string? author, string? series, string? sequence,
        IReadOnlyList<(string LocalPath, string UploadName)> files)
    {
        var uploadTitle = sequence != null ? $"{sequence}. - {title}" : title;
        using var content = BuildUploadContent(libraryId, folderId, uploadTitle, author, series, files);
        await _client.PostMultipartAsync(ApiEndpoints.Upload, content, "'upload' permission",
            timeout: Timeout.InfiniteTimeSpan);
```

(everything from the `// ABS's upload endpoint returns HTTP 200` comment down to
the closing brace of the method stays exactly as it is.)

And the new method, placed immediately after `UploadAsync`:

```csharp
    /// <summary>
    /// Build the multipart body for an upload. Separate from <see cref="UploadAsync"/>
    /// so tests can construct a body without an HTTP server — the 2 GB failure this
    /// guards against is client-side, thrown while the body is assembled.
    /// </summary>
    /// <remarks>
    /// File parts are <see cref="StreamContent"/>, not <see cref="ByteArrayContent"/>:
    /// buffering a file into a byte[] caps uploads at .NET's 2 GB array limit, which
    /// ordinary 35h+ single-file m4b volumes exceed. The caller MUST dispose the
    /// returned content — that is what closes the open file handles.
    /// </remarks>
    internal static MultipartFormDataContent BuildUploadContent(string libraryId, string folderId,
        string uploadTitle, string? author, string? series,
        IReadOnlyList<(string LocalPath, string UploadName)> files)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(libraryId), "library");
        content.Add(new StringContent(folderId), "folder");
        content.Add(new StringContent(uploadTitle), "title");
        if (author != null)
            content.Add(new StringContent(author), "author");
        if (series != null)
            content.Add(new StringContent(series), "series");
        for (int i = 0; i < files.Count; i++)
        {
            // No explicit Content-Type: ByteArrayContent sent none either, and the
            // wire format must not change.
            var fileContent = new StreamContent(File.OpenRead(files[i].LocalPath));
            content.Add(fileContent, i.ToString(), files[i].UploadName);
        }
        return content;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter UploadServiceContentTests
```

Expected: PASS, 2 tests. The large-file test takes a few seconds — it drains
~2.2 GB of sparse zeros.

If `BuildUploadContent_StreamsFileLargerThanTwoGigabytes` fails with
"The file is too long", the `StreamContent` swap did not take effect. If it fails
on disk space, the filesystem did not honor the sparse `SetLength`; report this
rather than shrinking the constant below 2 GiB, which would defeat the test.

- [ ] **Step 5: Run the full suite and format**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
dotnet format AbsCli.sln
```

Expected: all tests pass; `dotnet format` makes no further changes when re-run.

- [ ] **Step 6: Commit**

```bash
git add src/AbsCli/Services/UploadService.cs tests/AbsCli.Tests/Services/UploadServiceContentTests.cs
git commit -m "fix: stream upload file parts instead of buffering them"
```

---

### Task 2: Stream the backup upload and uncap its timeout

**Files:**
- Modify: `src/AbsCli/Services/BackupService.cs:51-61`

No new test: `BackupService.UploadAsync` posts in the same method it builds in, and
adding an injectable-handler seam for a one-line change is not worth it. Task 1's
test covers the `StreamContent` behavior this repeats.

- [ ] **Step 1: Apply the change**

Replace the whole `UploadAsync` method in `src/AbsCli/Services/BackupService.cs`:

```csharp
    public async Task<BackupListResponse> UploadAsync(string filePath)
    {
        // StreamContent, not ByteArrayContent: a backup zip can exceed .NET's 2 GB
        // byte[] cap. The `using` is what closes the file handle.
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(File.OpenRead(filePath));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", System.IO.Path.GetFileName(filePath));
        // Infinite, unlike the other backup operations: the body size is the
        // operator's file, not something the server bounds, so 10 minutes is an
        // arbitrary wall for a multi-GB upload over a slow link.
        await _client.PostMultipartAsync(ApiEndpoints.BackupUpload, content, "'admin' access",
            timeout: Timeout.InfiniteTimeSpan);
        return await ListAsync();
    }
```

Leave `LongOperationTimeout` in place — create, apply and download still use it.

- [ ] **Step 2: Update the LongOperationTimeout comment**

The comment at `src/AbsCli/Services/BackupService.cs:8-10` still claims upload uses
it. Replace those three comment lines with:

```csharp
    // create/apply/download are sync server-side and can take minutes on large
    // libraries (SQLite dump capped at 2min, zip step uncapped). Override the default
    // 100s HTTP timeout so the CLI doesn't drop the connection while ABS is still
    // working. Upload opts out entirely — see UploadAsync.
```

- [ ] **Step 3: Build, test and format**

```bash
dotnet build AbsCli.sln
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
dotnet format AbsCli.sln
```

Expected: build succeeds, all tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/AbsCli/Services/BackupService.cs
git commit -m "fix: stream backup upload and drop its 10-minute cap"
```

---

### Task 3: Stream the cover upload

**Files:**
- Modify: `src/AbsCli/Services/CoversService.cs:31-39`

No new test, for the same reason as Task 2. Covers are images, so the 2 GB cap is
academic here — this changes for consistency, so nobody reads `ReadAllBytesAsync`
as the house pattern and copies it into the next upload path.

- [ ] **Step 1: Apply the change**

Replace the body of `UploadFromFileAsync` in `src/AbsCli/Services/CoversService.cs`,
keeping its existing `<summary>` doc comment above it:

```csharp
    public async Task<CoverApplyResponse> UploadFromFileAsync(string itemId, string localFilePath)
    {
        // StreamContent for consistency with the other upload paths; the `using`
        // closes the file handle.
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(File.OpenRead(localFilePath)), "cover", Path.GetFileName(localFilePath));
        return await _client.PostMultipartAsync(ApiEndpoints.ItemCover(itemId), content,
            AppJsonContext.Default.CoverApplyResponse, "'upload' permission");
    }
```

Note the ordering: the content is created before the part is added, and no
`Content-Type` is set on the part — it had none before.

- [ ] **Step 2: Build, test and format**

```bash
dotnet build AbsCli.sln
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
dotnet format AbsCli.sln
```

Expected: build succeeds, all tests pass.

- [ ] **Step 3: Commit**

```bash
git add src/AbsCli/Services/CoversService.cs
git commit -m "fix: stream cover upload instead of buffering it"
```

---

### Task 4: Refuse to replay an unreplayable 401

**Files:**
- Modify: `src/AbsCli/Api/AbsApiClient.cs:128-147` (both `PostMultipartAsync` overloads)
- Modify: `src/AbsCli/Api/AbsApiClient.cs:411-426` (`EnsureSuccessOrHandleAuthAsync`)

Background: on a 401 the client refreshes the token and retries with
`new HttpRequestMessage(method, endpoint)` — with **no body**. For a multipart POST
that re-sends an empty request, and `upload` synthesizes its receipt from the
request rather than the response, so the operator is told the upload succeeded. Now
that the body is a consumed stream it cannot be replayed at all, so the path must
fail honestly instead.

No unit test: `EnsureSuccessOrHandleAuthAsync` calls `Environment.Exit`, and
`AbsApiClient` builds its own `HttpClient` internally, so there is no seam to inject
a 401 through. Adding one is out of scope for this plan.

- [ ] **Step 1: Add the `replayable` parameter**

In `src/AbsCli/Api/AbsApiClient.cs`, change the signature of
`EnsureSuccessOrHandleAuthAsync`:

```csharp
    private async Task EnsureSuccessOrHandleAuthAsync(
        HttpResponseMessage response, HttpMethod method, string endpoint,
        string? permissionHint = null,
        string? notFoundHint = null,
        bool replayable = true)
```

- [ ] **Step 2: Handle the unreplayable case**

Replace the `if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)`
block (the whole `if`, up to but not including `else if (!response.IsSuccessStatusCode)`):

```csharp
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await RefreshTokenAsync();
            if (!replayable)
            {
                // The request body is a consumed file stream; resending the URL
                // alone would post an empty multipart, and upload reports success
                // from the request rather than the response — so a silent retry
                // here reads as a successful upload that never happened. The token
                // is refreshed above, so a re-run succeeds immediately.
                _logger.Error("Session expired mid-request and the upload body cannot be resent. " +
                    "Tokens have been refreshed — re-run the command.");
                Environment.Exit(2);
            }
            var retryRequest = new HttpRequestMessage(method, endpoint);
            var retryResponse = await _http.SendAsync(retryRequest);
            if (!retryResponse.IsSuccessStatusCode)
            {
                _logger.Error($"API request failed after token refresh: {(int)retryResponse.StatusCode} {retryResponse.ReasonPhrase}");
                Environment.Exit(2);
            }
        }
```

- [ ] **Step 3: Pass `replayable: false` from both multipart overloads**

In the non-generic `PostMultipartAsync` (around line 134):

```csharp
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Post, endpoint, permissionHint, notFoundHint,
            replayable: false);
```

In the generic `PostMultipartAsync<T>` (around line 143), the identical change:

```csharp
        await EnsureSuccessOrHandleAuthAsync(response, HttpMethod.Post, endpoint, permissionHint, notFoundHint,
            replayable: false);
```

Leave every other caller alone — they default to `replayable: true`.

- [ ] **Step 4: Build, test and format**

```bash
dotnet build AbsCli.sln
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
dotnet format AbsCli.sln
```

Expected: build succeeds, all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/AbsCli/Api/AbsApiClient.cs
git commit -m "fix: fail loudly when a 401 hits an unreplayable upload body"
```

---

### Task 5: Verify against a live server

**Files:** none — verification only.

The unit test proves the >2 GB body assembles and drains, but not that a streamed
multipart still round-trips through the real HTTP path. Per the repo's pre-PR rule,
the smoke test must pass before any PR is opened.

- [ ] **Step 1: Bring up a clean stack**

```bash
cd docker && docker compose down -v && docker compose up -d
```

Wait for the healthcheck to answer:

```bash
curl -sf http://docker-audiobookshelf-1.orb.local:80/healthcheck && echo up
```

- [ ] **Step 2: Seed it**

```bash
ABS_URL=http://docker-audiobookshelf-1.orb.local:80 bash docker/seed.sh
```

- [ ] **Step 3: Run the smoke test**

```bash
ABS_URL=http://docker-audiobookshelf-1.orb.local:80 bash docker/smoke-test.sh
```

Expected: the suite passes. It exercises `upload`, cover upload and `backup upload`
— all three changed paths — over real HTTP.

`smoke-test.sh` is **not idempotent**: it creates and deletes library items, so a
second run against the same stack fails on item-count assertions. Those failures are
artifacts of a dirty stack, not regressions. To re-run, repeat Steps 1-3 from
`docker compose down -v`.

- [ ] **Step 4: Report the result**

Report the actual smoke outcome — pass or fail, with the failing output if it
failed. Do not mark the smoke as passed in a PR description without having run it.

---

## Done when

- `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj` passes, including the two new
  `UploadServiceContentTests`.
- `dotnet format AbsCli.sln --verify-no-changes` is clean.
- `docker/smoke-test.sh` passes against a freshly seeded stack.
- No `File.ReadAllBytesAsync` remains in `src/AbsCli/Services/` — check with
  `grep -rn "ReadAllBytes" src/AbsCli --include=*.cs --exclude-dir=bin --exclude-dir=obj`
  (the bare form drowns in AOT debug artifacts under `src/AbsCli/bin/`).
