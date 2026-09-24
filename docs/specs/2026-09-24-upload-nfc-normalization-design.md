# Upload NFC normalization under invariant globalization — design

**Date:** 2026-09-24
**Status:** approved
**Issue:** [#97](https://github.com/thomaslazar/abs-cli/issues/97)

## Problem

`upload --wait` exits 1 ("could not be auto-confirmed") when `--title`,
`--author` or `--series` contains decomposed Unicode (NFD, e.g. `o` + U+0308).
The upload itself succeeds.

ABS's `sanitizeFilename` (`server/utils/fileUtils.js:366`) NFC-normalizes each
path segment. `FilenameSanitizer.Sanitize` mirrors this with
`string.Normalize(NormalizationForm.FormC)` — but `AbsCli.csproj` sets
`InvariantGlobalization=true`, under which .NET 10's `Normalize` is a silent
no-op for non-ASCII input (verified: `"Löwe".Normalize(FormC)` returns
the 5-char input unchanged). The predicted relPath therefore stays NFD, never
matches the server's NFC relPath, and the receipt's `relPath` is wrong too.

The issue's proposed fix (normalize in `RelPathMatcher`, normalize flags before
sending) cannot work for the same reason: `Normalize` does nothing.

## Decision

Keep `InvariantGlobalization` (self-contained AOT binary, no `libicu`
dependency on Linux) and ship a small managed NFC composer.

**Fidelity: pragmatic, not full UAX #15.** Canonical composition of
starter + mark pairs, singleton mappings and Hangul — no canonical reordering
(no combining-class data). Divergence from ICU is limited to inputs whose
combining marks are not already in canonical order; the consequence is today's
exit-1 warning, not wrong data. NFD produced by macOS and catalogue sources
(DNB) is canonically ordered, so it is covered.

Rejected:
- Dropping `InvariantGlobalization` — adds a runtime `libicu` requirement on
  Linux (breaks minimal containers) and changes culture behavior CLI-wide.
- Full UAX #15 — needs vendored `UnicodeData.txt` combining classes (.NET has
  no public ccc API) for a case no real input source produces.
- Accent-folding in the matcher — leaves the receipt's `relPath` NFD.

## Design

### `src/AbsCli/Api/UnicodeNfc.cs`

`public static string Compose(string s)`

- Fast path: return `s` unchanged (same instance) if every char is < U+0300
  (nothing below U+0300 is a composition second element or has a singleton
  mapping).
- Iterate by rune (surrogate pairs handled). Each rune is first mapped through
  the singleton table (e.g. U+212B ANGSTROM SIGN → U+00C5).
- Track the last starter's position in the output. For each following rune
  try, in order: pair table `(starter, rune) → composite`, then Hangul
  L+V → LV and LV+T → LVT arithmetic. On success the starter is replaced in
  place and the rune is dropped.
- On failure the rune is appended. If it is a combining mark
  (`UnicodeCategory` NonSpacingMark / SpacingCombiningMark / EnclosingMark)
  the starter stays; later marks may still compose with it (no ccc-based
  blocking). Otherwise the rune becomes the new starter. Hangul composition
  only applies when the starter is the last output rune (UAX #15 adjacency).
- Known divergences (all need marks that do not compose with the base):
  out-of-canonical-order marks (`a` + U+0301 + U+0323), and an uncomposable
  mark blocking a later mark of equal combining class (`a` + U+0310 +
  U+0301 — ICU keeps it decomposed, `Compose` yields `á` + U+0310).
  Skipping blocking is the better guess: the common failing-mark case is a
  lower-class below-mark followed by an above-mark, which ICU does compose.

### `src/AbsCli/Api/UnicodeNfc.g.cs` (generated, checked in)

- `Pairs`: sorted `(ulong key = first << 21 | second, int composite)` arrays,
  binary-searched.
- `Singletons`: sorted `(int from, string to)` arrays, binary-searched.
- Plain arrays, no reflection, no dictionaries built at startup — AOT-safe.

### `tools/GenerateNfcTables`

Console tool (not invariant, so ICU is live) iterating all scalar values
U+0000–U+10FFFF (skipping surrogates):

- **Pair:** let `D = NFD(c)` with ≥ 2 runes, `p = NFC(D without its last
  rune)`. If `p` is a single rune and `NFC(p + last) == c`, add
  `(p, last) → c`. Covers two-rune decompositions (`o` + U+0308 → `ö`) and
  stacked ones via their intermediate (`ạ` + U+0302 → `ậ`). Hangul syllables
  are skipped (handled arithmetically); composition exclusions fall out
  naturally because `NFC(...) != c` for them.
- **Singleton:** `NFC(c) != c` and `c` is not produced by a pair → entry
  `c → NFC(c)`.
- Asserts every pair's second element and every singleton key is ≥ U+0300
  (backs `Compose`'s fast path).
- Writes `UnicodeNfc.g.cs`. Run manually; **not** wired into the build —
  Unicode stability means the tables rarely change, and a build step would
  make output depend on the host's ICU version. Added to `AbsCli.sln` under
  the existing `tools` folder.

### Wiring

`FilenameSanitizer.Sanitize` replaces `filename.Normalize(FormC)` with
`UnicodeNfc.Compose(filename)`. This fixes the predicted receipt `relPath`,
the per-segment 255-byte truncation budget, and the `--wait` match.

Not changed:
- `RelPathMatcher` — the server side is already NFC.
- Flag values sent to ABS — thin pass-through; ABS normalizes server-side.
- Help text — behavior is now simply correct.

## Testing

Test project is not invariant, so ICU `Normalize` is the oracle.

- Exhaustive: for every scalar value `c`,
  `UnicodeNfc.Compose(c.Normalize(FormD)) == c.Normalize(FormC)`, and
  `UnicodeNfc.Compose(c) == c.Normalize(FormC)`.
- Corpus: German, French, Vietnamese (stacked but ordered marks), Hangul
  jamo sequences, the DNB title from #97 — compare against ICU.
- `Sanitize` regression: NFD `Die Löwin von Neetha` → NFC.
- Documented gaps: the two divergence examples above — assert the known
  outputs so a future change is deliberate.
- Self-test (AOT binary, invariant mode — the only place the original bug
  reproduces): `Sanitize` of the #97 NFD title yields NFC.
- Smoke: new `run_drift_case` with an NFD title (via `printf`) expecting the
  NFC relPath.
