# Research: toggle ebook primary status

**Status:** Research notes for v0.5.0. Not yet a spec.

## Goal

Let agents and users flip which ebook file is the "primary" for a
library item that holds more than one ebook format (e.g. an `.epub` and
a `.pdf` of the same book). The ABS web UI exposes this under the
ebook-files table: each non-primary ebook row has a button that swaps
primary/supplementary status. This is currently unreachable from the
CLI — `items update` writes against `media`, not `libraryFiles`, and
there's no generic field for "primary file".

Audio files have no equivalent. ABS treats audio inside an audiobook as
an ordered track sequence, not a primary/alternates set; the
`isSupplementary` flag and the `:fileid/status` endpoint are
ebook-only. Two versions of the same audiobook would live as separate
library items.

## ABS data model

Primary ebook is stored as a single `ebookFile` object on the `Book`
model (`temp/audiobookshelf/server/models/Book.js:116`):

```
ebookFile: {
  ino,            // matches a libraryFile entry
  ebookFormat,    // "epub" / "pdf" / ...
  metadata: { filename, path, size, mtimeMs, ctimeMs, birthtimeMs },
  addedAt, updatedAt
}
```

Non-primary ebook files live in the item's `libraryFiles` array with
`isSupplementary: true`
(`temp/audiobookshelf/server/objects/files/LibraryFile.js:10`). The
primary ebook's matching `libraryFile` entry has
`isSupplementary: false`. Scanner code (`BookScanner.js:533-536`)
keeps this in sync: on rescan, exactly the file whose `ino` matches
`media.ebookFile.ino` is marked non-supplementary, every other ebook
file is marked supplementary.

In `items get` JSON the `ino` of every ebook file is already exposed
under `libraryFiles[].ino` — no extra lookup needed to obtain the
`:fileid` argument.

## ABS endpoint

```
PATCH /api/items/:id/ebook/:fileid/status
```

Source: `temp/audiobookshelf/server/routers/ApiRouter.js:128`,
controller `LibraryItemController.updateEbookFileStatus`
(`LibraryItemController.js:1111-1158`).

`:fileid` is the file's `ino`, resolved by
`LibraryItemController.middleware` via
`req.libraryItem.getLibraryFileWithIno(req.params.fileid)`
(`LibraryItemController.js:1177`).

### Permission

`canUpdate` — *not* admin-only. Lower bar than the v0.5.0 tools
endpoints (`encode-m4b`, `embed-metadata`), same bar as `items update`
and `items chapters set`. The middleware enforces this generically for
all `PATCH`/`POST` routes on this controller
(`LibraryItemController.js:1189`).

### Validation

- Item must exist (`media` non-null) → 404.
- User must have access (`checkCanAccessLibraryItem`) → 403.
- File must exist on the item by `ino` → 404 ("Library file
  '<ino>' does not exist for library item").
- Item must be a book (`req.libraryItem.isBook`) → 400.
- File must be an ebook file (`req.libraryFile.isEBookFile`) → 400
  ("Invalid ebook file id").

No check that more than one ebook file exists — the endpoint will
happily unset the only primary ebook on a single-ebook item, leaving it
with zero primary ebooks. See "Sharp edges" below.

### Request

- Body: empty.
- No query params.

The action is encoded entirely by which file you target.

### Response

`res.sendStatus(200)` — empty body. Side effects:

- `media.ebookFile` is rewritten (set or `null`) and saved
  (`LibraryItemController.js:1140-1142`).
- All ebook entries in `libraryFiles` have their `isSupplementary`
  flag rewritten so exactly the new primary (if any) is non-supplementary
  (`LibraryItemController.js:1144-1149`).
- `isMissing` is recomputed against `media.hasMediaFiles`
  (`LibraryItemController.js:1152`).
- A socket `item_updated` event is broadcast
  (`LibraryItemController.js:1156`).

To observe the result, refetch the item (`items get --id <id>`) and
inspect `media.ebookFile` and `libraryFiles[].isSupplementary`.

## Toggle semantics (the important part)

The endpoint name says "status" — that's literal. It does not take a
"make this one primary" intent. It looks at the targeted file's current
`isSupplementary` value and flips it
(`LibraryItemController.js:1129-1140`):

| Target file currently | Effect of one PATCH call |
|---|---|
| Supplementary (`isSupplementary: true`) | Becomes primary. Any previously-primary ebook is auto-demoted to supplementary in the same call. |
| Primary (`isSupplementary: false`) | Becomes supplementary. `media.ebookFile` is set to `null`. **No other file is auto-promoted** — the item is left with no primary ebook. |

So to swap primary from epub → pdf, you target the pdf with a single
call. The previously-primary epub is demoted automatically. You do
*not* call it twice.

But: if you accidentally call it on the file that's currently primary,
you end up with both files supplementary and no primary ebook. The
docs / help text need to call this out loudly. Recovery is one more
call on whichever file you want primary.

## Sharp edges

1. **Toggling the primary unsets without auto-promoting.** Already
   covered above. Top item to document in `--help`.
2. **Works on single-ebook items too.** No "must have ≥2 ebook files"
   guard. You can unset the only primary on an item that has just one
   ebook file. The file stays on disk (this endpoint never touches
   files); ABS just stops considering it the primary.
3. **`isSupplementary` is reasserted by the scanner.** `BookScanner`
   re-syncs `libraryFiles[].isSupplementary` against
   `media.ebookFile.ino` on every rescan
   (`BookScanner.js:533-536`). So the source of truth across rescans
   is `media.ebookFile`. Setting it to `null` *does* survive a rescan
   — the scanner won't pick a new primary on its own once one has
   been unset. (The scanner sets one initially only when no
   `ebookFile` exists yet, in `BookScanner.setEbookFile` /
   `ScanLibrary.scanLibraryItem` — not in the supplementary-sync
   pass.)
4. **No effect on item progress, position, or playback** — primary
   ebook selection only affects which file ABS serves to the ebook
   reader. The audio side is untouched.

## Proposed CLI shape

```
abs-cli items toggle-ebook-status --id <itemId> --ino <fileIno>
```

Argument names match `items get`'s output keys:
- `--id` — library item id.
- `--ino` — the `ino` from `libraryFiles[].ino`. Stringified, since
  ABS treats it as a string in URLs.

Output: print the post-call item JSON (single fresh `items get` after
the PATCH), so the caller can see `media.ebookFile` and
`libraryFiles[].isSupplementary` reflect the new state. Pattern
matches `items update` which echoes the updated item.

### Naming

`toggle-ebook-status` over `set-primary-ebook`:

- `set-primary-ebook <itemId> <fileIno>` reads as a setter, but the
  endpoint isn't one. Calling it twice on the same file no-ops you
  back to start; calling it on the current primary unsets. A `set-*`
  verb that doesn't reliably "set" is misleading.
- `toggle-ebook-status` matches the server's wording in the controller
  doc comment (`LibraryItemController.js:1102-1106`) and the toggle
  semantics.
- Trade-off: less discoverable. Someone looking for "primary ebook"
  via `--help | grep` won't find it on the verb. Mitigation: lead the
  `--help` blurb with "Toggle which ebook file is primary (epub vs.
  pdf, etc.)" so search by intent still works.

### Help-text checklist (per `feedback_help_documents_caveats`)

`--help` for this command must include, not bury in spec docs:
- The toggle semantics (calling twice on the same file is a no-op).
- The "no auto-promotion when unsetting the primary" sharp edge,
  with the worked example: epub primary → call on epub → no primary
  → call on pdf → pdf primary.
- That `--ino` comes from `items get` → `libraryFiles[].ino`.
- That the endpoint requires `canUpdate`, not admin (so it composes
  with the existing agent token scope).
- That audio files have no equivalent — for users who'll try the
  obvious "what about my audio files?" follow-up.

## Open questions

- Should we also offer a non-toggle helper that pre-checks state and
  refuses to no-op (e.g. `items set-primary-ebook` that errors if the
  target is already primary, rather than unsetting)? This would need
  a `items get` round-trip first. Probably not worth it — adds a
  pre-fetch which violates the thin-pass-through rule
  (`feedback_cli_thin_passthrough`). A clear `--help` blurb is
  cheaper and equally safe.
- Should we expose a separate `--unset` flag to make the unset path
  explicit (`items toggle-ebook-status --id ... --unset` requires
  the file to be currently primary)? Same concern: client-side state
  enforcement when the server has none. Lean: no.
- Does the existing `items get` output already include
  `libraryFiles[].isEBookFile` / `libraryFiles[].fileType` so a caller
  can identify which files are ebooks? Worth confirming during
  implementation — if not, document the filename-extension fallback
  in `--help`.
