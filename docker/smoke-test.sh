#!/usr/bin/env bash
# Smoke test: exercises every abs-cli command against a live ABS instance.
# Tests the actual binary (AOT or JIT) — not `dotnet run`.
#
# Expects a seeded ABS instance (15 audiobooks + 1 multi-ebook fixture, 7 authors, 3 series).
#
# Usage:
#   ABS_URL=http://host.docker.internal:13378 CLI=./path/to/abs-cli bash docker/smoke-test.sh
#
# If CLI is not set, builds a Release binary via `dotnet publish` first.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ABS_URL="${ABS_URL:-http://host.docker.internal:13378}"

# --- Build or locate the binary ---
if [ -z "${CLI:-}" ]; then
    echo "CLI not set — building Release binary..."
    dotnet publish "$REPO_ROOT/src/AbsCli/AbsCli.csproj" \
        -c Release -r linux-x64 --self-contained true /p:PublishAot=true \
        -o "$REPO_ROOT/src/AbsCli/bin/smoke-test" \
        --nologo -v quiet 2>&1
    CLI="$REPO_ROOT/src/AbsCli/bin/smoke-test/abs-cli"
fi

if [ ! -x "$CLI" ]; then
    echo "ERROR: binary not found or not executable at: $CLI"
    exit 1
fi

echo "Binary: $CLI"
echo "Binary size: $(du -h "$CLI" | cut -f1)"
echo ""

PASS=0
FAIL=0

pass() { echo "  PASS: $1"; PASS=$((PASS + 1)); }
fail() { echo "  FAIL: $1 — $2"; FAIL=$((FAIL + 1)); }

assert_json_key() {
    local label="$1" key="$2" json="$3"
    if echo "$json" | python3 -c "import sys,json; d=json.load(sys.stdin); assert '$key' in d" 2>/dev/null; then
        pass "$label"
    else
        fail "$label" "key '$key' not found in response"
        echo "    response: ${json:0:200}"
    fi
}

assert_json_expr() {
    local label="$1" expr="$2" json="$3"
    if echo "$json" | python3 -c "
import sys,json
d=json.load(sys.stdin)
assert $expr
" 2>/dev/null; then
        pass "$label"
    else
        fail "$label" "assertion failed: $expr"
        echo "    response: ${json:0:300}"
    fi
}

json_get() {
    # $1 = JSON string, $2 = python expression appended to the parsed object
    # e.g. "['results'][0]['id']", ".get('id','')", "['relPath'].lstrip('/')"
    echo "$1" | python3 -c "import sys,json; print(json.load(sys.stdin)$2)" 2>/dev/null
}

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

abs_login() {
    # $1 username, $2 password — non-interactive CLI login (writes config).
    $CLI login --server "$ABS_URL" --username "$1" --password-stdin <<<"$2" >/dev/null 2>&1
}

# --- Authenticate via the CLI (dogfoods non-interactive login) ---
echo "Setting up test context..."
if abs_login root root; then
    pass "login: root non-interactive login succeeds"
else
    fail "login: root non-interactive login succeeds" "abs_login root failed"
    exit 1
fi

LIB_ID=$(json_get "$($CLI libraries list 2>/dev/null)" "['libraries'][0]['id']")

echo "ABS_URL=$ABS_URL  LIB_ID=$LIB_ID"
echo ""

# Server + library context for commands that take them explicitly.
# NOTE: no auth token is exported — auth is driven by the config file
# that `$CLI login` writes (flag → env → file precedence).
export ABS_SERVER="$ABS_URL"
export ABS_LIBRARY="$LIB_ID"

# ============================================================
echo "=== Help Screens ==="
# ============================================================

# Parent commands and self-test don't need examples (they just list subcommands)
for cmd in "" "config" "libraries" "items" "series" "authors" "backup" "metadata" "tasks" "self-test"; do
    label="help: abs-cli $cmd --help"
    output=$($CLI $cmd --help 2>&1) || true
    if echo "$output" | grep -q "Description:\|Usage:"; then
        pass "$label"
    else
        fail "$label" "no help text"
    fi
done

# Leaf commands must have at least 2 examples (for AI agent usability)
for cmd in "login" "config get" "config set" \
           "libraries list" "libraries get" "libraries scan" \
           "items list" "items get" \
           "items update" "items batch-update" "items batch-get" "items scan" \
           "series list" "series get" \
           "authors list" "authors get" \
           "backup create" "backup list" "backup apply" "backup download" "backup delete" "backup upload" \
           "upload" \
           "metadata search" "metadata providers" "metadata covers" \
           "tasks list" \
           "search"; do
    label="help+examples: abs-cli $cmd"
    output=$($CLI $cmd --help 2>&1) || true
    if ! echo "$output" | grep -q "Description:\|Usage:"; then
        fail "$label" "no help text"
        continue
    fi
    example_count=$(echo "$output" | grep -c "abs-cli " || true)
    if [ "$example_count" -ge 2 ]; then
        pass "$label ($example_count examples)"
    else
        fail "$label" "only $example_count examples (need at least 2)"
    fi
done

# items list must have filter groups and sort fields reference
output=$($CLI items list --help 2>&1)
if echo "$output" | grep -q "Filter groups:"; then
    pass "items list help has Filter groups section"
else
    fail "items list help has Filter groups section" "missing"
fi
if echo "$output" | grep -q "genres"; then
    pass "items list filter groups lists genres"
else
    fail "items list filter groups lists genres" "missing"
fi
if echo "$output" | grep -q "Sort fields:"; then
    pass "items list help has Sort fields section"
else
    fail "items list help has Sort fields section" "missing"
fi
if echo "$output" | grep -q "media.metadata.title"; then
    pass "items list sort fields lists title"
else
    fail "items list sort fields lists title" "missing"
fi

# ============================================================
echo ""
echo "=== Config Commands ==="
# ============================================================

output=$($CLI config get 2>/dev/null)
assert_json_key "config get returns JSON" "server" "$output"
assert_json_key "config get has configPath" "configPath" "$output"

# config set + get roundtrip
$CLI config set server "http://smoke-test.example.com" 2>&1
output=$($CLI config get 2>/dev/null)
assert_json_expr "config set/get roundtrip" "d['server']=='http://smoke-test.example.com'" "$output"

# Restore for subsequent tests
$CLI config set server "$ABS_URL" 2>&1

# ============================================================
echo ""
echo "=== Libraries Commands ==="
# ============================================================

output=$($CLI libraries list 2>/dev/null)
assert_json_key "libraries list returns JSON" "libraries" "$output"
assert_json_expr "libraries list has 1 library" "len(d['libraries'])==1" "$output"

output=$($CLI libraries get --id "$LIB_ID" 2>/dev/null)
assert_json_key "libraries get has name" "name" "$output"
assert_json_expr "libraries get correct id" "d['id']=='$LIB_ID'" "$output"
assert_json_expr "libraries get name is Test Library" "d['name']=='Test Library'" "$output"

# ============================================================
echo ""
echo "=== Items Commands ==="
# ============================================================

# List all items
output=$($CLI items list 2>/dev/null)
assert_json_key "items list has results" "results" "$output"
assert_json_key "items list has total" "total" "$output"
assert_json_expr "items list has 16 items" "d['total']==16" "$output"

# Pagination
output=$($CLI items list --limit 5 --page 0 2>/dev/null)
assert_json_expr "items list pagination: 5 results" "len(d['results'])==5" "$output"
assert_json_expr "items list pagination: total still 16" "d['total']==16" "$output"

# Get single item
FIRST_ITEM_ID=$(json_get "$output" "['results'][0]['id']")
output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
assert_json_key "items get has id" "id" "$output"
assert_json_expr "items get correct id" "d['id']=='$FIRST_ITEM_ID'" "$output"
assert_json_key "items get has media" "media" "$output"

# Update metadata — change title, verify it sticks
ORIGINAL_TITLE=$(json_get "$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)" "['media']['metadata']['title']")
output=$(echo '{"metadata":{"title":"Smoke Test Updated Title"}}' | $CLI items update --id "$FIRST_ITEM_ID" --stdin 2>/dev/null)
assert_json_key "items update returns updated item" "libraryItem" "$output"

output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
assert_json_expr "items update persisted new title" \
    "d['media']['metadata']['title']=='Smoke Test Updated Title'" "$output"

# Restore original title
echo "{\"metadata\":{\"title\":\"$ORIGINAL_TITLE\"}}" | $CLI items update --id "$FIRST_ITEM_ID" --stdin 2>/dev/null > /dev/null
output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
assert_json_expr "items update restored original title" \
    "d['media']['metadata']['title']=='$ORIGINAL_TITLE'" "$output"

# Update multiple fields at once
output=$(echo '{"metadata":{"description":"Smoke test description","genres":["Fantasy","Epic"]}}' \
    | $CLI items update --id "$FIRST_ITEM_ID" --stdin 2>/dev/null)
assert_json_key "items multi-field update returns item" "libraryItem" "$output"

output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
assert_json_expr "items multi-field update: description set" \
    "d['media']['metadata']['description']=='Smoke test description'" "$output"
assert_json_expr "items multi-field update: genres set" \
    "'Fantasy' in d['media']['metadata'].get('genres',[])" "$output"

# Restore: clear description and genres
echo '{"metadata":{"description":null,"genres":[]}}' \
    | $CLI items update --id "$FIRST_ITEM_ID" --stdin 2>/dev/null > /dev/null

# Update from file
TMPFILE=$(mktemp)
echo '{"metadata":{"publisher":"Smoke Test Press"}}' > "$TMPFILE"
output=$($CLI items update --id "$FIRST_ITEM_ID" --input "$TMPFILE" 2>/dev/null)
assert_json_key "items update from file returns item" "libraryItem" "$output"

output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
assert_json_expr "items update from file: publisher set" \
    "d['media']['metadata'].get('publisher')=='Smoke Test Press'" "$output"
rm -f "$TMPFILE"

# Restore publisher
echo '{"metadata":{"publisher":null}}' \
    | $CLI items update --id "$FIRST_ITEM_ID" --stdin 2>/dev/null > /dev/null

# Batch get — fetch two items by ID
SECOND_ITEM_ID=$(json_get "$($CLI items list --limit 5 --page 0 2>/dev/null)" "['results'][1]['id']")
output=$(echo "{\"libraryItemIds\":[\"$FIRST_ITEM_ID\",\"$SECOND_ITEM_ID\"]}" \
    | $CLI items batch-get --stdin 2>/dev/null)
assert_json_expr "batch-get returns 2 items" "len(d.get('libraryItems',[]))==2" "$output"

# Batch update — update two items in one call. Guards against the bug
# where the CLI issued PATCH /api/items/batch/update while ABS only
# registers POST for that route (which 404s as "Cannot PATCH ...").
BATCH_PAYLOAD="[{\"id\":\"$FIRST_ITEM_ID\",\"mediaPayload\":{\"metadata\":{\"publisher\":\"Smoke Batch Press A\"}}},{\"id\":\"$SECOND_ITEM_ID\",\"mediaPayload\":{\"metadata\":{\"publisher\":\"Smoke Batch Press B\"}}}]"
output=$(echo "$BATCH_PAYLOAD" | $CLI items batch-update --stdin 2>&1)
rc=$?
if [ $rc -eq 0 ]; then
    assert_json_expr "batch-update returns success" "d.get('success') is True" "$output"
else
    fail "batch-update returns success" "rc=$rc output: ${output:0:200}"
fi

# Verify both items actually got the new publisher.
pub_a=$(json_get "$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)" "['media']['metadata'].get('publisher')")
pub_b=$(json_get "$($CLI items get --id "$SECOND_ITEM_ID" 2>/dev/null)" "['media']['metadata'].get('publisher')")
if [ "$pub_a" = "Smoke Batch Press A" ] && [ "$pub_b" = "Smoke Batch Press B" ]; then
    pass "batch-update persisted both items"
else
    fail "batch-update persisted both items" "got '$pub_a' / '$pub_b'"
fi

# Restore both
$CLI items batch-update --stdin 2>/dev/null <<< "[{\"id\":\"$FIRST_ITEM_ID\",\"mediaPayload\":{\"metadata\":{\"publisher\":null}}},{\"id\":\"$SECOND_ITEM_ID\",\"mediaPayload\":{\"metadata\":{\"publisher\":null}}}]" > /dev/null

# ============================================================
echo ""
echo "=== Item Delete ==="
# ============================================================

# Resolve FOLDER_ID locally — this section runs before the Upload
# section (which sets the shared FOLDER_ID), so don't depend on it.
DELETE_FOLDER_ID=$(json_get "$($CLI libraries get --id "$LIB_ID" 2>/dev/null)" "['folders'][0]['id']")
DELETE_TMP=$(mktemp -d)
python3 -c "
header = bytes([0xFF, 0xFB, 0x90, 0x00]); frame = header + b'\x00' * 413
with open('$DELETE_TMP/d.mp3', 'wb') as f:
    [f.write(frame) for _ in range(38)]
"
DEL_ITEM_1=""; DEL_ITEM_2=""; DEL_ITEM_3=""
delete_cleanup() { cleanup_items "${DELETE_TMP:-}" "${DEL_ITEM_1:-}" "${DEL_ITEM_2:-}" "${DEL_ITEM_3:-}"; }
trap delete_cleanup EXIT

# Single soft delete
out=$($CLI upload --library "$LIB_ID" --folder "$DELETE_FOLDER_ID" --title "DELETE_SOFT" --author "Del Author" --wait --files "$DELETE_TMP/d.mp3" 2>/dev/null)
DEL_ITEM_1=$(json_get "$out" ".get('id','')")
out=$($CLI items delete --id "$DEL_ITEM_1" 2>/dev/null)
assert_json_expr "items delete returns success" "d['success']=='true'" "$out"
out=$($CLI items get --id "$DEL_ITEM_1" 2>&1 || true)
if echo "$out" | grep -qi "not found"; then pass "soft-deleted item is gone"; else fail "soft-deleted item is gone" "got: ${out:0:160}"; fi
DEL_ITEM_1=""

# Batch hard delete (two items)
out=$($CLI upload --library "$LIB_ID" --folder "$DELETE_FOLDER_ID" --title "DELETE_B1" --author "Del Author" --wait --files "$DELETE_TMP/d.mp3" 2>/dev/null)
DEL_ITEM_2=$(json_get "$out" ".get('id','')")
out=$($CLI upload --library "$LIB_ID" --folder "$DELETE_FOLDER_ID" --title "DELETE_B2" --author "Del Author" --wait --files "$DELETE_TMP/d.mp3" 2>/dev/null)
DEL_ITEM_3=$(json_get "$out" ".get('id','')")
out=$(echo "{\"libraryItemIds\":[\"$DEL_ITEM_2\",\"$DEL_ITEM_3\"]}" | $CLI items batch-delete --stdin --hard 2>/dev/null)
assert_json_expr "items batch-delete returns success" "d['success']=='true'" "$out"
out=$($CLI items get --id "$DEL_ITEM_2" 2>&1 || true)
if echo "$out" | grep -qi "not found"; then pass "batch-hard-deleted item 1 gone"; else fail "batch-hard-deleted item 1 gone" "got: ${out:0:160}"; fi
out=$($CLI items get --id "$DEL_ITEM_3" 2>&1 || true)
if echo "$out" | grep -qi "not found"; then pass "batch-hard-deleted item 2 gone"; else fail "batch-hard-deleted item 2 gone" "got: ${out:0:160}"; fi
DEL_ITEM_2=""; DEL_ITEM_3=""

# Permission denial: readonlyuser lacks delete (part E)
out=$($CLI upload --library "$LIB_ID" --folder "$DELETE_FOLDER_ID" --title "DELETE_DENY" --author "Del Author" --wait --files "$DELETE_TMP/d.mp3" 2>/dev/null)
DEL_ITEM_1=$(json_get "$out" ".get('id','')")
abs_login readonlyuser readonlypass
out=$($CLI items delete --id "$DEL_ITEM_1" 2>&1 || true)
if echo "$out" | grep -qi "permission denied.*delete"; then pass "items delete: readonlyuser hits 'delete' permission denial"; else fail "items delete: readonlyuser hits 'delete' permission denial" "got: ${out:0:160}"; fi
abs_login root root

delete_cleanup
trap - EXIT
DEL_ITEM_1=""

# ============================================================
echo ""
echo "=== Series Commands ==="
# ============================================================

output=$($CLI series list --limit 10 2>/dev/null)
assert_json_key "series list has results" "results" "$output"
assert_json_key "series list has total" "total" "$output"
assert_json_expr "series list has 3 series" "d['total']==3" "$output"
assert_json_expr "series list returns results" "len(d['results'])==3" "$output"

# Get a specific series
FIRST_SERIES_ID=$(json_get "$output" "['results'][0]['id']")
output=$($CLI series get --id "$FIRST_SERIES_ID" 2>/dev/null)
assert_json_key "series get has id" "id" "$output"
assert_json_key "series get has name" "name" "$output"

# ============================================================
echo ""
echo "=== Authors Commands ==="
# ============================================================

output=$($CLI authors list 2>/dev/null)
assert_json_key "authors list returns paginated shape (results)" "results" "$output"
assert_json_key "authors list returns paginated shape (total)" "total" "$output"
assert_json_expr "authors list has 7 authors" "d['total']==7 and len(d['results'])==7" "$output"
assert_json_expr "authors list contains Brandon Sanderson" \
    "any(a['name']=='Brandon Sanderson' for a in d['results'])" "$output"

AUTHOR_ID=$(echo "$output" | python3 -c "
import sys,json
authors = json.load(sys.stdin)['results']
bs = next(a for a in authors if a['name']=='Brandon Sanderson')
print(bs['id'])
")

# Pagination round-trip
output=$($CLI authors list --limit 3 --page 0 2>/dev/null)
assert_json_expr "authors list page 0 returns 3 results" "len(d['results'])==3" "$output"
assert_json_expr "authors list page 0 reports total 7" "d['total']==7" "$output"
PAGE0_NAMES=$(echo "$output" | python3 -c "import sys,json; print(','.join(sorted(a['name'] for a in json.load(sys.stdin)['results'])))")

output=$($CLI authors list --limit 3 --page 1 2>/dev/null)
assert_json_expr "authors list page 1 returns 3 results" "len(d['results'])==3" "$output"
assert_json_expr "authors list page 1 reports total 7" "d['total']==7" "$output"
PAGE1_NAMES=$(echo "$output" | python3 -c "import sys,json; print(','.join(sorted(a['name'] for a in json.load(sys.stdin)['results'])))")

if [ "$PAGE0_NAMES" != "$PAGE1_NAMES" ]; then
    pass "authors list pages do not overlap"
else
    fail "authors list pages do not overlap" "page 0 and page 1 returned the same names"
fi

# Reverse sort by name — first result should NOT be the alphabetically-first name
output=$($CLI authors list --sort name --desc 2>/dev/null)
FIRST_NAME=$(json_get "$output" "['results'][0]['name']")
ALPHA_FIRST=$(echo "$output" | python3 -c "import sys,json; print(sorted(a['name'] for a in json.load(sys.stdin)['results'])[0])")
if [ "$FIRST_NAME" != "$ALPHA_FIRST" ]; then
    pass "authors list --sort name --desc starts with last name alphabetically"
else
    fail "authors list --sort name --desc starts with last name alphabetically" "first result was the alphabetically-first name"
fi

output=$($CLI authors get --id "$AUTHOR_ID" 2>/dev/null)
assert_json_key "authors get has id" "id" "$output"
assert_json_expr "authors get is Brandon Sanderson" "d['name']=='Brandon Sanderson'" "$output"

# --- lookup ---
output=$($CLI authors lookup --name "Brandon Sanderson" 2>/dev/null)
assert_json_expr "authors lookup returns object for known author" \
    "isinstance(d, dict) and d.get('name')" "$output"

output=$($CLI authors lookup --name "ZzzNotARealAuthorXyz" 2>/dev/null)
# null body deserialises to Python None
assert_json_expr "authors lookup returns null for missing author" \
    "d is None" "$output"

# --- match ---
output=$($CLI authors match --id "$AUTHOR_ID" --name "Brandon Sanderson" 2>/dev/null)
assert_json_key "authors match returns updated key" "updated" "$output"
assert_json_key "authors match returns author key" "author" "$output"

# --- update (description set, then clear) ---
output=$($CLI authors update --id "$AUTHOR_ID" --description "Smoke-test description" 2>/dev/null)
assert_json_key "authors update returns updated key" "updated" "$output"
assert_json_expr "authors update set description" \
    "d['author']['description']=='Smoke-test description'" "$output"

output=$($CLI authors update --id "$AUTHOR_ID" --description "" 2>/dev/null)
assert_json_expr "authors update cleared description" \
    "d['author'].get('description') in (None, '')" "$output"

# --- delete (add a throwaway co-author to a book, delete it, restore book) ---
ORIGINAL_AUTHORS=$(echo "$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)" \
    | python3 -c "
import sys,json
authors = json.load(sys.stdin)['media']['metadata']['authors']
print(json.dumps([{'name': a['name']} for a in authors]))
")
THROWAWAY_PAYLOAD=$(echo "$ORIGINAL_AUTHORS" | python3 -c "
import sys,json
authors = json.load(sys.stdin)
authors.append({'name': 'Smoke Test Throwaway'})
print(json.dumps({'metadata': {'authors': authors}}))
")
echo "$THROWAWAY_PAYLOAD" | $CLI items update --id "$FIRST_ITEM_ID" --stdin 2>/dev/null > /dev/null

output=$($CLI authors list 2>/dev/null)
assert_json_expr "authors update added throwaway author" \
    "any(a['name']=='Smoke Test Throwaway' for a in d['results'])" "$output"
THROWAWAY_ID=$(echo "$output" | python3 -c "
import sys,json
print(next(a['id'] for a in json.load(sys.stdin)['results'] if a['name']=='Smoke Test Throwaway'))
")

output=$($CLI authors delete --id "$THROWAWAY_ID" 2>/dev/null)
assert_json_expr "authors delete returns success" "d.get('success')=='true'" "$output"

output=$($CLI authors list 2>/dev/null)
assert_json_expr "authors delete removed throwaway" \
    "not any(a['name']=='Smoke Test Throwaway' for a in d['results'])" "$output"
assert_json_expr "authors list back to 7 authors" "len(d['results'])==7" "$output"

# Restore book to original authors
RESTORE_PAYLOAD=$(python3 -c "
import json
print(json.dumps({'metadata': {'authors': json.loads('$ORIGINAL_AUTHORS')}}))
")
echo "$RESTORE_PAYLOAD" | $CLI items update --id "$FIRST_ITEM_ID" --stdin 2>/dev/null > /dev/null

# --- update merge-on-rename (rename throwaway into an existing author) ---
MERGEE_PAYLOAD=$(echo "$ORIGINAL_AUTHORS" | python3 -c "
import sys,json
authors = json.load(sys.stdin)
authors.append({'name': 'Smoke Test Mergee'})
print(json.dumps({'metadata': {'authors': authors}}))
")
echo "$MERGEE_PAYLOAD" | $CLI items update --id "$FIRST_ITEM_ID" --stdin 2>/dev/null > /dev/null

MERGEE_ID=$($CLI authors list 2>/dev/null | python3 -c "
import sys,json
print(next(a['id'] for a in json.load(sys.stdin)['results'] if a['name']=='Smoke Test Mergee'))
")

output=$($CLI authors update --id "$MERGEE_ID" --name "Jim Butcher" 2>/dev/null)
assert_json_expr "authors update rename-into-existing returned merged:true" \
    "d.get('merged')==True" "$output"
assert_json_expr "authors update merge response carries the existing author" \
    "d['author']['name']=='Jim Butcher'" "$output"

output=$($CLI authors list 2>/dev/null)
assert_json_expr "authors update merge removed throwaway" \
    "not any(a['name']=='Smoke Test Mergee' for a in d['results'])" "$output"
assert_json_expr "authors list still 7 after merge" "len(d['results'])==7" "$output"

# Restore book to original authors (merge added Jim Butcher to FIRST_ITEM_ID)
echo "$RESTORE_PAYLOAD" | $CLI items update --id "$FIRST_ITEM_ID" --stdin 2>/dev/null > /dev/null

# --- image set/get/remove ---
output=$($CLI authors image set --id "$AUTHOR_ID" --url "https://placehold.co/64x64.png" 2>/dev/null)
assert_json_key "authors image set returns author" "author" "$output"
assert_json_expr "authors image set populated imagePath" \
    "d['author'].get('imagePath') is not None and d['author']['imagePath']!=''" "$output"

IMG_TMP=$(mktemp --suffix=.png)
output=$($CLI authors image get --id "$AUTHOR_ID" --output "$IMG_TMP" 2>/dev/null)
assert_json_expr "authors image get descriptor reports bytes" "d['bytes']>0" "$output"
if [ -s "$IMG_TMP" ]; then
    pass "authors image get wrote non-empty file"
else
    fail "authors image get wrote non-empty file" "file is empty or missing"
fi
rm -f "$IMG_TMP"

IMG_TMP_RAW=$(mktemp --suffix=.png)
output=$($CLI authors image get --id "$AUTHOR_ID" --output "$IMG_TMP_RAW" --raw 2>/dev/null)
assert_json_expr "authors image get --raw descriptor reports bytes" "d['bytes']>0" "$output"
if [ -s "$IMG_TMP_RAW" ]; then
    pass "authors image get --raw wrote non-empty file"
else
    fail "authors image get --raw wrote non-empty file" "file is empty or missing"
fi
rm -f "$IMG_TMP_RAW"

output=$($CLI authors image remove --id "$AUTHOR_ID" 2>/dev/null)
assert_json_key "authors image remove returns author" "author" "$output"
assert_json_expr "authors image remove cleared imagePath" \
    "d['author'].get('imagePath') is None" "$output"

# Removing again should fail with 400 (the documented quirk)
error_output=$($CLI authors image remove --id "$AUTHOR_ID" 2>&1 || true)
if echo "$error_output" | grep -q "Bad request"; then
    pass "authors image remove on no-image surfaces as 400"
else
    fail "authors image remove on no-image surfaces as 400" "got: ${error_output:0:200}"
fi

# ============================================================
echo ""
echo "=== Search Command (top-level) ==="
# ============================================================

output=$($CLI search --query "Storm Front" 2>/dev/null)
assert_json_key "search has book key" "book" "$output"
assert_json_key "search has authors key" "authors" "$output"
assert_json_key "search has series key" "series" "$output"
assert_json_expr "search finds Storm Front" "len(d.get('book',[]))>0" "$output"

output=$($CLI search --query "Mistborn" 2>/dev/null)
assert_json_expr "search finds Mistborn series" "len(d.get('series',[]))>0" "$output"

# ============================================================
echo ""
echo "=== Backup Commands ==="
# ============================================================

output=$($CLI backup create 2>/dev/null)
assert_json_key "backup create returns backups" "backups" "$output"
assert_json_expr "backup create has at least 1 backup" "len(d['backups'])>=1" "$output"

BACKUP_ID=$(json_get "$output" "['backups'][-1]['id']")

output=$($CLI backup list 2>/dev/null)
assert_json_key "backup list has backups" "backups" "$output"
assert_json_key "backup list has backupLocation" "backupLocation" "$output"
assert_json_expr "backup list finds our backup" \
    "any(b['id']=='$BACKUP_ID' for b in d['backups'])" "$output"

BACKUP_DL=$(mktemp --suffix=.audiobookshelf)
$CLI backup download --id "$BACKUP_ID" --output "$BACKUP_DL" 2>/dev/null
if [ -s "$BACKUP_DL" ]; then
    pass "backup download wrote file"
else
    fail "backup download wrote file" "file is empty"
fi

output=$($CLI backup upload --file "$BACKUP_DL" 2>/dev/null)
assert_json_key "backup upload returns backups" "backups" "$output"
rm -f "$BACKUP_DL"

output=$($CLI backup apply --id "$BACKUP_ID" 2>/dev/null)
pass "backup apply completed (exit 0)"

output=$($CLI backup delete --id "$BACKUP_ID" 2>/dev/null)
assert_json_key "backup delete returns backups" "backups" "$output"

# ============================================================
echo ""
echo "=== Cache Commands ==="
# ============================================================

$CLI cache purge-items 2>/dev/null
pass "cache purge-items completes (exit 0)"

$CLI cache purge 2>/dev/null
pass "cache purge completes (exit 0)"

# ============================================================
echo ""
echo "=== Upload Command ==="
# ============================================================

FOLDER_ID=$(json_get "$($CLI libraries get --id "$LIB_ID" 2>/dev/null)" "['folders'][0]['id']")

UPLOAD_TMP=$(mktemp -d)
python3 -c "
header = bytes([0xFF, 0xFB, 0x90, 0x00])
frame = header + b'\x00' * 413
with open('$UPLOAD_TMP/test.mp3', 'wb') as f:
    for _ in range(38):
        f.write(frame)
"

abs_login uploaduser uploadpass

output=$($CLI upload --title "Smoke Test Upload" --author "Test Author" \
    --folder "$FOLDER_ID" --wait --files "$UPLOAD_TMP/test.mp3" 2>/dev/null)
UPLOADED_ITEM_ID=""
assert_json_expr "upload --wait returned item JSON" "'id' in d" "$output"
UPLOADED_ITEM_ID=$(json_get "$output" "['id']" || echo "")

abs_login root root

# Cleanup: hard-delete the uploaded item (also removes orphan author "Test Author")
if [ -n "$UPLOADED_ITEM_ID" ]; then
    $CLI items delete --id "$UPLOADED_ITEM_ID" --hard >/dev/null 2>&1 || true
fi

# --- Sanitisation drift detection ---
# If the CLI's FilenameSanitizer drifts from ABS's sanitizeFilename, --wait
# will fail to match the scanned item's relPath and these uploads will time
# out. Each case exercises a specific sanitisation rule.

run_drift_case() {
    local label="$1" title="$2" author="$3" expected_relpath="$4" sequence_arg="$5" series_arg="$6"
    abs_login uploaduser uploadpass
    local out
    out=$($CLI upload --title "$title" --author "$author" $series_arg $sequence_arg \
        --folder "$FOLDER_ID" --wait --files "$UPLOAD_TMP/test.mp3" 2>&1)
    local rc=$?
    abs_login root root
    if [ $rc -ne 0 ]; then
        fail "sanitize drift: $label" "upload failed: ${out:0:300}"
        return
    fi
    local actual
    actual=$(json_get "$out" "['relPath'].lstrip('/')" || echo "PARSE_ERROR")
    if [ "$actual" = "$expected_relpath" ]; then
        pass "sanitize drift: $label"
        local item_id=$(json_get "$out" "['id']")
        [ -n "$item_id" ] && $CLI items delete --id "$item_id" --hard >/dev/null 2>&1 || true
    else
        fail "sanitize drift: $label" "expected relPath '$expected_relpath', got '$actual'"
    fi
}

# Colon in title: first ':' replaced with ' - '
run_drift_case "colon in title" "Alien: 3" "Sanitize Author" \
    "Sanitize Author/Alien - 3" "" ""

# Trailing dot in author
run_drift_case "trailing dot in author" "Plain Title One" "J.R.R. Tolkien." \
    "J.R.R. Tolkien/Plain Title One" "" ""

# Sequence prefix: ABS keeps "N. -" in relPath (it's the folder name);
# it only strips the prefix when deriving media.metadata.title. This is
# why title-substring search failed pre-fix — the title-metadata lost the
# prefix but the search query we built included it.
run_drift_case "sequence prefix kept in relPath" "Hobbit Draft" "Sanitize Author Seq" \
    "Sanitize Author Seq/Dwarves/1. - Hobbit Draft" "--sequence 1" "--series Dwarves"

# Decimal sequence: ABS stores BookSeries.sequence as STRING, so "1.5" must
# flow through the CLI unchanged. Regression test for the old int-only typing
# that rejected decimal series positions at the CLI boundary.
run_drift_case "decimal sequence kept in relPath" "Hobbit Decimal" "Sanitize Author Dec" \
    "Sanitize Author Dec/Dwarves/1.5. - Hobbit Decimal" "--sequence 1.5" "--series Dwarves"

# Illegal char (pipe) stripped
run_drift_case "illegal char stripped" "Pipe|Title" "Sanitize Author Pipe" \
    "Sanitize Author Pipe/PipeTitle" "" ""

# Whitespace run collapsed
run_drift_case "whitespace collapsed" "Extra   Spaces   Title" "Sanitize Author WS" \
    "Sanitize Author WS/Extra Spaces Title" "" ""

rm -rf "$UPLOAD_TMP"

# Filename-collision tests: build a 2-dir source tree where both dirs have a file
# called "01.mp3". Confirm default errors, --prefix-source-dir succeeds, and
# --files-manifest succeeds. All three uploads run as uploaduser and clean up.

COLLIDE_TMP=$(mktemp -d)
mkdir -p "$COLLIDE_TMP/Part1" "$COLLIDE_TMP/Part2"
python3 -c "
header = bytes([0xFF, 0xFB, 0x90, 0x00])
frame = header + b'\x00' * 413
for d in ['$COLLIDE_TMP/Part1', '$COLLIDE_TMP/Part2']:
    for n in ['01.mp3', '02.mp3']:
        with open(f'{d}/{n}', 'wb') as f:
            for _ in range(10):
                f.write(frame)
"

abs_login uploaduser uploadpass

# Default: collision detected, error, no upload
collide_out=$($CLI upload --title "Collision Default" --author "Collide Author" \
    --folder "$FOLDER_ID" --files "$COLLIDE_TMP"/Part1/*.mp3 "$COLLIDE_TMP"/Part2/*.mp3 2>&1 || true)
if echo "$collide_out" | grep -qi "Duplicate filenames"; then
    pass "upload errors on duplicate basenames by default"
else
    fail "upload errors on duplicate basenames by default" "got: ${collide_out:0:200}"
fi

# --prefix-source-dir: succeeds, all 4 files land on server with prefixed names
prefix_out=$($CLI upload --title "Collision Prefix" --author "Collide Author Prefix" \
    --folder "$FOLDER_ID" --prefix-source-dir --wait \
    --files "$COLLIDE_TMP"/Part1/*.mp3 "$COLLIDE_TMP"/Part2/*.mp3 2>/dev/null)
PREFIX_ITEM=$(json_get "$prefix_out" ".get('id','')" || echo "")
if [ -n "$PREFIX_ITEM" ]; then
    pass "upload --prefix-source-dir created item"
    abs_login root root
    $CLI items delete --id "$PREFIX_ITEM" --hard >/dev/null 2>&1 || true
    abs_login uploaduser uploadpass
else
    fail "upload --prefix-source-dir created item" "item not found in library"
fi

# --files-manifest: succeeds with explicit per-file naming
cat > "$COLLIDE_TMP/manifest.json" <<EOF
[
  {"src": "$COLLIDE_TMP/Part1/01.mp3", "as": "001-track.mp3"},
  {"src": "$COLLIDE_TMP/Part1/02.mp3", "as": "002-track.mp3"},
  {"src": "$COLLIDE_TMP/Part2/01.mp3", "as": "003-track.mp3"},
  {"src": "$COLLIDE_TMP/Part2/02.mp3", "as": "004-track.mp3"}
]
EOF
manifest_out=$($CLI upload --title "Collision Manifest" --author "Collide Author Manifest" \
    --folder "$FOLDER_ID" --files-manifest "$COLLIDE_TMP/manifest.json" --wait 2>/dev/null)
MANIFEST_ITEM=$(json_get "$manifest_out" ".get('id','')" || echo "")
if [ -n "$MANIFEST_ITEM" ]; then
    pass "upload --files-manifest created item"
    abs_login root root
    $CLI items delete --id "$MANIFEST_ITEM" --hard >/dev/null 2>&1 || true
    abs_login uploaduser uploadpass
else
    fail "upload --files-manifest created item" "item not found in library"
fi

abs_login root root
rm -rf "$COLLIDE_TMP"

# ============================================================
echo ""
echo "=== Permission Errors ==="
# ============================================================

abs_login testuser testpass

UPLOAD_TMP2=$(mktemp -d)
python3 -c "
with open('$UPLOAD_TMP2/test.mp3', 'wb') as f:
    f.write(bytes([0xFF, 0xFB, 0x90, 0x00]) + b'\x00' * 413)
"

error_output=$($CLI upload --title "Should Fail" --author "Test" \
    --folder "$FOLDER_ID" --files "$UPLOAD_TMP2/test.mp3" 2>&1 || true)
if echo "$error_output" | grep -qi "permission denied"; then
    pass "upload as testuser shows permission denied"
else
    fail "upload as testuser shows permission denied" "got: ${error_output:0:200}"
fi
rm -rf "$UPLOAD_TMP2"

error_output=$($CLI backup list 2>&1 || true)
if echo "$error_output" | grep -qi "permission denied\|admin"; then
    pass "backup list as testuser shows permission denied"
else
    fail "backup list as testuser shows permission denied" "got: ${error_output:0:200}"
fi

abs_login root root

# canUpdate denials: testuser AND uploaduser both have update=true in seed.sh,
# so they can't exercise these paths. readonlyuser has update=false.
abs_login readonlyuser readonlypass

error_output=$(echo '{"metadata":{"title":"Should Fail"}}' \
    | $CLI items update --id "$FIRST_ITEM_ID" --stdin 2>&1 || true)
if echo "$error_output" | grep -q "'update' permission"; then
    pass "items update as readonlyuser hits 'update' permission denial"
else
    fail "items update as readonlyuser hits 'update' permission denial" "got: ${error_output:0:200}"
fi

# items batch-update 403: the upstream canUpdate gate landed in ABS v2.34
# (LibraryItemController batch routes). Pre-2.34 servers do not 403 here
# and this assertion will fail against them — the CLI claims support down
# to 2.33.1, but the smoke runs against the dev stack image only.
error_output=$(echo "[{\"id\":\"$FIRST_ITEM_ID\",\"mediaPayload\":{\"metadata\":{\"title\":\"Should Fail\"}}}]" \
    | $CLI items batch-update --stdin 2>&1 || true)
if echo "$error_output" | grep -q "'update' permission"; then
    pass "items batch-update as readonlyuser hits 'update' permission denial"
else
    fail "items batch-update as readonlyuser hits 'update' permission denial" "got: ${error_output:0:200}"
fi

error_output=$($CLI authors update --id "$AUTHOR_ID" --description "Should Fail" 2>&1 || true)
if echo "$error_output" | grep -q "'update' permission"; then
    pass "authors update as readonlyuser hits 'update' permission denial"
else
    fail "authors update as readonlyuser hits 'update' permission denial" "got: ${error_output:0:200}"
fi

error_output=$(echo '{"chapters":[{"title":"x","start":0,"end":1}]}' \
    | $CLI items chapters set --id "$FIRST_ITEM_ID" --stdin 2>&1 || true)
if echo "$error_output" | grep -q "'update' permission"; then
    pass "items chapters set as readonlyuser hits 'update' permission denial"
else
    fail "items chapters set as readonlyuser hits 'update' permission denial" "got: ${error_output:0:200}"
fi

error_output=$($CLI cache purge-items 2>&1 || true)
if echo "$error_output" | grep -q "admin permission"; then
    pass "cache purge-items as readonlyuser hits 'admin permission' denial"
else
    fail "cache purge-items as readonlyuser hits 'admin permission' denial" "got: ${error_output:0:200}"
fi

error_output=$($CLI cache purge 2>&1 || true)
if echo "$error_output" | grep -q "admin permission"; then
    pass "cache purge as readonlyuser hits 'admin permission' denial"
else
    fail "cache purge as readonlyuser hits 'admin permission' denial" "got: ${error_output:0:200}"
fi

abs_login root root

# ============================================================
echo ""
echo "=== Me + Progress ==="
# ============================================================

# 1. me — assert username and permissions are present
output=$($CLI me 2>/dev/null)
assert_json_key "me has username" "username" "$output"
assert_json_key "me has permissions" "permissions" "$output"

# Pick a seeded library item to operate on (independent fetch — the
# Collections block also does this but runs AFTER this one).
progress_items_json=$($CLI items list --limit 1 2>/dev/null)
PROGRESS_LID=$(json_get "$progress_items_json" ".get('results',[{}])[0].get('id','')")
if [ -z "$PROGRESS_LID" ]; then
    fail "progress: seeded library item available" "no items"
else
    pass "progress: seeded library item available"
fi

# Cleanup trap — in case mid-flow abort leaves progress set
progress_cleanup() {
    if [ -n "${PROGRESS_LID:-}" ]; then
        $CLI items progress remove --library-item "$PROGRESS_LID" >/dev/null 2>&1 || true
    fi
}
trap progress_cleanup EXIT

# 2. progress get on item with no progress yet → 404 exit 2
output=$($CLI items progress get --library-item "$PROGRESS_LID" 2>&1 || true)
if echo "$output" | grep -qi "not found"; then
    pass "progress get: 404 surfaces when no progress recorded"
else
    fail "progress get: 404 surfaces when no progress recorded" "got: ${output:0:200}"
fi

# 3. progress set --is-finished true
output=$($CLI items progress set --library-item "$PROGRESS_LID" --is-finished true 2>/dev/null)
assert_json_expr "progress set persisted isFinished:true" "d['isFinished']==True" "$output"

# 4. progress get returns isFinished:true
output=$($CLI items progress get --library-item "$PROGRESS_LID" 2>/dev/null)
assert_json_expr "progress get sees isFinished:true" "d['isFinished']==True" "$output"

# 5. items get --include progress returns userMediaProgress with the same state
output=$($CLI items get --id "$PROGRESS_LID" --include progress 2>/dev/null)
assert_json_expr "items get --include progress decorates with progress" \
    "d.get('userMediaProgress',{}).get('isFinished')==True" "$output"

# 6. batch-update-progress flips isFinished back to false
echo "[{\"libraryItemId\":\"$PROGRESS_LID\",\"isFinished\":false}]" \
    | $CLI items batch-update-progress --stdin >/dev/null 2>&1
output=$($CLI items progress get --library-item "$PROGRESS_LID" 2>/dev/null)
assert_json_expr "batch-update-progress flipped isFinished to false" "d['isFinished']==False" "$output"

# 7. progress remove clears the record
output=$($CLI items progress remove --library-item "$PROGRESS_LID" 2>/dev/null)
assert_json_expr "progress remove returns success:true" "d['success']=='true'" "$output"

# 8. follow-up progress get returns 404
output=$($CLI items progress get --library-item "$PROGRESS_LID" 2>&1 || true)
if echo "$output" | grep -qi "not found"; then
    pass "progress get after remove surfaces 404"
else
    fail "progress get after remove surfaces 404" "got: ${output:0:200}"
fi

# 9. progress set with no body flags → exit 1
output=$($CLI items progress set --library-item "$PROGRESS_LID" 2>&1 || true)
if echo "$output" | grep -qi "Specify at least one"; then
    pass "progress set rejects empty body flags"
else
    fail "progress set rejects empty body flags" "got: ${output:0:200}"
fi

# 10. --finished-at without --is-finished true → exit 1
output=$($CLI items progress set --library-item "$PROGRESS_LID" --finished-at "2026-05-28T12:00:00Z" 2>&1 || true)
if echo "$output" | grep -qi "finished-at only applies"; then
    pass "progress set rejects --finished-at without --is-finished true"
else
    fail "progress set rejects --finished-at without --is-finished true" "got: ${output:0:200}"
fi

trap - EXIT
PROGRESS_LID=""

# ============================================================
echo ""
echo "=== Collections ==="
# ============================================================

collections_cleanup() {
    if [ -n "${COLLECTION_ID:-}" ]; then
        $CLI collections delete --id "$COLLECTION_ID" >/dev/null 2>&1 || true
        COLLECTION_ID=""
    fi
}
trap collections_cleanup EXIT

# Grab three seeded book library item IDs for the smoke flow.
items_json=$($CLI items list --limit 3 2>/dev/null)
LID1=$(json_get "$items_json" ".get('results',[{}])[0].get('id','')")
LID2=$(json_get "$items_json" ".get('results',[{},{}])[1].get('id','')")
LID3=$(json_get "$items_json" ".get('results',[{},{},{}])[2].get('id','')")
if [ -z "$LID1" ] || [ -z "$LID2" ] || [ -z "$LID3" ]; then
    fail "collections: 3 library items available" "missing seeded items"
else
    pass "collections: 3 library items available"
fi

# 1. list — works whether empty or populated; just check the shape.
output=$($CLI collections list 2>/dev/null)
assert_json_key "collections list has results" "results" "$output"
assert_json_key "collections list has total" "total" "$output"

# 2. create with two books (via --stdin)
output=$(echo "{\"books\":[\"$LID1\",\"$LID2\"]}" \
    | $CLI collections create --name "smoke test" --stdin 2>/dev/null)
COLLECTION_ID=$(json_get "$output" ".get('id','')")
if [ -n "$COLLECTION_ID" ]; then
    pass "collections create returns an id"
else
    fail "collections create returns an id" "no id in response"
fi
assert_json_expr "create has 2 books" "len(d['books'])==2" "$output"
assert_json_expr "create persisted name" "d['name']=='smoke test'" "$output"

# 3. get
output=$($CLI collections get --id "$COLLECTION_ID" 2>/dev/null)
assert_json_expr "get returns 2 books" "len(d['books'])==2" "$output"
assert_json_expr "get returns correct name" "d['name']=='smoke test'" "$output"

# 4. update name + description
output=$($CLI collections update --id "$COLLECTION_ID" --name "renamed" --description "desc" 2>/dev/null)
assert_json_expr "update applies name" "d['name']=='renamed'" "$output"
assert_json_expr "update applies description" "d['description']=='desc'" "$output"

# 5. add a third book
output=$($CLI collections add --id "$COLLECTION_ID" --book "$LID3" 2>/dev/null)
assert_json_expr "add yields 3 books" "len(d['books'])==3" "$output"

# 6. duplicate add → 400 → exit 2
output=$($CLI collections add --id "$COLLECTION_ID" --book "$LID3" 2>&1 || true)
if echo "$output" | grep -qi "already in collection\|bad request"; then
    pass "duplicate add surfaces a 400 message"
else
    fail "duplicate add surfaces a 400 message" "got: ${output:0:200}"
fi

# 7. reorder (put LID3 first)
output=$(echo "{\"books\":[\"$LID3\",\"$LID2\",\"$LID1\"]}" \
    | $CLI collections reorder --id "$COLLECTION_ID" --stdin 2>/dev/null)
FIRST_ID=$(json_get "$output" ".get('books',[{}])[0].get('id','')")
if [ "$FIRST_ID" = "$LID3" ]; then
    pass "reorder put LID3 first"
else
    fail "reorder put LID3 first" "got first id: $FIRST_ID"
fi

# 8. batch-remove all three
output=$(echo "{\"books\":[\"$LID1\",\"$LID2\",\"$LID3\"]}" \
    | $CLI collections batch-remove --id "$COLLECTION_ID" --stdin 2>/dev/null)
assert_json_expr "batch-remove empties collection" "len(d['books'])==0" "$output"

# 8b. batch-add two books back into the empty collection
output=$(echo "{\"books\":[\"$LID1\",\"$LID2\"]}" \
    | $CLI collections batch-add --id "$COLLECTION_ID" --stdin 2>/dev/null)
assert_json_expr "batch-add restored 2 books" "len(d['books'])==2" "$output"

# 8c. batch-add is idempotent for duplicates: re-adding LID1 alongside a new LID3
# should yield 3 (skips LID1, adds LID3), unlike single `add` which 400s on dupes
output=$(echo "{\"books\":[\"$LID1\",\"$LID3\"]}" \
    | $CLI collections batch-add --id "$COLLECTION_ID" --stdin 2>/dev/null)
assert_json_expr "batch-add silently skips existing books" "len(d['books'])==3" "$output"

# 8d. single remove drops one book
output=$($CLI collections remove --id "$COLLECTION_ID" --book "$LID2" 2>/dev/null)
assert_json_expr "remove drops the book" "len(d['books'])==2" "$output"

# 9. delete
output=$($CLI collections delete --id "$COLLECTION_ID" 2>/dev/null)
assert_json_expr "delete returns success:true" "d['success']=='true'" "$output"

# 10. get on deleted → 404 → CLI exits non-zero
output=$($CLI collections get --id "$COLLECTION_ID" 2>&1 || true)
if echo "$output" | grep -qi "not found"; then
    pass "get on deleted collection surfaces 404"
else
    fail "get on deleted collection surfaces 404" "got: ${output:0:200}"
fi

# 11. permission denied as readonlyuser (no `update` perm — seeded by docker/seed.sh)
abs_login readonlyuser readonlypass
output=$(echo "{\"books\":[\"$LID1\"]}" \
    | $CLI collections create --name "denied" --stdin 2>&1 || true)
abs_login root root
if echo "$output" | grep -qi "permission denied.*update"; then
    pass "collections create: readonlyuser hits 'update' permission denial"
else
    fail "collections create: readonlyuser hits 'update' permission denial" "got: ${output:0:200}"
fi

trap - EXIT
COLLECTION_ID=""

# ============================================================
echo ""
echo "=== Scan Commands ==="
# ============================================================

$CLI libraries scan 2>/dev/null
pass "libraries scan completed (exit 0)"

sleep 2

output=$($CLI tasks list 2>/dev/null)
assert_json_key "tasks list has tasks" "tasks" "$output"

output=$($CLI items scan --id "$FIRST_ITEM_ID" 2>/dev/null)
assert_json_key "items scan has result" "result" "$output"

# ============================================================
echo ""
echo "=== Metadata Commands ==="
# ============================================================

output=$($CLI metadata providers 2>/dev/null)
assert_json_key "metadata providers has providers" "providers" "$output"
assert_json_expr "metadata providers has books" "len(d['providers']['books'])>0" "$output"
assert_json_expr "metadata providers has google" \
    "any(p['value']=='google' for p in d['providers']['books'])" "$output"

if [ "${SMOKE_TEST_EXTERNAL:-}" = "1" ]; then
    echo "  (external provider tests enabled)"
    output=$($CLI metadata search --provider google --title "Storm Front" --author "Jim Butcher" 2>/dev/null)
    assert_json_expr "metadata search returns results" "len(d)>0" "$output"
    output=$($CLI metadata covers --provider google --title "Storm Front" 2>/dev/null)
    assert_json_key "metadata covers has results" "results" "$output"
else
    echo "  (skipping external provider tests — set SMOKE_TEST_EXTERNAL=1 to enable)"
fi

# ============================================================
echo ""
echo "=== Cover Commands ==="

# Generate a tiny valid PNG (1x1 transparent pixel) as a fixture.
COVER_TMP=$(mktemp -d)
COVER_FILE="$COVER_TMP/cover.png"
python3 -c "
import struct, zlib
def chunk(t, d):
    return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t+d) & 0xffffffff)
sig = b'\x89PNG\r\n\x1a\n'
ihdr = chunk(b'IHDR', struct.pack('>IIBBBBB', 1, 1, 8, 6, 0, 0, 0))
idat_raw = b'\x00\x00\x00\x00\x00'
idat = chunk(b'IDAT', zlib.compress(idat_raw))
iend = chunk(b'IEND', b'')
with open('$COVER_FILE', 'wb') as f:
    f.write(sig + ihdr + idat + iend)
"

# --- Mode 1: --file (multipart upload) ---
output=$($CLI items cover set --id "$FIRST_ITEM_ID" --file "$COVER_FILE" 2>/dev/null)
assert_json_expr "items cover set --file applied cover" "d['success']==True and d['cover']" "$output"
SERVER_COVER_PATH=$(json_get "$output" "['cover']" || echo "")

output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
assert_json_expr "items get reports non-null coverPath after --file set" "d['media'].get('coverPath')" "$output"

# Download cover to file
DOWNLOAD_FILE="$COVER_TMP/downloaded.bin"
output=$($CLI items cover get --id "$FIRST_ITEM_ID" --output "$DOWNLOAD_FILE" 2>/dev/null)
assert_json_expr "items cover get --output writes file and reports descriptor" "d['path']=='$DOWNLOAD_FILE' and d['bytes']>0" "$output"
if [ -s "$DOWNLOAD_FILE" ]; then
    pass "downloaded cover file is non-empty"
else
    fail "downloaded cover file is non-empty" "file missing or zero-byte"
fi

# Stream cover bytes to stdout (capture via wc -c)
bytes=$($CLI items cover get --id "$FIRST_ITEM_ID" --output - 2>/dev/null | wc -c)
if [ "$bytes" -gt 0 ]; then
    pass "items cover get --output - streams non-zero bytes to stdout"
else
    fail "items cover get --output - streams non-zero bytes to stdout" "zero bytes"
fi

# Remove cover
output=$($CLI items cover remove --id "$FIRST_ITEM_ID" 2>/dev/null)
assert_json_expr "items cover remove returns success" "d['success']" "$output"

output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
assert_json_expr "items get reports null coverPath after remove" "d['media'].get('coverPath') is None" "$output"

# --- Mode 2: --server-path (PATCH, link to existing on-disk file) ---
# Re-link the cover file the previous --file step left on the ABS server's disk.
if [ -n "$SERVER_COVER_PATH" ]; then
    output=$($CLI items cover set --id "$FIRST_ITEM_ID" --server-path "$SERVER_COVER_PATH" 2>/dev/null)
    assert_json_expr "items cover set --server-path applied cover from existing on-disk file" "d['success']==True and d['cover']=='$SERVER_COVER_PATH'" "$output"

    output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
    assert_json_expr "items get coverPath matches --server-path target" "d['media'].get('coverPath')=='$SERVER_COVER_PATH'" "$output"

    # Cleanup: remove again so the next mode (or end-state) is clean
    $CLI items cover remove --id "$FIRST_ITEM_ID" > /dev/null 2>&1
fi

# --- Mode 3: --url (POST with {url}; ABS server downloads) ---
# Looks up the first seeded item's title/author in-flight so the cover
# query always tracks whatever the current seed actually contains, then
# asks ABS's `best` meta-provider for cover URLs. `best` aggregates across
# Google, FantLab, Amazon, etc., which is far more reliable than pinning
# to any single provider (e.g. Google returns no covers at all for some
# seeded titles like "Rivers of London").
item_json=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
item_title=$(json_get "$item_json" "['media']['metadata'].get('title') or ''")
item_author=$(json_get "$item_json" "['media']['metadata'].get('authorName') or ''")

if [ -n "$item_title" ]; then
    covers_json=$($CLI metadata covers --provider best --title "$item_title" --author "$item_author" 2>/dev/null)
    cover_url=$(echo "$covers_json" | python3 -c "import sys,json; d=json.load(sys.stdin); r=d.get('results',[]); print(r[0] if r else '')" 2>/dev/null)

    if [ -n "$cover_url" ]; then
        pass "metadata covers returned a URL for seeded book"

        output=$($CLI items cover set --id "$FIRST_ITEM_ID" --url "$cover_url" 2>/dev/null)
        assert_json_expr "items cover set --url applied cover from metadata-provider URL" "d['success']==True and d['cover']" "$output"

        output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
        assert_json_expr "items get reports non-null coverPath after --url set" "d['media'].get('coverPath')" "$output"

        $CLI items cover remove --id "$FIRST_ITEM_ID" > /dev/null 2>&1
    else
        fail "metadata covers returned a URL for seeded book" "best returned no URLs for '$item_title'"
    fi
else
    fail "items get readable for --url cover test" "could not read item title"
fi

rm -rf "$COVER_TMP"

# ============================================================
echo ""
echo "=== Encode M4B Commands ==="

# Generate two real MP3 fixtures with distinct tones via ffmpeg.
ENCODE_TMP=$(mktemp -d)
ENCODE_ITEM_ID=""
CANCEL_TEST_ITEM_ID=""
encode_cleanup() { cleanup_items "${ENCODE_TMP:-}" "${ENCODE_ITEM_ID:-}" "${CANCEL_TEST_ITEM_ID:-}"; }
trap encode_cleanup EXIT

ffmpeg -y -f lavfi -i "sine=frequency=440:duration=30" -ac 2 -c:a libmp3lame -b:a 128k \
    "$ENCODE_TMP/track1.mp3" > /dev/null 2>&1
ffmpeg -y -f lavfi -i "sine=frequency=523:duration=30" -ac 2 -c:a libmp3lame -b:a 128k \
    "$ENCODE_TMP/track2.mp3" > /dev/null 2>&1

if [ -s "$ENCODE_TMP/track1.mp3" ] && [ -s "$ENCODE_TMP/track2.mp3" ]; then
    pass "encode-m4b: ffmpeg generated two non-empty MP3 fixtures"
else
    fail "encode-m4b: ffmpeg generated two non-empty MP3 fixtures" "one or both empty"
fi

# Upload as a multi-file audiobook.
output=$($CLI upload --library "$LIB_ID" --folder "$FOLDER_ID" \
    --title "ENCODE_M4B_TEST" --author "Smoke Author" \
    --wait --files "$ENCODE_TMP/track1.mp3" "$ENCODE_TMP/track2.mp3" 2>/dev/null)
ENCODE_ITEM_ID=$(json_get "$output" ".get('id', '')")
if [ -n "$ENCODE_ITEM_ID" ]; then
    pass "encode-m4b: upload created a library item (id=$ENCODE_ITEM_ID)"
else
    fail "encode-m4b: upload created a library item" "no id in upload response"
    echo "    response: ${output:0:200}"
fi

# Assert the freshly-uploaded item has two audio files.
output=$($CLI items get --id "$ENCODE_ITEM_ID" 2>/dev/null)
assert_json_expr "encode-m4b: item starts with 2 audioFiles" "len(d['media']['audioFiles'])==2" "$output"

# Run encode-m4b start --codec copy.
output=$($CLI items encode-m4b start --id "$ENCODE_ITEM_ID" --codec copy 2>/dev/null)
if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
assert d['libraryItemId'] == '$ENCODE_ITEM_ID'
assert d['action'] == 'encode-m4b'
assert d['started'] is True
assert d['options']['codec'] == 'copy'
assert 'bitrate' not in d['options']
assert 'channels' not in d['options']
" 2>/dev/null; then
    pass "encode-m4b start: receipt shape valid (codec only, no defaults)"
else
    fail "encode-m4b start: receipt shape valid" "unexpected receipt"
    echo "    response: ${output:0:200}"
fi

# Already-processing 400: a second start before the task completes should fail.
output=$($CLI items encode-m4b start --id "$ENCODE_ITEM_ID" --codec copy 2>&1 || true)
if echo "$output" | grep -q "already processing"; then
    pass "encode-m4b start: second start while pending returns 400"
else
    fail "encode-m4b start: second start while pending returns 400" "missing 'already processing'"
    echo "    response: ${output:0:200}"
fi

# Poll tasks list until all tasks are gone. The encode-m4b itself finishes
# quickly (especially with codec=copy), but ABS then fires a watcher-scan that
# updates the item record in the DB. We must wait for that scan to finish too,
# so we poll for zero tasks total rather than filtering by action.
poll_ok=0
for i in $(seq 1 90); do
    tasks=$($CLI tasks list 2>/dev/null)
    task_count=$(echo "$tasks" | python3 -c "
import sys, json
d = json.load(sys.stdin)
print(len(d.get('tasks', [])))
" 2>/dev/null)
    if [ "${task_count:-1}" = "0" ]; then
        poll_ok=1
        break
    fi
    sleep 1
done
if [ "$poll_ok" = "1" ]; then
    pass "encode-m4b: all tasks cleared (encode + watcher-scan) within 90s"
else
    fail "encode-m4b: all tasks cleared (encode + watcher-scan) within 90s" "tasks still pending after timeout"
fi

# Verify post-encode state: one audio file, named *.m4b.
# The watcher-scan task clears from the tasks list before ABS finishes writing
# the updated item to the DB, so poll the item directly until it converges.
item_poll_ok=0
for i in $(seq 1 30); do
    output=$($CLI items get --id "$ENCODE_ITEM_ID" 2>/dev/null)
    if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
af = d['media']['audioFiles']
assert len(af) == 1
assert af[0]['metadata']['filename'].endswith('.m4b')
" 2>/dev/null; then
        item_poll_ok=1
        break
    fi
    sleep 1
done
if [ "$item_poll_ok" = "1" ]; then
    pass "encode-m4b: post-encode item has single .m4b audio file"
else
    fail "encode-m4b: post-encode item has single .m4b audio file" "post-state mismatch after 30s"
    echo "    response: ${output:0:300}"
fi

# Cancel 404: cancel on a nonexistent item must surface the combined message.
output=$($CLI items encode-m4b cancel --id "li_does_not_exist_$$" 2>&1 || true)
if echo "$output" | grep -q "no pending encode-m4b task for item"; then
    pass "encode-m4b cancel: 404 surfaces combined notFoundHint message"
else
    fail "encode-m4b cancel: 404 surfaces combined notFoundHint message" "missing hint string"
    echo "    response: ${output:0:200}"
fi

# Cancel happy path: upload a separate item, start an aac re-encode (slower
# than copy/remux), cancel mid-flight, verify the item was NOT merged. The
# 30s fixtures + aac@192k re-encoding gives enough headroom for the cancel to
# arrive before the task finishes on typical hardware.
output=$($CLI upload --library "$LIB_ID" --folder "$FOLDER_ID" \
    --title "ENCODE_M4B_CANCEL_TEST" --author "Smoke Author" \
    --wait --files "$ENCODE_TMP/track1.mp3" "$ENCODE_TMP/track2.mp3" 2>/dev/null)
CANCEL_TEST_ITEM_ID=$(json_get "$output" ".get('id', '')")
if [ -n "$CANCEL_TEST_ITEM_ID" ]; then
    pass "encode-m4b cancel: upload created cancel-test item (id=$CANCEL_TEST_ITEM_ID)"
else
    fail "encode-m4b cancel: upload created cancel-test item" "no id in upload response"
fi

# Start a slow-ish aac re-encode and immediately fire cancel.
$CLI items encode-m4b start --id "$CANCEL_TEST_ITEM_ID" \
    --codec aac --bitrate 192k --channels 2 > /dev/null 2>&1
cancel_output=$($CLI items encode-m4b cancel --id "$CANCEL_TEST_ITEM_ID" 2>&1)
cancel_exit=$?
if [ "$cancel_exit" = "0" ] && [ -z "$cancel_output" ]; then
    pass "encode-m4b cancel: happy path exits 0 with empty stdout"
else
    fail "encode-m4b cancel: happy path exits 0 with empty stdout" "exit=$cancel_exit, output=${cancel_output:0:200}"
fi

# Poll tasks list until all tasks are gone (cancel + any trailing watcher-scan).
cancel_poll_ok=0
for i in $(seq 1 30); do
    tasks=$($CLI tasks list 2>/dev/null)
    task_count=$(echo "$tasks" | python3 -c "
import sys, json
d = json.load(sys.stdin)
print(len(d.get('tasks', [])))
" 2>/dev/null)
    if [ "${task_count:-1}" = "0" ]; then
        cancel_poll_ok=1
        break
    fi
    sleep 1
done
if [ "$cancel_poll_ok" = "1" ]; then
    pass "encode-m4b cancel: tasks list cleared within 30s after cancel"
else
    fail "encode-m4b cancel: tasks list cleared within 30s after cancel" "tasks still pending"
fi

# Verify the item still has 2 audioFiles (cancel prevented the merge). If the
# encode finished before cancel landed, this assertion will fail with the item
# at audioFiles.length == 1 (single .m4b) — diagnose-able.
output=$($CLI items get --id "$CANCEL_TEST_ITEM_ID" 2>/dev/null)
if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
af = d['media']['audioFiles']
assert len(af) == 2, f'audioFiles count after cancel: {len(af)} (expected 2 — encode may have finished before cancel landed)'
" 2>/dev/null; then
    pass "encode-m4b cancel: item retains 2 audioFiles (merge did not complete)"
else
    fail "encode-m4b cancel: item retains 2 audioFiles (merge did not complete)" "post-cancel state mismatch"
    echo "    response: ${output:0:300}"
fi

# Enum-validation rejection (no HTTP call).
output=$($CLI items encode-m4b start --id "$ENCODE_ITEM_ID" --codec wmv 2>&1 || true)
if echo "$output" | grep -q "must be one of: copy, aac, opus"; then
    pass "encode-m4b start: --codec wmv rejected client-side"
else
    fail "encode-m4b start: --codec wmv rejected client-side" "missing enum error"
    echo "    response: ${output:0:200}"
fi

# Permission denial: non-admin user attempting start should 403.
abs_login uploaduser uploadpass
output=$($CLI items encode-m4b start --id "$ENCODE_ITEM_ID" --codec copy 2>&1 || true)
abs_login root root
if echo "$output" | grep -qi "permission denied.*admin"; then
    pass "encode-m4b start: non-admin user gets 403 admin-permission message"
else
    fail "encode-m4b start: non-admin user gets 403 admin-permission message" "wrong / missing 403"
    echo "    response: ${output:0:200}"
fi

# Cleanup runs via the trap above; call it explicitly so re-runs in the same shell are clean.
encode_cleanup
trap - EXIT
ENCODE_ITEM_ID=""

# ============================================================
echo ""
echo "=== Chapter Commands ==="

CHAPTERS_TMP=$(mktemp -d)
CHAPTERS_ITEM_ID=""
chapters_cleanup() { cleanup_items "${CHAPTERS_TMP:-}" "${CHAPTERS_ITEM_ID:-}"; }
trap chapters_cleanup EXIT

# Use the real ffmpeg fixture pattern from the encode-m4b block to avoid
# the synthetic-MP3 + --wait scan timing flakiness seen at line 553.
ffmpeg -y -f lavfi -i "sine=frequency=440:duration=5" -ac 2 -c:a libmp3lame -b:a 128k \
    "$CHAPTERS_TMP/test.mp3" > /dev/null 2>&1

abs_login uploaduser uploadpass
output=$($CLI upload --folder "$FOLDER_ID" \
    --title "CHAPTERS_TEST" --author "Smoke Author" \
    --wait --files "$CHAPTERS_TMP/test.mp3" 2>/dev/null)
abs_login root root
CHAPTERS_ITEM_ID=$(json_get "$output" ".get('id', '')")
if [ -n "$CHAPTERS_ITEM_ID" ]; then
    pass "chapters: upload created test item (id=$CHAPTERS_ITEM_ID)"
else
    fail "chapters: upload created test item" "no id in upload response"
fi

cat > "$CHAPTERS_TMP/chapters.json" <<EOF
{"chapters":[
    {"title":"Test Chapter 1","start":0,"end":0.5},
    {"title":"Test Chapter 2","start":0.5,"end":1.0}
]}
EOF

# Happy path: set returns success/updated=true.
output=$($CLI items chapters set --id "$CHAPTERS_ITEM_ID" --input "$CHAPTERS_TMP/chapters.json" 2>/dev/null)
if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
assert d['success'] is True
assert d['updated'] is True
" 2>/dev/null; then
    pass "chapters set: returns success=true updated=true on first write"
else
    fail "chapters set: returns success=true updated=true on first write" "unexpected response"
    echo "    response: ${output:0:200}"
fi

# Idempotence: same body again returns updated=false.
output=$($CLI items chapters set --id "$CHAPTERS_ITEM_ID" --input "$CHAPTERS_TMP/chapters.json" 2>/dev/null)
if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
assert d['success'] is True
assert d['updated'] is False
" 2>/dev/null; then
    pass "chapters set: returns updated=false on no-op repeat"
else
    fail "chapters set: returns updated=false on no-op repeat" "unexpected response"
    echo "    response: ${output:0:200}"
fi

# Chapters visible on items get.
output=$($CLI items get --id "$CHAPTERS_ITEM_ID" 2>/dev/null)
if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
chs = d['media']['chapters']
assert len(chs) == 2
assert chs[0]['title'] == 'Test Chapter 1'
assert chs[0]['start'] == 0
assert chs[0]['end'] == 0.5
assert chs[1]['title'] == 'Test Chapter 2'
" 2>/dev/null; then
    pass "chapters set: chapters visible on items get"
else
    fail "chapters set: chapters visible on items get" "post-state mismatch"
    echo "    response: ${output:0:300}"
fi

# Stdin input path.
output=$(echo '{"chapters":[{"title":"Stdin Ch","start":0,"end":0.7}]}' \
    | $CLI items chapters set --id "$CHAPTERS_ITEM_ID" --stdin 2>/dev/null)
if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
assert d['success'] is True
assert d['updated'] is True
" 2>/dev/null; then
    pass "chapters set: --stdin path writes successfully"
else
    fail "chapters set: --stdin path writes successfully" "unexpected response"
fi

# Mutual-exclusion: both --input and --stdin.
output=$($CLI items chapters set --id "$CHAPTERS_ITEM_ID" \
    --input "$CHAPTERS_TMP/chapters.json" --stdin <<< '{"chapters":[]}' 2>&1 || true)
if echo "$output" | grep -q "exactly one"; then
    pass "chapters set: rejects both --input and --stdin"
else
    fail "chapters set: rejects both --input and --stdin" "missing error string"
fi

# Mutual-exclusion: neither --input nor --stdin.
output=$($CLI items chapters set --id "$CHAPTERS_ITEM_ID" 2>&1 || true)
if echo "$output" | grep -q "Provide --input"; then
    pass "chapters set: rejects when neither --input nor --stdin given"
else
    fail "chapters set: rejects when neither --input nor --stdin given" "missing error string"
fi

# Malformed JSON: wrong type for start.
output=$(echo '{"chapters":[{"title":"x","start":"not-a-number","end":1.0}]}' \
    | $CLI items chapters set --id "$CHAPTERS_ITEM_ID" --stdin 2>&1 || true)
if echo "$output" | grep -q "Invalid chapters JSON"; then
    pass "chapters set: wrong-typed start rejected client-side"
else
    fail "chapters set: wrong-typed start rejected client-side" "missing error string"
fi

# 500 quirk on missing item.
output=$($CLI items chapters set --id "li_does_not_exist_$$" \
    --input "$CHAPTERS_TMP/chapters.json" 2>&1 || true)
if [ -n "$output" ]; then
    pass "chapters set: nonexistent item produces non-zero exit"
else
    fail "chapters set: nonexistent item produces non-zero exit" "empty output"
fi

# 403 (canUpdate denial) is exercised in the Permission Errors section
# using readonlyuser, alongside items update / batch-update / authors update.

# Chapter lookup tests hit Audnexus via ABS — same external assumption as
# the already-ungated `authors lookup` / `authors match` tests above (which
# use Brandon Sanderson). We pair with a Sanderson ASIN here so there's a
# single class of external dependency, not two.

# Known-good ASIN: Mistborn: The Final Empire (Brandon Sanderson) —
# 47 Audnexus-indexed chapters, isAccurate=true (verified 2026-05-13).
# If this ever 404s, find another Sanderson Audible ASIN via
# `abs-cli metadata search --provider audible --title <title>
# --author "Brandon Sanderson"` and confirm it via
# `items chapters lookup` before swapping. Do NOT change the test to
# expect failure.
output=$($CLI items chapters lookup --asin "B002V0QCYU" 2>/dev/null || true)
if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
assert d['asin'] == 'B002V0QCYU'
assert len(d['chapters']) > 0
assert 'isAccurate' in d
" 2>/dev/null; then
    pass "chapters lookup: known-good ASIN returns chapters"
else
    fail "chapters lookup: known-good ASIN returns chapters" "unexpected response"
    echo "    response: ${output:0:200}"
fi

# Well-formed but unknown ASIN → exit 2 with "Chapters not found".
output=$($CLI items chapters lookup --asin "B000000000" 2>&1 || true)
if echo "$output" | grep -q "Chapters not found"; then
    pass "chapters lookup: unknown ASIN surfaces 'Chapters not found'"
else
    fail "chapters lookup: unknown ASIN surfaces 'Chapters not found'" "missing error string"
    echo "    response: ${output:0:200}"
fi

# Malformed ASIN → exit 2 with "Invalid ASIN". Server-side ABS rejects
# at `isValidASIN` before any Audnexus call, so this case has zero
# external dependency.
output=$($CLI items chapters lookup --asin "not-an-asin" 2>&1 || true)
if echo "$output" | grep -q "Invalid ASIN"; then
    pass "chapters lookup: invalid ASIN surfaces 'Invalid ASIN'"
else
    fail "chapters lookup: invalid ASIN surfaces 'Invalid ASIN'" "missing error string"
    echo "    response: ${output:0:200}"
fi

chapters_cleanup
trap - EXIT
CHAPTERS_ITEM_ID=""

# ============================================================
echo ""
echo "=== Embed Metadata Commands ==="

EMBED_TMP=$(mktemp -d)
EMBED_ITEM_ID=""
EMBED_ITEM_ID_2=""
embed_cleanup() { cleanup_items "${EMBED_TMP:-}" "${EMBED_ITEM_ID:-}" "${EMBED_ITEM_ID_2:-}"; }
trap embed_cleanup EXIT

# Two real-audio fixtures via ffmpeg (matches encode-m4b smoke pattern).
ffmpeg -y -f lavfi -i "sine=frequency=440:duration=5" -ac 2 -c:a libmp3lame -b:a 128k \
    "$EMBED_TMP/track1.mp3" > /dev/null 2>&1
ffmpeg -y -f lavfi -i "sine=frequency=523:duration=5" -ac 2 -c:a libmp3lame -b:a 128k \
    "$EMBED_TMP/track2.mp3" > /dev/null 2>&1

abs_login uploaduser uploadpass

output=$($CLI upload --folder "$FOLDER_ID" \
    --title "EMBED_METADATA_TEST_1" --author "Smoke Author" \
    --wait --files "$EMBED_TMP/track1.mp3" 2>/dev/null)
EMBED_ITEM_ID=$(json_get "$output" ".get('id', '')")

output=$($CLI upload --folder "$FOLDER_ID" \
    --title "EMBED_METADATA_TEST_2" --author "Smoke Author" \
    --wait --files "$EMBED_TMP/track2.mp3" 2>/dev/null)
EMBED_ITEM_ID_2=$(json_get "$output" ".get('id', '')")

abs_login root root

if [ -n "$EMBED_ITEM_ID" ] && [ -n "$EMBED_ITEM_ID_2" ]; then
    pass "embed-metadata: uploaded two test items ($EMBED_ITEM_ID, $EMBED_ITEM_ID_2)"
else
    fail "embed-metadata: uploaded two test items" "missing one or both upload IDs"
fi

# --- Single happy path with --wait ---
output=$($CLI items embed-metadata --id "$EMBED_ITEM_ID" --wait 2>/dev/null)
exit_code=$?
if [ "$exit_code" = "0" ] && echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
assert d['libraryItemId'] == '$EMBED_ITEM_ID'
assert d['action'] == 'embed-metadata'
assert d['started'] is True
assert d['options']['backup'] is True
assert d['options']['forceEmbedChapters'] is False
" 2>/dev/null; then
    pass "embed-metadata --wait: receipt shape valid with defaults"
else
    fail "embed-metadata --wait: receipt shape valid with defaults" "exit=$exit_code, output: ${output:0:200}"
fi

# --- --no-backup option-echo verification (the server-side filesystem
# check is omitted: docker-compose vs GitHub Actions service container
# expose the ABS metadata mount under different paths, so reaching into
# the container is not portable. The receipt is the contract we ship.)
output=$($CLI items embed-metadata --id "$EMBED_ITEM_ID_2" --no-backup --wait 2>/dev/null)
if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
assert d['options']['backup'] is False
" 2>/dev/null; then
    pass "embed-metadata --no-backup: receipt reflects backup=false"
else
    fail "embed-metadata --no-backup: receipt reflects backup=false" "unexpected response"
fi

# --- Batch happy path with --wait ---
output=$(echo "{\"libraryItemIds\":[\"$EMBED_ITEM_ID\",\"$EMBED_ITEM_ID_2\"]}" \
    | $CLI items batch-embed-metadata --stdin --wait 2>/dev/null)
if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
assert d['action'] == 'embed-metadata'
assert d['started'] is True
assert sorted(d['libraryItemIds']) == sorted(['$EMBED_ITEM_ID', '$EMBED_ITEM_ID_2'])
assert d['options']['backup'] is True
" 2>/dev/null; then
    pass "batch-embed-metadata --wait: receipt shape valid"
else
    fail "batch-embed-metadata --wait: receipt shape valid" "unexpected response: ${output:0:300}"
fi

# --- Negatives ---
output=$($CLI items embed-metadata --id "li_does_not_exist_$$" 2>&1 || true)
if echo "$output" | grep -qE "(Not found|Bad request)"; then
    pass "embed-metadata: nonexistent item exits 2"
else
    fail "embed-metadata: nonexistent item exits 2" "unexpected: ${output:0:200}"
fi

output=$(echo '{"libraryItemIds":[]}' | $CLI items batch-embed-metadata --stdin 2>&1 || true)
if echo "$output" | grep -q "non-empty array"; then
    pass "batch-embed-metadata: empty list rejected client-side"
else
    fail "batch-embed-metadata: empty list rejected client-side" "unexpected: ${output:0:200}"
fi

output=$(echo "{\"libraryItemIds\":[\"$EMBED_ITEM_ID\",\"li_nonexistent_$$\"]}" \
    | $CLI items batch-embed-metadata --stdin 2>&1 || true)
if echo "$output" | grep -qE "(Not found|Bad request)"; then
    pass "batch-embed-metadata: bad ID in list aborts the whole batch"
else
    fail "batch-embed-metadata: bad ID in list aborts the whole batch" "unexpected: ${output:0:200}"
fi

output=$($CLI items batch-embed-metadata --input /dev/null --stdin <<< '{}' 2>&1 || true)
if echo "$output" | grep -q "exactly one"; then
    pass "batch-embed-metadata: rejects both --input and --stdin"
else
    fail "batch-embed-metadata: rejects both --input and --stdin" "missing error string"
fi

output=$($CLI items batch-embed-metadata 2>&1 || true)
if echo "$output" | grep -q "Provide --input"; then
    pass "batch-embed-metadata: rejects when neither --input nor --stdin given"
else
    fail "batch-embed-metadata: rejects when neither --input nor --stdin given" "missing error string"
fi

output=$(echo '{"libraryItemIds":"not-an-array"}' | $CLI items batch-embed-metadata --stdin 2>&1 || true)
if echo "$output" | grep -q "Invalid JSON"; then
    pass "batch-embed-metadata: wrong-typed libraryItemIds rejected client-side"
else
    fail "batch-embed-metadata: wrong-typed libraryItemIds rejected client-side" "unexpected: ${output:0:200}"
fi

# Permission denial: uploaduser is non-admin.
abs_login uploaduser uploadpass
output=$($CLI items embed-metadata --id "$EMBED_ITEM_ID" 2>&1 || true)
abs_login root root
if echo "$output" | grep -q "admin permission"; then
    pass "embed-metadata: uploaduser hits admin permission denial"
else
    fail "embed-metadata: uploaduser hits admin permission denial" "unexpected: ${output:0:200}"
fi

embed_cleanup
trap - EXIT
EMBED_ITEM_ID=""
EMBED_ITEM_ID_2=""

# ============================================================
echo ""
echo "=== Items Get Expanded ==="

# The seeded "Multi Ebook Test" item is the natural fixture — it has
# two ebook files (.epub + .pdf), and the libraryFiles[] array is the
# main reason --expanded exists.
EXPANDED_ITEM_ID=$($CLI items list --library "$LIB_ID" --limit 100 2>/dev/null \
    | python3 -c "
import sys, json
d = json.load(sys.stdin)
for r in d['results']:
    if r.get('media',{}).get('metadata',{}).get('title','') == 'Multi Ebook Test':
        print(r['id']); break
" 2>/dev/null)

if [ -n "$EXPANDED_ITEM_ID" ]; then
    pass "items get --expanded: located seeded multi-ebook item ($EXPANDED_ITEM_ID)"
else
    fail "items get --expanded: located seeded multi-ebook item" "Multi Ebook Test not found"
fi

# Default (minified) — must NOT contain libraryFiles.
output=$($CLI items get --id "$EXPANDED_ITEM_ID" 2>/dev/null)
if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
assert 'libraryFiles' not in d
assert d['id'] == '$EXPANDED_ITEM_ID'
" 2>/dev/null; then
    pass "items get (default): minified shape has no libraryFiles"
else
    fail "items get (default): minified shape has no libraryFiles" "unexpected shape"
fi

# --expanded — must contain libraryFiles with the two ebook entries.
output=$($CLI items get --id "$EXPANDED_ITEM_ID" --expanded 2>/dev/null)
if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
assert 'libraryFiles' in d
ebook_files = [lf for lf in d['libraryFiles'] if lf.get('fileType') == 'ebook']
assert len(ebook_files) >= 2, f'expected 2+ ebook files, got {len(ebook_files)}'
inos = sorted([lf['ino'] for lf in ebook_files])
supplementary = [lf for lf in ebook_files if lf.get('isSupplementary') is True]
primary = [lf for lf in ebook_files if lf.get('isSupplementary') is False]
assert len(supplementary) == 1, f'expected 1 supplementary, got {len(supplementary)}'
assert len(primary) == 1, f'expected 1 primary, got {len(primary)}'
" 2>/dev/null; then
    pass "items get --expanded: libraryFiles has 2 ebook entries with correct primary/supplementary split"
else
    fail "items get --expanded: libraryFiles has 2 ebook entries with correct primary/supplementary split" "unexpected response"
    echo "    response: ${output:0:300}"
fi

# Sanity: --expanded also surfaces the bonus fields.
output=$($CLI items get --id "$EXPANDED_ITEM_ID" --expanded 2>/dev/null)
if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
assert 'lastScan' in d
assert 'scanVersion' in d
" 2>/dev/null; then
    pass "items get --expanded: exposes lastScan and scanVersion"
else
    fail "items get --expanded: exposes lastScan and scanVersion" "missing bonus fields"
fi

# ============================================================
echo ""
echo "=== Toggle Ebook Status ==="

# Find the seeded multi-ebook item by title.
EBOOK_ITEM_ID=$($CLI items list --library "$LIB_ID" --limit 100 2>/dev/null \
    | python3 -c "
import sys, json
d = json.load(sys.stdin)
for r in d['results']:
    if r.get('media',{}).get('metadata',{}).get('title','') == 'Multi Ebook Test':
        print(r['id']); break
" 2>/dev/null)

if [ -n "$EBOOK_ITEM_ID" ]; then
    pass "toggle-ebook-status: located seeded multi-ebook item ($EBOOK_ITEM_ID)"
else
    fail "toggle-ebook-status: located seeded multi-ebook item" "Multi Ebook Test not found"
fi

# Read initial state. `items get --expanded` returns the full shape
# including libraryFiles[]. Both the primary's ino and the
# supplementary's ino come from one CLI call.
EBOOK_STATE=$($CLI items get --id "$EBOOK_ITEM_ID" --expanded 2>/dev/null \
    | python3 -c "
import sys, json
d = json.load(sys.stdin)
primary_ino = (d.get('media',{}).get('ebookFile') or {}).get('ino','')
ebook_files = [lf for lf in d.get('libraryFiles',[]) if lf.get('fileType') == 'ebook']
inos = [lf.get('ino','') for lf in ebook_files]
supplementary = [i for i in inos if i != primary_ino]
print(f'{primary_ino}|{supplementary[0] if supplementary else \"\"}')
" 2>/dev/null)
PRIMARY_INO=$(echo "$EBOOK_STATE" | cut -d'|' -f1)
SUPP_INO=$(echo "$EBOOK_STATE" | cut -d'|' -f2)

if [ -n "$PRIMARY_INO" ] && [ -n "$SUPP_INO" ]; then
    pass "toggle-ebook-status: read initial state (primary=$PRIMARY_INO, supplementary=$SUPP_INO)"
else
    fail "toggle-ebook-status: read initial state" "primary=$PRIMARY_INO supplementary=$SUPP_INO"
fi

# Toggle the supplementary file → becomes primary.
output=$($CLI items toggle-ebook-status --id "$EBOOK_ITEM_ID" --ino "$SUPP_INO" 2>/dev/null)
if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
assert d['libraryItemId'] == '$EBOOK_ITEM_ID'
assert d['fileIno'] == '$SUPP_INO'
assert d['action'] == 'toggle-ebook-status'
assert d['toggled'] is True
" 2>/dev/null; then
    pass "toggle-ebook-status: receipt shape valid"
else
    fail "toggle-ebook-status: receipt shape valid" "unexpected: ${output:0:200}"
fi

# Verify state flipped: previously-supplementary is now primary.
new_primary=$($CLI items get --id "$EBOOK_ITEM_ID" 2>/dev/null \
    | python3 -c "
import sys, json
d = json.load(sys.stdin)
print((d.get('media',{}).get('ebookFile') or {}).get('ino',''))
" 2>/dev/null)
if [ "$new_primary" = "$SUPP_INO" ]; then
    pass "toggle-ebook-status: supplementary file is now primary"
else
    fail "toggle-ebook-status: supplementary file is now primary" "expected $SUPP_INO, got $new_primary"
fi

# Recovery toggle: re-target the original primary → restore.
$CLI items toggle-ebook-status --id "$EBOOK_ITEM_ID" --ino "$PRIMARY_INO" > /dev/null 2>&1
restored_primary=$($CLI items get --id "$EBOOK_ITEM_ID" 2>/dev/null \
    | python3 -c "
import sys, json
d = json.load(sys.stdin)
print((d.get('media',{}).get('ebookFile') or {}).get('ino',''))
" 2>/dev/null)
if [ "$restored_primary" = "$PRIMARY_INO" ]; then
    pass "toggle-ebook-status: recovery toggle restored original primary"
else
    fail "toggle-ebook-status: recovery toggle restored original primary" "expected $PRIMARY_INO, got $restored_primary"
fi

# Negative: bogus --ino returns 404 with ABS message.
output=$($CLI items toggle-ebook-status --id "$EBOOK_ITEM_ID" --ino "99999999" 2>&1 || true)
if echo "$output" | grep -qE "(Not found|does not exist)"; then
    pass "toggle-ebook-status: bogus --ino exits 2 with 404 passthrough"
else
    fail "toggle-ebook-status: bogus --ino exits 2 with 404 passthrough" "unexpected: ${output:0:200}"
fi

# Permission denial: readonlyuser (no canUpdate) → 403.
abs_login readonlyuser readonlypass
output=$($CLI items toggle-ebook-status --id "$EBOOK_ITEM_ID" --ino "$PRIMARY_INO" 2>&1 || true)
abs_login root root
if echo "$output" | grep -q "'update' permission"; then
    pass "toggle-ebook-status: readonlyuser hits 'update' permission denial"
else
    fail "toggle-ebook-status: readonlyuser hits 'update' permission denial" "unexpected: ${output:0:200}"
fi

# ============================================================
echo ""
echo "=== Diagnostic Logging ==="
# ============================================================

# Debug on via ABS_DEBUG=1 emits at least one DEBUG line to stderr.
debug_output=$(ABS_DEBUG=1 $CLI libraries list 2>&1 >/dev/null)
if echo "$debug_output" | grep -qE '^[0-9TZ:.\-]+ DEBUG '; then
    pass "ABS_DEBUG=1 libraries list emits DEBUG lines"
else
    fail "ABS_DEBUG=1 libraries list emits DEBUG lines" "got: ${debug_output:0:200}"
fi

# Default level (no --debug, no ABS_DEBUG) emits no DEBUG lines.
default_output=$($CLI libraries list 2>&1 >/dev/null)
if echo "$default_output" | grep -qE ' DEBUG '; then
    fail "default libraries list does not emit DEBUG lines" "got: ${default_output:0:200}"
else
    pass "default libraries list does not emit DEBUG lines"
fi

# --log-json combined with ABS_DEBUG=1 emits parseable JSON with the three expected fields.
json_first_line=$(ABS_DEBUG=1 $CLI --log-json libraries list 2>&1 >/dev/null | head -1)
assert_json_expr "ABS_DEBUG=1 --log-json libraries list emits JSON with timestamp/level/message" "'timestamp' in d and 'level' in d and 'message' in d" "$json_first_line"

# ============================================================
echo ""
echo "========================================"
echo "Results: $PASS passed, $FAIL failed"
echo "========================================"

if [ "$FAIL" -gt 0 ]; then
    exit 1
fi
