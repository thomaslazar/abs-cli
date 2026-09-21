# `items batch-update` media payload wrapper — design

**Date:** 2026-09-21
**Status:** approved, not yet implemented
**Issue:** [#93](https://github.com/thomaslazar/abs-cli/issues/93)
**Related:** [#94](https://github.com/thomaslazar/abs-cli/issues/94) (closed as not planned; its one useful guard is folded in here)

## Problem

`items batch-update` documents a request body that Audiobookshelf cannot parse, and
sending it does not produce a 400 — it takes the server down.

`abs-cli items batch-update --help-full` prints an entry shaped
`{id, metadata, tags}`. ABS reads the media payload from a `mediaPayload` key:

- `LibraryItemController.js:665` — `const mediaPayload = updatePayload.mediaPayload`
- `LibraryItemController.js:675` — `Array.isArray(mediaPayload.metadata?.series)`

With the documented shape `mediaPayload` is `undefined`, and the `?.` at `:675` sits
one level too deep, so `undefined.metadata` throws a TypeError. (A podcast item dies
earlier still, at `:668`.) `Book.updateFromRequest` is itself defensive —
`Book.js:371` is `if (!payload) return false` — so the crash is in the controller,
after that call returns.

The throw happens in an async route handler with nothing catching it, so it becomes
an unhandled rejection, and `Server.js:214-216` handles those with `process.exit(1)`.
Under `restart: unless-stopped` the server then enters a crash loop for as long as a
client keeps retrying.

Correct body:

```json
[{ "id": "...", "mediaPayload": { "metadata": { "explicit": true } } }]
```

### How this happened, and what it is not

Not a v1.1.1 regression. The type shipped in **v1.1.0** (commit `9bc4cb8`,
2026-08-13, "document the items batch request shapes").

The cause is visible in the code. `ItemsBatchUpdateEntry`'s doc comment cites
`LibraryItemController.js:632-640` — the array-and-unique-id validation block.
Whoever wrote it modelled that faithfully and stopped 25 lines before `:665`, where
the payload is actually unwrapped.

### The CLI never sent a wrong shape on its own

`PrepareBatchUpdateBody` returns `jsonBody` unchanged, and no
`JsonUnmappedMemberHandling.Disallow` is configured, so unknown members are ignored.
A caller who already writes the correct body passes validation and succeeds today —
`docker/smoke-test.sh:337` has been doing exactly that since it was written, which is
why the suite never caught this. The harm reaches users only through the help text.
There is therefore no behavior change for correct bodies and nothing to migrate.

## Design

### 1. Wrap the entry payload

`src/AbsCli/Models/RequestShapes.cs`:

```csharp
public class ItemsBatchUpdateEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("mediaPayload")]
    public ItemMediaUpdateRequest? MediaPayload { get; set; }
}
```

`ItemMediaUpdateRequest` is reused rather than a parallel type being introduced,
because that is precisely what ABS means: `:673` calls
`libraryItem.media.updateFromRequest(mediaPayload)`, the same call the single-item
route makes with its whole body. The batch entry is "an id plus the single-item
body", and the two shapes cannot drift apart.

The MSBuild `RegenerateResponseExamples` target rewrites
`src/AbsCli/Commands/ResponseExamples.g.cs` when `Models/*.cs` changes, so the
`--help-full` block corrects itself; it is not hand-edited.

The type's doc comment is corrected to cite `:665` and `:675` — the lines that
actually determine the shape — instead of the validation block that misled it.

### 2. Guard against a missing payload

`PrepareBatchUpdateBody` rejects any entry whose `mediaPayload` is absent or null,
before anything is sent. All-or-nothing, naming the offending index:

```
batch-update entry 4: "mediaPayload" is missing (nothing to update)
```

Exit 1, matching every other bad-body path in this command
(`ItemsCommand.cs:313`) and the codebase at large, which uses only 1 and 2.

An explicit `null` counts as missing: `null.metadata` throws at `:675` exactly as
`undefined.metadata` does.

**Emptiness is deliberately not checked.** `mediaPayload: {}` is a verified harmless
no-op — `updateFromRequest` returns false, then `{}.metadata?.series` is `undefined`
and the series branch is skipped, HTTP 200. Refusing a caller's deliberate no-op
would be client-side policy of the kind the thin pass-through rule exists to prevent.

This guard is why #94 was closed: it is the one piece of that proposal worth keeping,
and it ships here rather than separately. The callers most likely to send the broken
body are those with scripts written against v1.1.0's incorrect help output;
correcting the help does not correct their scripts, so the shape fix and the guard
are one change.

### 3. Help text

The generated shape renders only under `--help-full`. A reader of plain `--help`,
having just used `items update` — where the media payload is the body — sees nothing
about the wrapper. One terse line on the command says each entry wraps its payload,
unlike `items update`. This is the pitfall that produced #93, so it qualifies as a
behavior warning under the cross-reference rule rather than as prose restating the
sample.

### 4. Record the upstream crash

A second entry in `docs/abs-upstream-bugs.md`, in the format of the existing
backup-apply one: a body without the wrapper is an unhandled rejection at `:675`,
which `Server.js:214-216` turns into `process.exit(1)`. Workaround column: the
client-side guard from change 2.

This is the more valuable of the two entries in that file — a deterministic,
reproducible server kill, where the backup-apply race is intermittent.

### 5. Tests

**Unit** (`tests/AbsCli.Tests/Commands/`, against `PrepareBatchUpdateBody`):

- rejects an entry with no `mediaPayload`, naming its index
- rejects an explicit `"mediaPayload": null`
- names the *first* offending index when several entries are bad
- accepts a valid body and returns the bytes unchanged, including fields the type
  does not model
- the existing array/unique-id checks still hold

**Generated-shape test:** assert the sample for `List<ItemsBatchUpdateEntry>`
contains `mediaPayload`. The help block is the artifact that caused this issue, so it
is pinned directly rather than only via the type.

**Smoke:** send a payload-less body with `ABS_SERVER` pointed at an unused port,
asserting the guard's message and a non-zero exit.

Pointing it at a dead address rather than the live stack is deliberate and is the
stronger test. `PrepareBatchUpdateBody` runs at `ItemsCommand.cs:309`, before
`CommandHelper.BuildClient()` at `:317`, so:

- guard working → exits 1 with the guard's message, no client ever built
- guard regressed → a connection error against a port with nothing on it

It proves the body never reaches *any* wire, which is the property that matters, and
a regression fails an assertion instead of killing the container and cascading into
every later section of the suite.

## Out of scope

- **The sibling batch verbs.** Audited, none unwrap a per-entry payload:
  `batch-update-progress` passes each entry straight to
  `createUpdateMediaProgressFromPayload` and skips bad ones (`MeController.js:297-312`);
  `batch-delete`, `batch-get` and `batch-embed-metadata` all take
  `{"libraryItemIds":[...]}`. `items batch-update` is the only such route.
- **A distinct exit code for bad input.** #94 proposed 3; the codebase uses only 1
  and 2, and this command's existing bad-body path uses 1. Introducing a third code
  is a CLI-wide convention change, separate from this fix.
- **Fixing the upstream crash.** Not this project's to fix; recorded in change 4.
