# Streaming uploads — design

**Date:** 2026-09-18
**Status:** approved, not yet implemented
**Issue:** [#87](https://github.com/thomaslazar/abs-cli/issues/87)

## Problem

`abs-cli upload` fails for any single source file of 2 GB or larger, client-side,
before the ABS server is ever contacted:

```
ERROR The file is too long. This operation is currently limited to supporting
files less than 2 gigabytes in size.
```

That is `SR.IO_FileTooLong2GB`, thrown by `File.ReadAllBytesAsync`. Three services
buffer an entire file into a `byte[]` before building the multipart body, so each
hits .NET's 2 GB single-object array cap:

| Call site | Command | Realistic exposure |
|---|---|---|
| `Services/UploadService.cs:33` | `upload` (both `--files` and `--files-manifest`) | high |
| `Services/BackupService.cs:54` | `backup upload` | high |
| `Services/CoversService.cs:33` | cover upload | academic (images) |

Single-file `.m4b` volumes above 2 GB are ordinary for long books — a 35+ hour
volume at a normal bitrate clears it easily. The reporting batch lost 3 of 31 books
purely on size (2.06, 2.21 and 2.41 GB; 36, 39 and 43 hours). `items encode-m4b`
also produces single-file merges, so a multi-file book that uploads fine today can
become un-re-uploadable after merging.

Buffering also sets peak memory at file size rather than buffer size, which
compounds across a batch of multi-GB books uploaded back to back.

## Approach

Replace `ReadAllBytesAsync` + `ByteArrayContent` with `File.OpenRead` +
`StreamContent` at each of the three call sites. `StreamContent` has no 2 GB
ceiling and streams from disk in chunks.

Two alternatives were considered and rejected:

- **A shared `FileContent(path, mediaType)` helper.** The three call sites differ
  in part name, media type, and arity (`upload` loops over N files; the others take
  one), so the helper would mostly pass parameters through. Its main payoff is a
  buffer-size knob no call site wants to tune.
- **A pre-flight size check only.** The issue's own fallback: reject >2 GB up front
  with a clear message. Makes the limit discoverable before a long batch, fixes
  nothing. Dropped entirely — with streaming there is no limit to warn about.

## Design

### 1. Stream the file parts

`UploadService` gains a seam so the multipart body can be built without posting it:

```csharp
internal static MultipartFormDataContent BuildUploadContent(
    string libraryId, string folderId, string uploadTitle,
    string? author, string? series,
    IReadOnlyList<(string LocalPath, string UploadName)> files)
```

The sequence-prefixed title is still computed by the caller and passed in as
`uploadTitle`. Each file part becomes `new StreamContent(File.OpenRead(path))`.

`UploadAsync` then does `using var content = BuildUploadContent(...)`. Disposing
the `MultipartFormDataContent` cascades to its children, closing the `FileStream`s
— newly load-bearing, since a multi-file upload now holds N open handles where it
previously held none past the read. None of the three call sites disposes its
content today; all three will.

`BackupService.UploadAsync` and `CoversService.UploadFromFileAsync` get the same
swap plus `using`, with no extracted seam — they are single-file and single-part.

**Wire behavior is unchanged.** Part names stay as they are, and each part keeps
exactly the `Content-Type` it has today: none for upload parts and the cover part,
`application/octet-stream` for the backup part. Only the buffering changes.

A file deleted between argument parsing and upload throws `FileNotFoundException`
from `File.OpenRead`, as it does from `ReadAllBytesAsync` today. No change.

### 2. Backup upload timeout

`upload` already passes `Timeout.InfiniteTimeSpan`, so removing the size ceiling
leaves it unbounded as intended. `backup upload` passes `LongOperationTimeout`
(10 minutes), which becomes the next wall for a multi-GB backup — a 2.4 GB body
over a home link can exceed it.

`BackupService.UploadAsync` moves to `Timeout.InfiniteTimeSpan`. The other backup
operations (create, apply, download) keep their 10 minutes: their duration is set
by the server, not by an operator-chosen file size.

### 3. Unreplayable 401 retry

`AbsApiClient.EnsureSuccessOrHandleAuthAsync` handles a 401 by refreshing the token
and retrying with `new HttpRequestMessage(method, endpoint)` — **no body**. For a
multipart POST that re-sends an empty request, and `upload` synthesizes its success
receipt from the request rather than the response, so the operator is told the
upload succeeded.

Streaming makes such a body genuinely unreplayable: the stream has been consumed.
Rather than leave a silent wrong answer in place, the retry path learns to refuse:

```csharp
private async Task EnsureSuccessOrHandleAuthAsync(
    HttpResponseMessage response, HttpMethod method, string endpoint,
    string? permissionHint = null, string? notFoundHint = null,
    bool replayable = true)
```

Both `PostMultipartAsync` overloads pass `replayable: false`. On a 401 for a
non-replayable request the client refreshes the token anyway — leaving the config
usable so the operator's re-run succeeds immediately — then logs an error saying
the session expired mid-upload and the command must be re-run, and exits 2.

The path is hard to reach in practice: preflight refreshes anything expiring within
60 seconds and ABS access tokens last ~12 hours, so an upload would have to run for
12 hours. It is fixed because the failure mode is a false success, not because it
is likely.

### 4. Test

`tests/AbsCli.Tests/Services/UploadServiceContentTests.cs`:

1. Create a 2.1 GB sparse temp file — `FileStream.SetLength`, which allocates no
   blocks on ext4 (unit tests run on `ubuntu-latest` only).
2. Call `BuildUploadContent` with that one file.
3. Drain it: `await content.CopyToAsync(Stream.Null)`.
4. Assert the copy completes and `content.Headers.ContentLength` exceeds 2 GB.
5. Delete the temp file in a `finally`.

No HTTP, no server, no handler injection — the failure is client-side, in content
construction, so the test reproduces it exactly. Against today's code it fails with
the `IO_FileTooLong2GB` message from the issue.

## Out of scope

- **README, help text, `CHANGELOG.md`** — no verb, flag, or user-visible behavior
  changes, and the changelog belongs to the release process.
- **Per-file pre-flight size checks.** Superseded by the fix.
- **Upload progress reporting.** A multi-GB upload with no feedback is a real
  papercut, but it is a separate feature, not part of removing the ceiling.
