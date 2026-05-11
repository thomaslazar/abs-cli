# Research: detect items already encoded as single `.m4b`

**Status:** Research notes for v0.5.0. Not yet a spec.

## Goal

Let agents and users determine whether a library item is **already** in
"single `.m4b`" shape — i.e. the `encode-m4b` task would be a no-op for
it. Useful for filtering candidates before a bulk encode run, or for
agent loops that should skip items that are already done.

## What "already m4b" means here

A book library item is a directory (or single file). Its
`media.audioFiles` array holds the per-file audio descriptors returned
by ABS's scanner. The relevant fields per audio file
(`temp/audiobookshelf/server/objects/files/AudioFile.js:50-68`):

- `metadata.path`, `metadata.ext`, `metadata.filename`
- `format` (uppercase short string from ffprobe — e.g. `"MP3"`,
  `"M4B"`, `"M4A"`, `"FLAC"`)
- `codec`, `mimeType`
- `exclude`, `error` (filter to `includedAudioFiles`)

`media.numAudioFiles` is also exposed on the book DTO
(`Book.js:664`) and counts every audio file regardless of exclude state;
`media.includedAudioFiles` is the filtered subset.

An item is "already a single `.m4b`" when, after filtering excluded
files, it has exactly one audio file and that file is m4b-shaped.

### Practical rule

```text
isSingleM4b(item) ==
    item.media.includedAudioFiles.length == 1
    AND (
        upper(item.media.includedAudioFiles[0].format)    in {"M4B", "M4A"}
        OR  lower(item.media.includedAudioFiles[0].metadata.ext) == ".m4b"
    )
```

Including `M4A` is intentional — `.m4b` files are technically MP4
containers and ffprobe sometimes reports the container format as
`M4A`. The file extension is the more reliable signal but format is the
fallback when extensions have been renamed.

## No new endpoint needed

This is a derived property of data already returned by
`GET /api/items/:id` and (in less detail) `GET /api/libraries/:id/items`.
We do not need a new ABS endpoint, and ABS does not expose a
"single-m4b filter" group for `items list`.

Cross-check: ABS itself uses the same kind of check in
`Book.js:283` (`includedAudioFiles.every((af) => supportedMimeTypes.includes(af.mimeType))`)
to gate ebook/audio detection — confirms reading `format`/`mimeType` off
`includedAudioFiles` is the canonical pattern.

## Proposed CLI shapes

Three viable options, leaning toward A:

### Option A: derived predicate, document in `--help`

No new commands. Document the derivation in `items list`/`items get`
help text plus an example in README so agents know how to compute it:

```bash
# items list piped to jq, surface only single-m4b items
abs-cli items list --limit 200 \
  | jq '.results[] | select(.media.numAudioFiles == 1
                            and (.media.audioFiles[0].format // ""
                                  | ascii_upcase
                                  | IN("M4B","M4A")))'
```

Pros: zero new surface area; agents already know how to compose with
`jq`/python. Matches the "thin pass-through" CLI principle.
Cons: every agent reinvents the predicate. Easy to get wrong (e.g.
forgetting the `M4A` fallback or the `exclude` filter).

### Option B: ABS-side filter group on `items list`

ABS supports custom filter groups via `items list --filter`. We'd need
to verify whether ABS exposes a filter like `audio-format=m4b` (it
doesn't, last we looked) — if not, this is server-side work that needs
a feature request upstream.

### Option C: derived synthetic flag in CLI output

Have `items list` and `items get` synthesize an `isSingleM4b` (or
`audioFormat`) field client-side and add a `--filter "single-m4b"`
client-side filter. Compromises the "JSON output exactly mirrors ABS"
rule we have in `architecture.md`.

**Leaning A.** A documented predicate in `--help` plus a worked
example in the `encode-m4b` `--help` ("skip items where ..."). Zero
new commands, agent-friendly, no architecture concessions.

## Open questions

- Should the predicate also consider `metadata.ext` for items where
  `format` is empty/unreliable? Probably yes (the rule above does).
- Should `excluded` audio files factor in? An item with one included
  `.m4b` plus excluded `.mp3` siblings is still effectively single-m4b
  — yes, filter to `includedAudioFiles` first.
- Naming: `isSingleM4b` reads cleanly but assumes only one m4b is
  ever desirable. A multi-disc audiobook could legitimately have
  multiple `.m4b` files (one per disc). Leave that case as
  "not single m4b" — it's accurate and matches what `encode-m4b`
  would do (collapse them into one).
