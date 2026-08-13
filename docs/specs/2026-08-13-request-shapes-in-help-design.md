# Request shapes in help — design

**Date:** 2026-08-13
**Status:** approved, not yet implemented

## Problem

Sixteen subcommands accept a JSON body via `--input <file>` or `--stdin`, and none
of them document its shape anywhere an agent can reach. `--help-full` exists and is
described as *"Show full help including response-shape blocks"* — responses only.
The request side has no equivalent, so the only shape information is whatever fits
in the option's own description, and those descriptions are inconsistent, sometimes
empty of content, and in one case wrong:

| Description today | Command | Problem |
|---|---|---|
| `JSON file path` | `items chapters set`, `collections create` | says nothing |
| `JSON file with libraryItemIds` | `items batch-update` | **wrong** — ABS wants a bare array |
| `JSON file with array body` | `items batch-update-progress` | shape unstated |
| `JSON file with books array (\`{"books":["lid",...]}\`)` | 8 sites | duplicated eight times |

`docs/input-output.md` compounds it: *"Update JSON format matches what the ABS API
expects … No custom schema."* That is a pointer to somewhere else, which for the
CLI's primary consumer — an AI agent with no ABS source access — is a dead end. It
surfaced from the `grimoire-cli` side for exactly that reason.

`--help` is the primary interface for the agents that consume this CLI. A command
whose body shape is undocumented cannot be called correctly without out-of-band
knowledge.

## Goals

- An agent reading `<command> --help-full` can construct a valid request body
  without consulting ABS docs or source.
- The documented shape is derived from code, not from hand-maintained prose.
- No change to what goes over the wire.

## Non-goals

- **Rejecting unknown fields.** Never. ABS ignores what it does not recognise, and
  mirroring server policy client-side is ruled out by the thin pass-through
  principle in `CLAUDE.md`.
- **Re-serialising the body.** See "Validation rule" — the original bytes are what
  gets sent.
- **Inventing requirements ABS does not have.** Where the endpoint requires
  nothing, the CLI requires nothing.
- New verbs or flags. The README Commands table is unaffected.

## Design

### 1. Where shapes surface

A new per-command `Request shape` section, registered by a new
`AddRequestExample<T>()` beside the existing `AddResponseExample<T>()`, tagged
`IsShape: true` so it renders under `--help-full` and stays out of plain `--help`.

The discoverability hint at `HelpExtensions.cs:170` currently reads *"Run
--help-full to see response shape(s)."* and must cover both.

### 2. Request types

One request type per JSON-input command, in `Models/`, registered on
`AppJsonContext`. This generalises a pattern the codebase already has rather than
inventing one: `ChaptersSetRequest` is already documented as *"Also serves as the
input shape: `--input` / `--stdin` payloads deserialize into this type, and a
deserialization failure is the CLI's only pre-HTTP validation."*

The sixteen commands fall into **three** classes, and the distinction drives both
what the type documents and whether the original bytes can be forwarded.

**Class A — pass-through.** The body reaches ABS verbatim, so the type documents the
ABS wire shape:

| Command | Body | Source |
|---|---|---|
| `items update` | free-form media object | `LibraryItemController.updateMedia` |
| `items batch-update` | **array** of objects, each with `id` | `LibraryItemController.js:632-640` |
| `items batch-get` | `{libraryItemIds: [...]}` | `LibraryItemController.batchGet` |
| `items batch-delete` | `{libraryItemIds: [...]}` | `LibraryItemController.batchDelete` |
| `items batch-update-progress` | array of progress objects | `MeController.batchUpdateMediaProgress` |
| `items chapters set` | `{chapters: [{title, start, end}]}` | `LibraryItemController.js:910` |
| `items batch-embed-metadata` | `{libraryItemIds: [...]}` | `ToolsController.batchEmbedMetadata` |
| `collections create` | `{libraryId, name, description?, books?}` | `CollectionController.js:32-48` |
| `collections reorder` | `{books: [...]}` | `CollectionController.js:170` |
| `collections batch-add` / `batch-remove` | `{books: [...]}` | `CollectionController.js:320,382` |
| `libraries reorder` | array of `{id, newOrder}` | `LibraryController.reorder` |

**Class B — CLI-transformed.** The CLI owns an input contract that deliberately
differs from the wire shape, so the type documents the **CLI's** contract, and the
body is constructed rather than forwarded:

| Command | CLI input | Wire body ABS receives |
|---|---|---|
| `playlists reorder` | `{books: ["li_a", …]}` | `{items: [{libraryItemId}]}` |
| `playlists batch-add` | `{books: ["li_a", …]}` | `{items: [{libraryItemId}]}` |
| `playlists batch-remove` | `{books: ["li_a", …]}` | `{items: [{libraryItemId}]}` |

`PlaylistsCommand.ReadBooksAsync` parses `{books:[…]}` into a `List<string>` and
`PlaylistsService.SerializeItems` emits ABS's `items` array. This keeps playlists
uniform with collections at the CLI surface. It is already shipped and is **not**
changed here — but it means the existing `{"books":[...]}` help text is correct for
these commands and must not be "fixed" to match ABS.

**Class C — free-form.** `items update` only; listed in class A above because it is
forwarded verbatim, but called out separately because ABS validates nothing.

`playlists create` is flag-based rather than JSON-input and so is out of scope
despite appearing near these; its body is built from options.

Per-field detail is enumerated in the implementation plan, taken from the named
controller method for class A and from the existing CLI parser for class B.

### 3. Validation rule

Parse the input into the type. On `JsonException`, emit one error line and exit 1
(`docs/input-output.md`: "1 — general error (bad arguments…)"). For **class A**
commands, then **send the original string** — never a re-serialisation. **Class B**
commands necessarily construct their body and keep doing exactly what they do today;
the rule there is that the transformation is not changed, only documented.

Where ABS itself requires structure, check it after parsing. That is not mirroring
policy; the thin pass-through principle says required inputs should match the
endpoint's required inputs. Concretely: `batch-update` needs a non-empty array whose
elements carry unique `id`s; `chapters set` needs `chapters` entries with `title`,
`start`, `end`; `collections create` and `playlists create` need `name` and
`libraryId`.

Where ABS requires nothing — `items update`, whose handler is
`const mediaPayload = req.body` with no validation — the parse is a **syntax check
only**. The type documents which fields ABS acts on; it does not gate them.

**Forward-the-original-bytes fixes an existing bug.** `chapters set` today
re-serialises (`ItemsCommand.cs:696`):

```csharp
var canonical = JsonSerializer.Serialize(parsed, AppJsonContext.Default.ChaptersSetRequest);
```

`ChapterWriteEntry.End` is a non-nullable `double`, so a payload missing `end`
deserialises to `0`, canonicalises to `"end": 0`, and ABS accepts it — writing a
zero-length chapter where forwarding the original would have earned a 400. That
command moves onto the original bytes with the rest.

### 4. Generator changes

`tools/GenerateResponseExamples` already emits samples for every
`[JsonSerializable]` type on `AppJsonContext`, skipping request bodies by name
(`Program.cs:112-122`). Request types come off that exclusion list.

Two adjustments:

- **Nullable reference properties render as `null` today** (`"serverVersion": null`).
  That teaches an agent the key but not the type. For request documentation they
  must render as `<string>`, matching how non-nullable strings already render.
- The generated class is named `ResponseExamples`. It will hold both, so it becomes
  `JsonExamples` — two call sites in `HelpExtensions.cs`.

### 5. Help-text cleanup

This is the largest mechanical part. Thirty-two option descriptions carry shape
information today — 16 `--input` and 16 `--stdin`. Once the shape block exists they
are duplication in the terse section, which the help rules forbid ("Don't state
what's already visible"). They collapse to one uniform pair:

- `--input` → *"JSON file with the request body (see --help-full)"*
- `--stdin` → *"Read the request body from stdin"*

Two consequences worth noting: this fixes `items batch-update`'s factually wrong
description, and it removes the same `{"books":[...]}` string repeated across eight
sites in two files.

**Class B needs care here.** For the three `playlists` commands the `{"books":[...]}`
text describes the CLI's own input contract, not a wire shape, and it is correct.
Their generated shape block must document that contract, and the cleanup must not
replace it with ABS's `items` shape — doing so would make the help actively wrong
and any agent following it would get a 400.

**Deliberately kept:** the inline `AddExamples` bodies, e.g.
`echo '{"metadata":{"title":"New Title"}}' | abs-cli items update --stdin`. Those
are executable one-liners, not shape documentation; they appear in plain `--help`
where the shape block does not, and they are the fastest thing for an agent to copy.

There is no separate body-describing help *section* anywhere — the shape
information lives only in those option descriptions.

### 6. Docs

`docs/input-output.md`'s "Input for Updates" section stops saying the format
"matches what the ABS API expects … No custom schema" and instead states that each
command documents its body under `--help-full`.

### 6b. The ABS-bump checklist must cover the request interface

This is load-bearing, not a nicety. Once shapes ship in help text, an agent
*trusts* them: a shape that has silently drifted is worse than no shape, because it
produces confidently wrong payloads. The version-bump process is the only place that
can catch drift (see the Testing limitation below), so `docs/abs-compatibility.md`'s
Handling ABS Updates section needs two changes.

**First, step 3 is response-only today:**

> 3. **Update DTOs** if response shapes changed

It becomes explicit about both directions and about the generated samples:

> 3. **Update DTOs if request *or* response shapes changed.** For every command with
>    a documented request shape, re-read its controller method and confirm the type's
>    fields still match — required keys, types, and nesting. Update the type, rebuild
>    (which regenerates the samples), and spot-check the affected `--help-full`
>    output. A drifted request shape is a correctness bug, not stale docs: agents
>    construct payloads from it.

**Second, the controller diff list at `docs/abs-compatibility.md:49-56` is already
incomplete** — independent of this change. It names `LibraryItemController`,
`LibraryController`, `SeriesController`, `AuthorController`, `SearchController`,
`TokenManager`, `models/` and `objects/`, but the CLI also calls endpoints served by:

| Controller | Endpoints the CLI uses |
|---|---|
| `CollectionController` | `api/collections`, `…/:id/book`, `…/:id/batch/add`, `…/batch/remove` |
| `PlaylistController` | `api/playlists`, `…/:id/item`, `…/:id/batch/add`, `…/batch/remove` |
| `MeController` | `api/me`, `api/me/progress/batch/update` |
| `ToolsController` | `api/tools/item/:id/encode-m4b`, embed-metadata |
| `MiscController` | `api/tags`, `api/genres`, `api/narrators`, `api/authorize` |
| `CacheController` | `api/cache/purge`, `api/cache/items/purge` |
| `BackupController` | `api/backups` and its sub-routes |

Those are added to the diff command, so a bump actually surfaces changes to the
endpoints whose shapes we now publish. Missing them is how a request shape drifts
without anyone noticing.

## Testing

- **Unit:** per validation gate — valid body, malformed JSON, structurally invalid
  (empty array, missing `id`, chapter missing `end`). Help rendering: the request
  shape appears under `--help-full` and not under plain `--help`. For class B, a test
  asserting the documented `{books:[…]}` contract still produces ABS's `items` wire
  body, so the transformation cannot drift from its own documentation.
- **`self-test`:** round-trip each new type, per the AOT convention.
- **Smoke:** unchanged. See the limitation below.

**Limitation, stated rather than papered over:** generated samples use placeholders
(`"<string>"`, `0`), so they are not executable bodies and the smoke suite cannot
verify that a documented shape is actually accepted by ABS. Nothing in CI can catch
a request shape that has drifted from ABS — the only mechanism is the version-bump
review, which is why section 6b treats the checklist as part of this change rather
than as documentation housekeeping. An authored-example approach would have been executable and
therefore verifiable, but would have put the shape in string literals that nothing
checks — the trade was made deliberately in favour of generated samples.

## Rejected alternatives

- **Deserialise and send the re-serialised object.** Would give real validation,
  but any field the model failed to cover gets silently dropped on the round trip.
  For `items update`, whose body is a large nested media object, that is a
  data-loss-shaped failure with a 200 response and nothing to notice. This is also
  why the existing `chapters set` canonicalisation is being removed rather than
  copied.
- **Doc-only types with no runtime role.** Rejected as decoration: a type shaped
  like a model that nothing exercises invites the next reader to assume it is wired
  up, and the compiler cannot verify field names it never binds to the wire. The
  chosen design keeps every type either validating (15 commands) or feeding the
  generator through an explicit `AddRequestExample<T>()` call (`items update`).
- **Authored JSON samples instead of types.** Executable and therefore
  smoke-verifiable, but two mechanisms, and the most-used command's shape becomes a
  literal nothing checks.
- **Requiring at least one recognised field on free-form bodies.** Would catch an
  empty `{}` before a pointless round trip, but ABS accepts it, so refusing locally
  is the CLI inventing a rule.

## Portability to grimoire-cli

The gap was reported from that side, and the same asymmetry exists there. What
ports is the shape of the solution: a request-shape help section fed by types, the
parse-then-forward-original rule, and the cleanup of per-option prose. What does
not port is the type list, which is specific to the ABS endpoints.
