# upload --wait: per-segment relPath matching

**Issue:** [#54](https://github.com/thomaslazar/abs-cli/issues/54) — `upload --wait`
false-negative on long titles; predicted `relPath` diverges from the server's
path truncation.

## Problem

`upload --wait` polls `items list` and matches the just-uploaded item by **exact
`relPath` equality** (`UploadService.WaitForItemByPathAsync`). ABS's upload
endpoint returns HTTP 200 with an **empty body** (`MiscController.handleUpload`),
so the CLI cannot learn the real path or id — it *predicts* the path by
re-implementing ABS's `sanitizeFilename` client-side (`FilenameSanitizer`).

The prediction diverges from the server for titles long enough to be truncated.
Confirmed against the v2.35.1 checkout (`server/utils/fileUtils.js:360-421`):
ABS runs `Path.extname()` on **every** directory part, truncates the *basename*
to `255 − len(ext)` UTF-16 bytes, `.trim()`s it, and **re-appends the
extension**:

```js
const ext = Path.extname(sanitized)          // ".«" for a title ending "verliebt.«"
const basename = Path.basename(sanitized, ext)
...
trimmedBasename = trimmedBasename.trim()
sanitized = trimmedBasename + ext            // basename cut, ext re-appended
```

The CLI (`FilenameSanitizer.TruncateToByteLimit`) instead assumes "no extension
here" and does a plain byte-prefix cut — so `…warst du in mich.«` (server) vs
`…warst du in mich ve` (CLI). Exact equality can therefore never match a
truncated-title item.

**Impact (from the issue):** false-negative on every long-titled item; scary
exit-1 "did not appear" wording that reads as data loss; the post-`--wait`
`items update` metadata step is skipped; re-running on the "failure" duplicates
the item.

The stored `relPath` is exactly the upload directory path — the scanner takes it
verbatim (`Watcher.js`, `LibraryScanner.js`), no metadata-driven renaming. So the
divergence is purely CLI-vs-server truncation drift.

## Decision

Replicating the server's truncation exactly (issue suggestion #3) is fragile and
version-coupled — it is the strategy the CLI already bet on and lost, and the
server's `Path.extname`-on-directories behaviour has further pathologies
(e.g. a sequence-only dot can make `extname` swallow the title). Matching by the
server's item id (suggestion #1) is impossible: the response has no body.

**Chosen: tolerant per-segment prefix matching** (issue suggestion #2). The key
property: client and server consume the *same leading characters in the same
order*; they only ever diverge in the truncated **tail** of a long segment.
Per-segment comparison lets long leading segments (author, series) prefix-match
while a distinguishing later segment — the `N. -` sequence prefix on the title —
still separates volumes of the same series.

## Design

### Matching (`UploadService.WaitForItemByPathAsync`)

Replace the exact-equality check with per-segment matching over the recent items
already polled (`sort=addedAt&desc=1&limit=20`):

1. Split both the candidate's `relPath` (leading `/` stripped, as today) and the
   predicted `relPath` on `/`. Different segment count ⇒ not a match.
2. Each segment must **segment-prefix-match**:
   - equal ⇒ match; else
   - the segment is only allowed to differ if it is **near the truncation limit**
     (UTF-16 byte length ≥ ~247 on either side) — short segments require exact
     equality, which rejects unrelated items naturally (a different author fails
     on segment 0); and
   - the common prefix length must be ≥ `MinTruncatedSegmentPrefix` (start at
     **100** chars — comfortably below the ~127-char truncation point, and above
     any realistic distinguishing prefix, since differing books/series/sequences
     diverge far earlier).
3. Collect all recent items that match. Return the item **iff exactly one
   matches**. Zero or ≥2 ⇒ no confident match (return null).

**Invariant: never return an item we are not certain of.** A tie (≥2 matches)
means a genuine, unresolvable collision — same author, same/no series, and titles
identical up to truncation, which the *server* writes into the **same folder**
anyway. We warn rather than guess.

### Caller (`UploadCommand`)

- Confident match → print the `LibraryItemMinified`, exit 0 (unchanged success
  path; now reached for long titles too).
- No confident match → emit the receipt and a **reworded** message: the files
  uploaded **successfully**, the item was not auto-confirmed (still scanning, or
  multiple long-title candidates matched), identify it via
  `abs-cli items list --sort addedAt --desc --limit 5`, and **do not re-upload**.
  Exit 1 (preserves the "stdout has an item ⇔ exit 0" contract for scripts) but
  without the data-loss wording.
- Remove the now-inaccurate "Match by relPath — deterministic" comment block.

### Cleanup

`FilenameSanitizer.TruncateToByteLimit` and `MaxFilenameBytes` become dead weight:
the per-segment prefix match depends only on the first ~100 chars, which
truncation does not alter. Delete them (and the misleading "ext preserved"
doc point). `PredictRelPath`/`Sanitize` stay — they produce the match key. The
predicted segment is then untruncated, which is fine (it only feeds the prefix
comparison, and the `--wait` success path returns the server's real `relPath`
anyway).

### Help text (`UploadCommand`, "Output" section)

Update to reflect the new behaviour: `--wait` matches by per-segment path prefix;
for very long titles the exact on-disk path is the server's, not the receipt's
prediction; on no-confident-match the files are still uploaded — re-run
`items list` rather than re-uploading.

## Known limitation (explicitly out of scope)

Two uploads that the server sanitizes to a **byte-identical folder** (same long
author, same/no series, titles diverging only past the truncation point, no
distinguishing sequence) cannot be told apart — the server has already merged
them into one directory. This also covers the server's own `Path.extname`
pathologies (e.g. a sequence-only dot). These degrade to the safe
"couldn't auto-confirm" path; never to a wrong match. Not worked around.

## Testing

- TDD the matcher with concrete cases, pinning the `MinTruncatedSegmentPrefix`
  and near-limit constants:
  - normal short title: exact match unchanged;
  - long title, single upload: matches despite tail divergence;
  - two volumes of one long-named series (`1. -` / `2. -`): each resolves to its
    own item;
  - unrelated recent item (different author): rejected;
  - byte-identical collision: two matches ⇒ no confident match (warn).
- Update/remove unit tests covering `TruncateToByteLimit`.
- Update `docker/smoke-test.sh`: the existing exact-round-trip `relPath`
  assertion changes to the per-segment match; add a long-title upload case so the
  regression is covered in the live HTTP path. Extend `docker/seed.sh` if needed.
