# Smoke-test Tidying Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** DRY up `docker/smoke-test.sh` by collapsing ~68 inline JSON-extraction one-liners into a `json_get` helper, funneling ~14 stray inline assertions through the existing `assert_json_expr`, and parameterizing the 4 near-identical cleanup traps into one `cleanup_items` helper — with zero CLI or test-behavior change.

**Architecture:** Pure bash refactor of a single ~2000-line test script. No new dependencies (stays on python3, jq rejected). Three independent helper consolidations. Because the harness has no unit tests, the acceptance gate is a full live-smoke run producing the **same PASS count** as the pre-change baseline; intermediate edits are gated by `bash -n` syntax checks.

**Tech Stack:** Bash, python3 (extraction/assertion guts), the AOT `abs-cli` binary, docker-compose ABS dev stack.

---

## Baseline & Conventions

- **Spec:** `docs/specs/2026-06-01-smoke-test-tidying-design.md`
- **Target file:** `docker/smoke-test.sh` (only this file changes; `seed.sh` is out of scope).
- **No behavior change:** same assertions, same PASS/FAIL count, same labels.
- **Running the smoke** (per `CLAUDE.md`): resolve the container IP and run against it; seed first if the stack is fresh.
  ```bash
  cd docker && docker compose up -d
  IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
  ABS_URL=http://$IP:80 bash docker/seed.sh          # only if freshly created
  ABS_URL=http://$IP:80 bash docker/smoke-test.sh
  ```
- **Existing helpers (do not duplicate):** `pass`, `fail`, `assert_json_key` (line ~43), `assert_json_expr` (line ~53), `abs_login` (line ~66).

---

## File Structure

- Modify: `docker/smoke-test.sh`
  - Helper block near top (after `assert_json_expr`, ~line 64): add `json_get` and `cleanup_items`.
  - ~68 extraction call sites scattered throughout: rewrite to `json_get`.
  - ~14 inline-assert call sites: rewrite to `assert_json_expr`.
  - 4 cleanup funcs (`delete_cleanup` ~303, `encode_cleanup` ~1245, `chapters_cleanup` ~1462, `embed_cleanup` ~1648): rewrite bodies to delegate to `cleanup_items`.

---

## Task 1: Capture baseline PASS count

**Files:** none (records a reference number for later comparison).

- [ ] **Step 1: Bring up the stack and run the current smoke unchanged**

```bash
cd /workspaces/abs-cli/docker && docker compose up -d
IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
# Seed only if the stack was freshly created:
ABS_URL=http://$IP:80 bash /workspaces/abs-cli/docker/seed.sh
ABS_URL=http://$IP:80 bash /workspaces/abs-cli/docker/smoke-test.sh | tee /tmp/smoke-baseline.txt
```

- [ ] **Step 2: Record the baseline tallies**

Run: `grep -E "PASS:|FAIL:" /tmp/smoke-baseline.txt | tail -1` (or read the summary line the script prints).
Expected: a final summary like `PASS=N FAIL=0`. Note `N` — this is the number every later run must match. **If FAIL is not 0 on the unmodified script, stop and report; the baseline must be green before refactoring.**

No commit (no file change).

---

## Task 2: Add the `json_get` helper

**Files:**
- Modify: `docker/smoke-test.sh` (insert after `assert_json_expr`, ~line 64)

- [ ] **Step 1: Insert the helper**

Add immediately after the closing `}` of `assert_json_expr`:

```bash
json_get() {
    # $1 = JSON string, $2 = python subscript chain (e.g. "['results'][0]['id']")
    echo "$1" | python3 -c "import sys,json; print(json.load(sys.stdin)$2)" 2>/dev/null
}
```

- [ ] **Step 2: Self-verify the helper in isolation**

Run:
```bash
source <(sed -n '/^json_get()/,/^}/p' /workspaces/abs-cli/docker/smoke-test.sh)
json_get '{"results":[{"id":"abc"}]}' "['results'][0]['id']"
json_get '{"media":{"metadata":{}}}' "['media']['metadata'].get('publisher')"
```
Expected: first prints `abc`; second prints `None` (matching the current inline behavior — `.get` of a missing key returns `None`, which is the existing semantics at e.g. line 276).

- [ ] **Step 3: Syntax check**

Run: `bash -n /workspaces/abs-cli/docker/smoke-test.sh`
Expected: no output, exit 0.

- [ ] **Step 4: Commit**

```bash
git add docker/smoke-test.sh
git commit -m "test: add json_get helper for smoke JSON extraction"
```

---

## Task 3: Convert extraction call sites to `json_get`

**Files:**
- Modify: `docker/smoke-test.sh` (all `print(json.load(sys.stdin)...)` extraction sites)

Convert mechanically. Each site of the form:
```bash
VAR=$(echo "$output" | python3 -c "import sys,json; print(json.load(sys.stdin)<CHAIN>)")
```
becomes:
```bash
VAR=$(json_get "$output" "<CHAIN>")
```
And piped forms `... | python3 -c "import sys,json; print(json.load(sys.stdin)<CHAIN>)"` where the input is a variable become `json_get "$var" "<CHAIN>"`. Preserve each chain **verbatim**, including `.get('id','')`, `.get('id', '')` (spacing differences are harmless), `.lstrip('/')`, etc.

**Do NOT convert** multi-line python blocks that do real logic (sorting, joining, comprehensions, payload building) — e.g. lines ~375, 386, 391 (`','.join(sorted(...))`), and the payload builders (~445, 470, 477). Those stay as inline python.

- [ ] **Step 1: List every candidate site**

Run: `grep -n "print(json.load(sys.stdin)" /workspaces/abs-cli/docker/smoke-test.sh`
Work top-to-bottom. For multi-line blocks (the line ends with `print(json.load(sys.stdin)` continuing onto further lines, or contains `join`/`sorted`/comprehension), skip and leave as-is.

- [ ] **Step 2: Convert the single-line subscript sites**

Edit each qualifying site to the `json_get` form above. Examples of the common shapes seen in this file:
- `$(echo "$output" | python3 -c "import sys,json; print(json.load(sys.stdin)['results'][0]['id'])")` → `$(json_get "$output" "['results'][0]['id']")`
- `$(echo "$out" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null)` → `$(json_get "$out" ".get('id','')")`
- `$(... | python3 -c "import sys,json; print(json.load(sys.stdin)['media']['metadata'].get('publisher'))")` → assign the piped input to a var or reuse the existing one, then `$(json_get "$thatvar" "['media']['metadata'].get('publisher')")`

(The `2>/dev/null` is already inside `json_get`, so drop the trailing one when present.)

- [ ] **Step 3: Verify no single-line subscript sites remain**

Run: `grep -nE "python3 -c \"import sys,json; print\(json\.load\(sys\.stdin\)" /workspaces/abs-cli/docker/smoke-test.sh`
Expected: zero matches (all single-line extractions now go through `json_get`). Any remaining hits must be the intentionally-skipped multi-line/logic blocks — eyeball each to confirm.

- [ ] **Step 4: Syntax check**

Run: `bash -n /workspaces/abs-cli/docker/smoke-test.sh`
Expected: no output, exit 0.

- [ ] **Step 5: Commit**

```bash
git add docker/smoke-test.sh
git commit -m "test: route smoke JSON extraction through json_get"
```

---

## Task 4: Add `cleanup_items` and rewire the 4 traps

**Files:**
- Modify: `docker/smoke-test.sh` (add helper after `json_get`; rewrite `delete_cleanup` ~303, `encode_cleanup` ~1245, `chapters_cleanup` ~1462, `embed_cleanup` ~1648)

- [ ] **Step 1: Insert the helper** (after `json_get`)

```bash
cleanup_items() {
    # $1 = tmp dir to remove (may be empty), remaining args = library item IDs to hard-delete
    abs_login root root
    local tmp="$1"; shift
    local id
    for id in "$@"; do
        [ -n "$id" ] && $CLI items delete --id "$id" --hard >/dev/null 2>&1 || true
    done
    [ -n "$tmp" ] && rm -rf "$tmp"
}
```

- [ ] **Step 2: Rewrite `delete_cleanup`** (~line 303)

Replace its body so it delegates (note: vars may be unset at definition time, so pass with `${VAR:-}`):
```bash
delete_cleanup() { cleanup_items "${DELETE_TMP:-}" "${DEL_ITEM_1:-}" "${DEL_ITEM_2:-}" "${DEL_ITEM_3:-}"; }
trap delete_cleanup EXIT
```

- [ ] **Step 3: Rewrite `encode_cleanup`** (~line 1245)

```bash
encode_cleanup() { cleanup_items "${ENCODE_TMP:-}" "${ENCODE_ITEM_ID:-}" "${CANCEL_TEST_ITEM_ID:-}"; }
trap encode_cleanup EXIT
```

- [ ] **Step 4: Rewrite `chapters_cleanup`** (~line 1462)

```bash
chapters_cleanup() { cleanup_items "${CHAPTERS_TMP:-}" "${CHAPTERS_ITEM_ID:-}"; }
trap chapters_cleanup EXIT
```

- [ ] **Step 5: Rewrite `embed_cleanup`** (~line 1648)

```bash
embed_cleanup() { cleanup_items "${EMBED_TMP:-}" "${EMBED_ITEM_ID:-}" "${EMBED_ITEM_ID_2:-}"; }
trap embed_cleanup EXIT
```

- [ ] **Step 6: Confirm `progress_cleanup` and `collections_cleanup` are untouched**

Run: `grep -nA4 "progress_cleanup()\|collections_cleanup()" /workspaces/abs-cli/docker/smoke-test.sh`
Expected: both still present with their original bodies (remove-progress / delete-collection). They are intentionally NOT converted.

- [ ] **Step 7: Syntax check**

Run: `bash -n /workspaces/abs-cli/docker/smoke-test.sh`
Expected: no output, exit 0.

- [ ] **Step 8: Commit**

```bash
git add docker/smoke-test.sh
git commit -m "test: collapse near-identical smoke cleanup traps into cleanup_items"
```

---

## Task 5: Funnel stray inline assertions through `assert_json_expr`

**Files:**
- Modify: `docker/smoke-test.sh` (~14 sites: lines ~269, 625, 1085, 1118, 1128, 1137, 1159, 1166, 1176, 1184, 1213, 1221, 1278, 1982)

`assert_json_expr "label" "<python-bool-expr>" "$json"` already exists and binds the parsed object to `d`. Convert inline `if echo "$json" | python3 -c "...; assert <EXPR>" 2>/dev/null; then pass ...; else fail ...; fi` blocks into a single `assert_json_expr` call, preserving the existing pass/fail label text exactly.

- [ ] **Step 1: Convert the clean (JSON-only) sites**

For sites whose `if` condition is *only* the python assert (e.g. lines 625, 1085, 1118, 1128, 1137, 1159, 1166, 1176, 1184, 1213, 1221, 1278, 1982), replace the whole if/pass/fail block with one call. Example — line ~1118:
```bash
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['success']==True and d['cover']" 2>/dev/null; then
    pass "<label>"
else
    fail "<label>" "<msg>"
fi
```
becomes:
```bash
assert_json_expr "<label>" "d['success']==True and d['cover']" "$output"
```
Keep the exact `<label>` string. (`assert_json_expr` already prints a generic failure detail + truncated response, matching the spirit of the old `fail` detail.) Use `d['key']` / `d.get('key')` forms unchanged — same Python the inline assert used.

- [ ] **Step 2: Restructure the combined-condition sites**

Three sites AND a non-JSON check with the assert in one `if`:
- Line ~269: `if [ $rc -eq 0 ] && echo "$output" | python3 -c "...; assert d.get('success') is True" ...`
- Line ~1685: `if [ "$exit_code" = "0" ] && echo "$output" | python3 -c "..." ...`
- Any grep-combined site surfaced by the grep in Step 3.

For these, split the bash check out and delegate the JSON half. Pattern:
```bash
if [ $rc -eq 0 ]; then
    assert_json_expr "<label>" "d.get('success') is True" "$output"
else
    fail "<label>" "non-zero exit ($rc)"
fi
```
Preserve the original label and the original non-JSON failure semantics.

- [ ] **Step 3: Verify no inline `assert` python remains**

Run: `grep -nE "python3 -c \"import sys,json; d=json.load\(sys.stdin\); assert" /workspaces/abs-cli/docker/smoke-test.sh`
Expected: zero matches outside the `assert_json_key`/`assert_json_expr` helper definitions themselves (lines ~44/~54). Confirm any hit is the helper body, not a call site.

- [ ] **Step 4: Syntax check**

Run: `bash -n /workspaces/abs-cli/docker/smoke-test.sh`
Expected: no output, exit 0.

- [ ] **Step 5: Commit**

```bash
git add docker/smoke-test.sh
git commit -m "test: route stray inline smoke asserts through assert_json_expr"
```

---

## Task 6: Full live-smoke verification (acceptance gate)

**Files:** none (verification only).

- [ ] **Step 1: Run the refactored smoke against the dev stack**

```bash
IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
ABS_URL=http://$IP:80 bash /workspaces/abs-cli/docker/smoke-test.sh | tee /tmp/smoke-after.txt
```

- [ ] **Step 2: Compare tallies to baseline**

Run: `grep -E "PASS=|FAIL=" /tmp/smoke-after.txt | tail -1`
Expected: **same PASS count as `/tmp/smoke-baseline.txt` from Task 1, FAIL=0.** If PASS dropped or any FAIL appeared, a conversion changed behavior — diff the failing section against `git show HEAD~4:docker/smoke-test.sh` and fix before proceeding. Do not declare done until counts match.

- [ ] **Step 3: Confirm clean tree**

Run: `git status --short`
Expected: clean (all changes already committed across Tasks 2–5).

---

## Self-Review (completed)

- **Spec coverage:** json_get (Tasks 2–3) ✓; cleanup_items for the 4 traps, progress/collections left alone (Task 4) ✓; stray-assert funnel (Task 5) ✓; payload builders & multi-line logic explicitly excluded (Task 3 Step 1) ✓; same-PASS-count live verification (Tasks 1 & 6) ✓.
- **Placeholders:** `<label>`/`<msg>`/`<CHAIN>`/`<EXPR>` are deliberate stand-ins for per-site text copied verbatim from the existing script, not unfilled TODOs.
- **Consistency:** helper names `json_get` / `cleanup_items` and the existing `assert_json_expr` signature `(label, expr, json)` are used identically everywhere.
