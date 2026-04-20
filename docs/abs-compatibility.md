# ABS Version Compatibility

## Compatibility Matrix

The CLI tracks which ABS versions it has been tested against. This is documented in
the project README and checked at runtime.

| abs-cli Version | ABS Versions  | Notes |
|----------------|--------------|-------|
| 0.1.x — 0.2.4   | 2.33.1        | Initial release, baseline API |
| 0.2.5+          | 2.33.1 — 2.33.2 | No API surface changes in 2.33.2 (maintenance release; internal refactors, image-endpoint clamping, cross-library bulk-download guard) |

This table grows as new ABS versions are tested. A single CLI version may support
multiple ABS versions if the API surface hasn't changed.

## Runtime Version Check

On first API call, the CLI reads the ABS server version from the login response
(`serverSettings.version`). If the version is outside the known-compatible range:

- **Newer than tested:** Warning to stderr: `Warning: ABS server version 2.35.0 has not been tested with this version of abs-cli. Proceed with caution.`
- **Older than supported:** Warning to stderr: `Warning: ABS server version 2.30.0 is older than the minimum supported version (2.33.1). Some features may not work.`

Warnings only — the CLI does not refuse to run. The user decides whether to proceed.

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
     server/auth/TokenManager.js \
     server/models/ \
     server/objects/
   ```
3. **Update DTOs** if response shapes changed
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
git clone --depth 1 --branch v2.33.1 https://github.com/advplyr/audiobookshelf.git temp/audiobookshelf
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
