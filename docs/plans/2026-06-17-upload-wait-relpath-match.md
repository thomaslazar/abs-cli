# upload --wait per-segment relPath matching — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `upload --wait` confirm long-titled uploads by matching the library item with tolerant per-segment relPath prefix matching instead of exact equality.

**Architecture:** Add a pure `RelPathMatcher` (per-segment prefix matching, unique-match selection). `UploadService.WaitForItemByPathAsync` uses it over the items it already polls. Delete the now-dead client-side truncation (`FilenameSanitizer.TruncateToByteLimit`). Reword the no-confident-match path so it no longer reads as data loss.

**Tech Stack:** C# / .NET, xUnit, NLog. Reference: `docs/specs/2026-06-17-upload-wait-relpath-match-design.md`.

---

## File Structure

- Create: `src/AbsCli/Api/RelPathMatcher.cs` — pure matching logic (per-segment prefix match + unique-match filter).
- Create: `tests/AbsCli.Tests/Api/RelPathMatcherTests.cs` — unit tests for the matcher.
- Modify: `src/AbsCli/Services/UploadService.cs` — `WaitForItemByPathAsync` uses the matcher; ambiguous ⇒ stop, no guess.
- Modify: `src/AbsCli/Api/FilenameSanitizer.cs` — delete `TruncateToByteLimit` + `MaxFilenameBytes`; update class doc.
- Modify: `tests/AbsCli.Tests/Api/FilenameSanitizerTests.cs` — remove the truncation test.
- Modify: `src/AbsCli/Commands/UploadCommand.cs` — reword no-match branch, drop stale comment, update help.
- Modify: `docker/smoke-test.sh` — add a long-title `--wait` success case.

No README change: no CLI verb or user-visible flag is added/removed/renamed.

---

### Task 1: Commit the approved spec

**Files:**
- Commit: `docs/specs/2026-06-17-upload-wait-relpath-match-design.md` (already on disk, untracked)

- [ ] **Step 1: Confirm branch and stage the spec**

Run: `git branch --show-current` (expect `fix/upload-wait-relpath-match`), then
```bash
git add docs/specs/2026-06-17-upload-wait-relpath-match-design.md
```

- [ ] **Step 2: Commit**

```bash
git commit -m "docs: add upload --wait relpath matching design"
```

---

### Task 2: RelPathMatcher — per-segment prefix matching (TDD)

**Files:**
- Create: `src/AbsCli/Api/RelPathMatcher.cs`
- Test: `tests/AbsCli.Tests/Api/RelPathMatcherTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/AbsCli.Tests/Api/RelPathMatcherTests.cs`:

```csharp
using AbsCli.Api;

namespace AbsCli.Tests.Api;

public class RelPathMatcherTests
{
    // A series segment long enough (>247 UTF-16 bytes ≈ >123 chars) to be
    // truncated by ABS. 130 'a's = 260 bytes.
    private static string Long(char c, int n) => new string(c, n);

    [Fact]
    public void IsMatch_ShortSegments_RequireExactEquality()
    {
        Assert.True(RelPathMatcher.IsMatch("Yone/Series/Title", "Yone/Series/Title"));
        Assert.False(RelPathMatcher.IsMatch("Yone/Series/Title", "Yone/Series/Other"));
    }

    [Fact]
    public void IsMatch_DifferentSegmentCount_NoMatch()
    {
        Assert.False(RelPathMatcher.IsMatch("Author/Title", "Author/Series/Title"));
    }

    [Fact]
    public void IsMatch_LongSegment_TruncatedTailsMatchOnPrefix()
    {
        // Predicted (untruncated) vs server (cut + re-appended ".«") share a
        // >100-char common prefix → same segment.
        var predicted = "Yone/" + Long('x', 130);
        var actual = "Yone/" + Long('x', 123) + ".«";
        Assert.True(RelPathMatcher.IsMatch(predicted, actual));
    }

    [Fact]
    public void IsMatch_LongSegments_DifferingBeforePrefixFloor_NoMatch()
    {
        // Two different long segments that diverge early (here at char 0).
        var predicted = "Yone/" + Long('x', 130);
        var actual = "Yone/" + Long('y', 130);
        Assert.False(RelPathMatcher.IsMatch(predicted, actual));
    }

    [Fact]
    public void IsMatch_LongSharedSeries_DistinguishesByTitleSequencePrefix()
    {
        var series = Long('s', 130);
        var predictedVol1 = $"Yone/{series}/1. - {Long('t', 130)}";
        var actualVol1 = $"Yone/{series[..123]}.«/1. - {Long('t', 123)}.«";
        var actualVol2 = $"Yone/{series[..123]}.«/2. - {Long('t', 123)}.«";
        Assert.True(RelPathMatcher.IsMatch(predictedVol1, actualVol1));
        Assert.False(RelPathMatcher.IsMatch(predictedVol1, actualVol2));
    }

    [Fact]
    public void Matches_ReturnsAllMatchingCandidates()
    {
        var predicted = "Yone/Series/Title";
        var candidates = new[] { "Other/X/Y", "Yone/Series/Title", "Yone/Series/Title" };
        Assert.Equal(2, RelPathMatcher.Matches(predicted, candidates).Count);
    }

    [Fact]
    public void Matches_NoCandidate_ReturnsEmpty()
    {
        var matches = RelPathMatcher.Matches("Yone/Series/Title", new[] { "A/B/C" });
        Assert.Empty(matches);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter RelPathMatcherTests`
Expected: FAIL — `RelPathMatcher` does not exist (compile error).

- [ ] **Step 3: Write the implementation**

Create `src/AbsCli/Api/RelPathMatcher.cs`:

```csharp
using System.Text;

namespace AbsCli.Api;

/// <summary>
/// Matches a CLI-predicted relPath against the relPath ABS actually stored.
/// The two diverge only in the truncated tail of long path segments (ABS
/// truncates each segment to ~255 UTF-16 bytes and re-appends what
/// <c>Path.extname</c> treats as an extension — see
/// <c>temp/audiobookshelf/server/utils/fileUtils.js</c>). We therefore compare
/// per segment: short segments must be equal; long (near-limit) segments may
/// differ in the tail provided they share a long common prefix.
/// </summary>
public static class RelPathMatcher
{
    // UTF-16 byte length at/above which a segment may have been truncated by
    // ABS. ABS's limit is 255; the margin covers the re-appended extension.
    private const int NearTruncationLimitBytes = 247;

    // Minimum common-prefix length (chars) for two differing near-limit
    // segments to count as the same segment truncated differently. Comfortably
    // below the ~127-char truncation point and above any realistic
    // distinguishing prefix (differing authors/series/sequences diverge far
    // earlier — e.g. the "N. -" title prefix diverges at char 0).
    private const int MinTruncatedSegmentPrefix = 100;

    /// <summary>True if a candidate relPath matches the predicted relPath.</summary>
    public static bool IsMatch(string predicted, string actual)
    {
        var p = predicted.Split('/');
        var a = actual.Split('/');
        if (p.Length != a.Length) return false;
        for (int i = 0; i < p.Length; i++)
        {
            if (!SegmentMatch(p[i], a[i])) return false;
        }
        return true;
    }

    /// <summary>All candidate relPaths that match <paramref name="predicted"/>.</summary>
    public static List<string> Matches(string predicted, IEnumerable<string> candidates)
    {
        var result = new List<string>();
        foreach (var c in candidates)
        {
            if (IsMatch(predicted, c)) result.Add(c);
        }
        return result;
    }

    private static bool SegmentMatch(string p, string a)
    {
        if (string.Equals(p, a, StringComparison.Ordinal)) return true;
        // Segments may only legitimately differ if one looks truncated;
        // otherwise they are simply different segments.
        if (!IsNearLimit(p) && !IsNearLimit(a)) return false;
        return CommonPrefixLength(p, a) >= MinTruncatedSegmentPrefix;
    }

    private static bool IsNearLimit(string s) =>
        Encoding.Unicode.GetByteCount(s) >= NearTruncationLimitBytes;

    private static int CommonPrefixLength(string x, string y)
    {
        int n = Math.Min(x.Length, y.Length);
        int i = 0;
        while (i < n && x[i] == y[i]) i++;
        return i;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter RelPathMatcherTests`
Expected: PASS (7 tests).

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Api/RelPathMatcher.cs tests/AbsCli.Tests/Api/RelPathMatcherTests.cs
git commit -m "feat: add per-segment relPath matcher for upload --wait"
```

---

### Task 3: Wire WaitForItemByPathAsync to the matcher

**Files:**
- Modify: `src/AbsCli/Services/UploadService.cs:75-112`

- [ ] **Step 1: Replace the method body**

In `src/AbsCli/Services/UploadService.cs`, replace the doc comment + method (lines 75-112) with:

```csharp
    /// <summary>
    /// Poll items list (sorted by addedAt desc) and return the just-uploaded
    /// item, matched against <paramref name="expectedRelPath"/> with tolerant
    /// per-segment prefix matching (see <see cref="RelPathMatcher"/>) because
    /// the CLI's predicted path and ABS's stored path diverge in the truncated
    /// tail of long segments. Returns null if nothing matches within the window
    /// OR if more than one recent item matches — an unresolvable collision the
    /// caller must not guess at.
    /// </summary>
    public async Task<LibraryItemMinified?> WaitForItemByPathAsync(
        string libraryId, string expectedRelPath,
        int timeoutSeconds = 120, int pollIntervalMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var query = HttpUtility.ParseQueryString("");
            query["sort"] = "addedAt";
            query["desc"] = "1";
            query["limit"] = "20";
            var url = ApiEndpoints.LibraryItems(libraryId) + "?" + query;
            var result = await _client.GetAsync(url, AppJsonContext.Default.PaginatedResponse);
            var candidates = new List<(string RelPath, JsonElement Element)>();
            foreach (var itemElement in result.Results)
            {
                if (!itemElement.TryGetProperty("relPath", out var relPathProp)) continue;
                var relPath = relPathProp.GetString();
                if (relPath == null) continue;
                // ABS stores relPath with a leading slash; strip it for comparison.
                candidates.Add((relPath.TrimStart('/'), itemElement));
            }
            var matches = RelPathMatcher.Matches(expectedRelPath, candidates.Select(c => c.RelPath));
            if (matches.Count == 1)
            {
                var element = candidates.First(c => c.RelPath == matches[0]).Element;
                return JsonSerializer.Deserialize(element.GetRawText(),
                    AppJsonContext.Default.LibraryItemMinified);
            }
            // matches.Count > 1 ⇒ ambiguous collision; polling won't resolve it.
            if (matches.Count > 1) return null;
            await Task.Delay(pollIntervalMs);
        }
        return null;
    }
```

- [ ] **Step 2: Add the System.Text.Json using**

At the top of `src/AbsCli/Services/UploadService.cs`, add `using System.Text.Json;` after the existing `using System.Web;` line (so `JsonElement` and `JsonSerializer` resolve unqualified).

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build AbsCli.sln`
Expected: Build succeeded, no errors.

- [ ] **Step 4: Run the full unit suite**

Run: `dotnet test AbsCli.sln`
Expected: PASS (existing tests + the new matcher tests).

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Services/UploadService.cs
git commit -m "fix: match upload --wait item by tolerant per-segment relPath"
```

---

### Task 4: Delete dead client-side truncation

**Files:**
- Modify: `src/AbsCli/Api/FilenameSanitizer.cs`
- Modify: `tests/AbsCli.Tests/Api/FilenameSanitizerTests.cs:105-113`

- [ ] **Step 1: Remove the truncation test**

In `tests/AbsCli.Tests/Api/FilenameSanitizerTests.cs`, delete the entire
`LongBasename_TruncatedToByteLimit` test (lines 105-113, the `[Fact]` and its method).

- [ ] **Step 2: Remove TruncateToByteLimit and the constant**

In `src/AbsCli/Api/FilenameSanitizer.cs`:
- Delete the `private const int MaxFilenameBytes = 255;` line.
- Change the end of `Sanitize` from `return TruncateToByteLimit(s);` to `return s;`.
- Delete the entire `private static string TruncateToByteLimit(string s) { ... }` method.

- [ ] **Step 3: Update the class doc comment**

In `src/AbsCli/Api/FilenameSanitizer.cs`, replace doc point 10 and the drift note. Change the numbered list item:

```
///  10. If basename in UTF-16 bytes &gt; 255, truncate the basename (ext preserved).
```

to:

```
/// Note: ABS truncates over-long path segments (~255 UTF-16 bytes) in a way the
/// CLI does not reproduce; upload --wait tolerates that tail divergence via
/// RelPathMatcher rather than predicting the exact truncated path.
```

And replace the "Drift risk / Smoke-test asserts… same relPath we predicted" lines with:

```
/// Drift risk: if ABS changes sanitise rules, predictions desync. The smoke
/// test asserts upload --wait still resolves the scanned item (exact match for
/// short titles, per-segment prefix match for long ones).
```

- [ ] **Step 4: Build and test**

Run: `dotnet build AbsCli.sln && dotnet test AbsCli.sln`
Expected: PASS. (`Sanitize` no longer truncates; no remaining test depends on truncation.)

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Api/FilenameSanitizer.cs tests/AbsCli.Tests/Api/FilenameSanitizerTests.cs
git commit -m "refactor: drop dead client-side path truncation"
```

---

### Task 5: Reword the no-match path and update help

**Files:**
- Modify: `src/AbsCli/Commands/UploadCommand.cs:63-70` (help) and `:134-154` (caller)

- [ ] **Step 1: Reword the no-confident-match branch**

In `src/AbsCli/Commands/UploadCommand.cs`, replace the `if (wait) { ... }` block body (the comment at lines 136-141, the `if (item == null)` error, lines 134-154) with:

```csharp
            if (wait)
            {
                var item = await service.WaitForItemByPathAsync(libraryId, receipt.RelPath);
                if (item == null)
                {
                    _logger.Warn(
                        "Upload succeeded, but the library item could not be auto-confirmed " +
                        "within the wait window — ABS may still be scanning, or the title is " +
                        "long enough that more than one recent item matched the predicted path. " +
                        "The files have landed; do NOT re-upload. Identify the item with: " +
                        "abs-cli items list --sort addedAt --desc --limit 5");
                    ConsoleOutput.WriteJson(receipt, AppJsonContext.Default.UploadReceipt);
                    Environment.Exit(1);
                    return 1;
                }
                ConsoleOutput.WriteJson(item, AppJsonContext.Default.LibraryItemMinified);
            }
```

- [ ] **Step 2: Update the "Output" help section**

In `src/AbsCli/Commands/UploadCommand.cs`, replace the `command.AddHelpSection("Output", ...)` call (lines 63-70) with:

```csharp
        command.AddHelpSection("Output",
            "Without --wait: returns an upload receipt. The files have landed but the",
            "item is not scanned yet. The receipt's relPath is a best-effort prediction;",
            "for very long titles ABS truncates path segments, so the real on-disk path",
            "is the server's, not the receipt's.",
            "",
            "With --wait: polls until the item appears and returns it as a",
            "LibraryItemMinified, matched by per-segment path prefix (tolerant of the",
            "server's title truncation). If it can't be auto-confirmed within ~2 min",
            "(still scanning, or a long title colliding with another recent item) the",
            "receipt is emitted with a warning and the command exits 1 — the files are",
            "uploaded regardless, so do not re-upload.");
```

- [ ] **Step 3: Build and test**

Run: `dotnet build AbsCli.sln && dotnet test AbsCli.sln`
Expected: PASS.

- [ ] **Step 4: Eyeball the help**

Run: `dotnet run --project src/AbsCli -- upload --help`
Expected: the "Output" section shows the new wording; no stray blank-line issues.

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/UploadCommand.cs
git commit -m "fix: reword upload --wait no-match as success-with-warning"
```

---

### Task 6: Smoke-test coverage for long titles

**Files:**
- Modify: `docker/smoke-test.sh` (after the drift cases, before `rm -rf "$UPLOAD_TMP"` at line ~698)

- [ ] **Step 1: Add a long-title --wait success case**

In `docker/smoke-test.sh`, immediately before the `rm -rf "$UPLOAD_TMP"` that follows the drift cases (~line 698), insert:

```bash
# --- Long-title --wait (issue #54) ---
# A title long enough that ABS truncates the path segment. We cannot predict
# the exact truncated relPath, so assert --wait still resolves the item (the
# per-segment matcher tolerates the tail divergence). Pre-fix this timed out.
LONG_TITLE="Ich täuschte Amnesie vor um meinen Verlobten loszuwerden da behauptete er Vor deinem Gedächtnisverlust warst du in mich verliebt und das ist die ganze lange Geschichte"
abs_login uploaduser uploadpass
long_out=$($CLI upload --title "$LONG_TITLE" --author "Long Title Author" \
    --folder "$FOLDER_ID" --wait --files "$UPLOAD_TMP/test.mp3" 2>&1)
long_rc=$?
abs_login root root
if [ $long_rc -eq 0 ] && json_get "$long_out" "['id']" >/dev/null 2>&1; then
    pass "long-title upload --wait resolves item (issue #54)"
    long_id=$(json_get "$long_out" "['id']")
    [ -n "$long_id" ] && $CLI items delete --id "$long_id" --hard >/dev/null 2>&1 || true
else
    fail "long-title upload --wait resolves item (issue #54)" "rc=$long_rc out=${long_out:0:300}"
fi
```

- [ ] **Step 2: Commit (smoke run happens in Task 7)**

```bash
git add docker/smoke-test.sh
git commit -m "test: smoke-cover long-title upload --wait (issue #54)"
```

---

### Task 7: Full verification

**Files:** none (verification only)

- [ ] **Step 1: Format check (as CI runs it)**

Run: `dotnet format AbsCli.sln --verify-no-changes`
Expected: no output, exit 0. If it fails, run `dotnet format AbsCli.sln`, commit the fix, re-run.

- [ ] **Step 2: Full unit suite**

Run: `dotnet test AbsCli.sln`
Expected: all pass.

- [ ] **Step 3: Bring up the docker stack and resolve the container IP**

```bash
cd docker && docker compose up -d && cd ..
ABS_IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
echo "ABS_IP=$ABS_IP"
```

- [ ] **Step 4: Seed if the stack is fresh**

Run: `ABS_URL=http://$ABS_IP:80 bash docker/seed.sh`
Expected: seed completes (skip only if the stack was already seeded this session).

- [ ] **Step 5: Run the smoke test**

Run: `ABS_URL=http://$ABS_IP:80 bash docker/smoke-test.sh`
Expected: all assertions pass — including the existing "sanitize drift" cases and the new "long-title upload --wait resolves item (issue #54)".

- [ ] **Step 6: Report results**

Confirm: format clean, unit tests green, smoke green (quote the relevant smoke lines). Only then is the work ready for a PR.
