# ABS Version Compatibility

## Compatibility Matrix

The CLI tracks which ABS versions it has been tested against. This is documented in
the project README and checked at runtime.

| abs-cli Version | ABS Versions  | Notes |
|----------------|--------------|-------|
| 0.1.x — 0.2.4   | 2.33.1        | Initial release, baseline API |
| 0.2.5 — 0.4.0   | 2.33.1 — 2.33.2 | No API surface changes in 2.33.2 (maintenance release; internal refactors, image-endpoint clamping, cross-library bulk-download guard) |
| 0.5.0 — 1.0.4   | 2.33.1 — 2.36.0 | v2.34 closes the upstream batch-update `canUpdate` gap (now returns 403 for users without update permission); v2.35 adds a 60s server-side refresh-token grace period (CLI behavior unchanged). v2.35.1 is a patch with internal-only fixes (BookAuthor dedup guard, case-insensitive user lookup). v2.36.0 adds five additive `/api/me/*` routes (auth sessions, bulk progress, bookmarks) that abs-cli does not consume; makes expanded item/book/podcast JSON a strict superset of minified, so expanded responses gain `numFiles`, `numTracks`, `numAudioFiles`, `numChapters`, `ebookFormat` and `numEpisodes` — all additive; the media-level fields pass through untyped, and the CLI now models the new top-level `numFiles` on its expanded-item DTO (it was previously dropped), emitting it only when the server sends it, so output against pre-2.36.0 servers is unchanged. `numEpisodes` stays unmodelled — the podcast DTO is a books-only placeholder; widens the refresh-token grace period to 10 minutes and makes it configurable; and stops accepting refresh tokens as bearer credentials (abs-cli only ever presents one at `POST /auth/refresh`, so unaffected). No response-shape removals, endpoint changes, or permission changes for endpoints abs-cli consumes. |
| 1.1.0+          | 2.34.0 — 2.36.0 | **Floor raised from 2.33.1 to 2.34.0.** Verified empirically by running the full smoke suite against both: 2.33.1 gave 336/338, failing only `items batch-update as readonlyuser hits 'update' permission denial` — on 2.33.1 `LibraryItemController.batchUpdate` has no `canUpdate` check, so a read-only user succeeds with `{"success": true, "updates": 1}`. The `update` permission this CLI documents for that verb is therefore not enforced below 2.34.0, which is the gap the row above already described. 2.34.0 gives 338/338. All routes abs-cli calls do exist at 2.33.1 — this is an upstream permission fix drawing the line, not CLI drift. |

This table grows as new ABS versions are tested. A single CLI version may support
multiple ABS versions if the API surface hasn't changed.

## Runtime Version Check

The CLI reads the server version at most once every 24 hours, from the
unauthenticated `GET /status` endpoint, on whichever command runs first after the
window lapses. `login` uses the version already in its own response instead of
probing. The result is stored in `~/.abs-cli/config.json` as `lastVersionCheck`
and `lastServerVersion`, and both are shown by `abs-cli config get`.

Binding the check to login would be wrong: these are self-hosted servers, so the
version changes when the image is pulled — an event involving no login — and tokens
persist and refresh, so an install can run for months without logging in.

If the version is outside the known-compatible range, a warning goes to stderr:

- **Newer than tested:** `abs-cli 1.0.3 was tested up to ABS 2.36.0; this server is 2.38.0. Check for a newer abs-cli.`
- **Older than supported:** `ABS server version 2.30.0 is older than the minimum supported version (2.34.0). Some features may not work.`

When the version changed since the last check, the warning says so:
`This server moved from ABS 2.36.0 to 2.38.0 since the last check.`

Warnings only — the CLI does not refuse to run. The user decides whether to proceed.
A failed probe is silent and does not update the timestamp, so the next invocation
retries; a diagnostic must never be the thing that fails the command.

Bugs found in ABS itself while testing against it are recorded in
[abs-upstream-bugs.md](abs-upstream-bugs.md), along with any workaround this repo
carries for them. Check it when bumping a version — a fix upstream may let a
workaround be dropped.

## Handling ABS Updates

When a new ABS version is released:

1. **Update the reference source:** `cd temp/audiobookshelf && git fetch && git checkout v<new_version>`
2. **Diff the controllers:** Compare the controllers used by abs-cli against the
   previous version to identify API changes:
   ```bash
   git diff v2.33.1..v2.34.0 -- server/controllers/LibraryItemController.js \
     server/controllers/LibraryController.js \
     server/controllers/SeriesController.js \
     server/controllers/AuthorController.js \
     server/controllers/SearchController.js \
     server/controllers/CollectionController.js \
     server/controllers/PlaylistController.js \
     server/controllers/MeController.js \
     server/controllers/ToolsController.js \
     server/controllers/MiscController.js \
     server/controllers/CacheController.js \
     server/controllers/BackupController.js \
     server/auth/TokenManager.js \
     server/models/ \
     server/objects/
   ```
3. **Update DTOs if request *or* response shapes changed.** For every command with a
   documented request shape, re-read its controller method and confirm the type's
   fields still match — required keys, types, and nesting. Update the type, rebuild
   (which regenerates the samples), and spot-check the affected `--help-full` output.
   A drifted request shape is a correctness bug, not stale docs: agents construct
   payloads from it, and nothing in CI can catch the drift.
   Nullability is load-bearing here — a non-nullable field renders `"<string>"` and a
   nullable one `"<string|null>"`, so getting it wrong misinforms agents about whether
   a field is required.
4. **Run integration tests** against the new ABS version (update the Docker image tag
   in docker-compose)
5. **Update the compatibility matrix** in README and in this doc
6. **Tag a release** if changes were needed

## Automated Compatibility Releases

When a new ABS version is released:

1. A CI workflow (manually triggered or on a schedule) runs the full integration test
   suite against the new ABS Docker image
2. If all tests pass with no code changes needed:
   - Update the compatibility matrix
   - Cut a minor CLI release (e.g., 0.1.1 → 0.1.2) that declares support for the
     new ABS version
   - The runtime version check in the new release accepts the new version without warnings
3. If tests fail:
   - The workflow reports which tests broke
   - A developer investigates, updates DTOs or commands as needed, and cuts a release
     with the fixes

This keeps the CLI's supported version range current without manual tracking. Users
running `abs-cli` get a clear signal: if your CLI version is up to date, your ABS
version is supported.

## ABS API Reference

The Audiobookshelf API has no OpenAPI spec. The community docs at api.audiobookshelf.org
are self-admittedly outdated and no longer maintained.

**The ABS source code is the single source of truth for API behavior.**

### Setting Up the Reference

Clone the ABS repository into `temp/` for local reference:

```bash
git clone --depth 1 https://github.com/advplyr/audiobookshelf.git temp/audiobookshelf
```

This directory is gitignored. Re-clone after a fresh checkout if needed. Pin to a
specific tag to match your target ABS version:

```bash
# Supported version is set in src/AbsCli/Api/AbsApiClient.cs (MinSupportedVersion / MaxTestedVersion)
git clone --depth 1 --branch v2.36.0 https://github.com/advplyr/audiobookshelf.git temp/audiobookshelf
```

### Building DTOs from Source

All C# DTO models must be derived from the ABS source code, not from the API docs.
When creating or updating a model:

1. Find the relevant controller in `temp/audiobookshelf/server/controllers/`
2. Trace the response object through the controller → model → `toJSON()` methods
3. Cross-reference with `temp/audiobookshelf/server/models/` for Sequelize model definitions
4. Verify field names, types, and nullability against the actual JavaScript objects

This is critical because the API docs are incomplete and sometimes wrong. The source
code is what your ABS instance actually runs.

### Key Source Files

| Area | File | Purpose |
|------|------|---------|
| Items | `server/controllers/LibraryItemController.js` | Item CRUD and batch operations |
| Libraries | `server/controllers/LibraryController.js` | Library endpoints, item listing, filtering |
| Series | `server/controllers/SeriesController.js` | Series endpoints |
| Authors | `server/controllers/AuthorController.js` | Author endpoints |
| Search | `server/controllers/SearchController.js` | Search endpoint |
| Auth | `server/auth/TokenManager.js` | Token generation, expiry, refresh flow |
| Models | `server/models/` | Sequelize model definitions (field names, types) |
| JSON shapes | `server/objects/` | `toJSON()` / `toOldJSON()` methods define API response shapes |

### Known API Behaviors

- Filter format: `filter=group.base64(value)` — value is base64-encoded, then URL-encoded
- Search returns a different metadata shape than item list (nested `authors` array vs flat `authorName` string)
- Batch update endpoint: `PATCH /api/items/batch/update`
- Pagination: `limit` and `page` (0-indexed)
- Sorting: `sort=field.path` with `desc=0|1`
