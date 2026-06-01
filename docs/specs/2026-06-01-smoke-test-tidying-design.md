# Smoke-test tidying — design

**Roadmap item:** 0.6.0 "Smoke-test tidying" (`docs/roadmap.md`).
**Date:** 2026-06-01
**Scope:** Pure test-harness DRY in `docker/smoke-test.sh`. No CLI behavior change, no
new dependency, same PASS count before/after.

## Background

`docker/smoke-test.sh` (~2000 lines) contains 88 `python3 -c` invocations and 6
per-section `*_cleanup()` EXIT traps. The repetition is the target — not the choice
of python. jq was considered and **rejected**: both python3 and jq ship by default
in the dev container and on `ubuntu-latest` (where the smoke runs in CI,
`build.yml:62`), so jq adds no portability win, only churn. Staying with python keeps
the diff small and avoids hand-translating ~40 assertion expressions.

The 88 python calls break down as:

| Category | Count | State today |
|---|---|---|
| Inside `assert_json_key` / `assert_json_expr` | 2 | already DRY |
| Inline assertions that bypass those helpers | ~14 | inline `python3 -c "...; assert ..."` |
| Extractions (`json_get` candidates) | ~68 | scattered one-liners |
| Payload builders (construct request JSON) | ~4 | irreducible, out of scope |

`seed.sh`'s 8 python calls are **out of scope**.

## Changes

### 1. `json_get` helper

Replace the ~68 inline extraction one-liners with one helper near the existing
`assert_*` helpers at the top of the file:

```bash
json_get() {  # $1 json, $2 python subscript chain
    echo "$1" | python3 -c "import sys,json; print(json.load(sys.stdin)$2)" 2>/dev/null
}
```

Call sites collapse from:

```bash
FIRST_ITEM_ID=$(echo "$output" | python3 -c "import sys,json; print(json.load(sys.stdin)['results'][0]['id'])")
```

to:

```bash
FIRST_ITEM_ID=$(json_get "$output" "['results'][0]['id']")
```

The path argument is a literal python subscript chain, so `.get(...)` forms and
nested access still work unchanged:
`json_get "$output" "['media']['metadata'].get('publisher')"`.

Multi-line extraction blocks (e.g. lines 375, 386, 391) that compute a derived value
(sorting, joining, comprehensions) are NOT plain subscripts — convert the simple ones
to `json_get`; leave any that need real python logic as inline blocks.

### 2. Funnel stray inline assertions into `assert_json_expr`

The ~14 sites that inline `python3 -c "...; assert <expr>"` should call the existing
`assert_json_expr "label" "<expr>" "$json"` helper instead.

- Clean cases (e.g. lines 1118, 1128, 1278) become a single `assert_json_expr` call.
- The ~3 cases that combine a JSON assert with a non-JSON check in one `if`
  (line 269 `$rc -eq 0 && …`, line 1685 `$exit_code`, grep combos) get a small
  restructure: do the non-JSON check in bash, the JSON part via `assert_json_expr`.

End state: every JSON assertion flows through `assert_json_key` or `assert_json_expr`.

### 3. `cleanup_items` helper

Collapse the 4 near-identical traps (`delete_cleanup`, `encode_cleanup`,
`chapters_cleanup`, `embed_cleanup`) — all of which do "login root → hard-delete N
items → rm tmp dir":

```bash
cleanup_items() {  # $1 tmp dir (may be empty), rest = item IDs
    abs_login root root
    local tmp="$1"; shift
    for id in "$@"; do
        [ -n "$id" ] && $CLI items delete --id "$id" --hard >/dev/null 2>&1 || true
    done
    [ -n "$tmp" ] && rm -rf "$tmp"
}
```

Each section keeps its own thin named trap that delegates, e.g.:

```bash
encode_cleanup() { cleanup_items "$ENCODE_TMP" "$ENCODE_ITEM_ID" "$CANCEL_TEST_ITEM_ID"; }
trap encode_cleanup EXIT
```

Keeping a named per-section wrapper preserves readable `trap` lines and lets each
section reference its own variables at trap-fire time.

**Leave alone:** `progress_cleanup` (removes progress, no login/tmp/delete) and
`collections_cleanup` (deletes a collection, no login/tmp) — genuinely different.

## Out of scope

- The ~4 payload-builder python blocks (lines 445, 470, 477) — they construct request
  bodies, not extract values. Leave as-is.
- `seed.sh` python calls.
- Any jq migration.

## Verification

- `docker/smoke-test.sh` against the dev compose stack must stay all-green, with the
  **same PASS count** before and after. This is the primary acceptance test — unit
  tests do not exercise the harness.
- Run per `CLAUDE.md` pre-PR instructions (container IP, seed if fresh).
