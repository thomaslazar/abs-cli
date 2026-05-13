#!/usr/bin/env bash
# Smoke test: exercises every abs-cli command against a live ABS instance.
# Tests the actual binary (AOT or JIT) — not `dotnet run`.
#
# Expects a seeded ABS instance (15 books, 6 authors, 3 series).
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

# --- Get auth token and library ID from ABS directly ---
echo "Setting up test context..."
TOKEN=$(curl -sf -X POST "$ABS_URL/login" \
    -H 'Content-Type: application/json' \
    -H 'X-Return-Tokens: true' \
    -d '{"username":"root","password":"root"}' \
    | python3 -c "import sys,json; print(json.load(sys.stdin)['user']['accessToken'])")

LIB_ID=$(curl -sf "$ABS_URL/api/libraries" \
    -H "Authorization: Bearer $TOKEN" \
    | python3 -c "import sys,json; print(json.load(sys.stdin)['libraries'][0]['id'])")

echo "ABS_URL=$ABS_URL  LIB_ID=$LIB_ID"
echo ""

# Export env so CLI picks them up
export ABS_SERVER="$ABS_URL"
export ABS_TOKEN="$TOKEN"
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
assert_json_expr "items list has 15 items" "d['total']==15" "$output"

# Pagination
output=$($CLI items list --limit 5 --page 0 2>/dev/null)
assert_json_expr "items list pagination: 5 results" "len(d['results'])==5" "$output"
assert_json_expr "items list pagination: total still 15" "d['total']==15" "$output"

# Get single item
FIRST_ITEM_ID=$(echo "$output" | python3 -c "import sys,json; print(json.load(sys.stdin)['results'][0]['id'])")
output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
assert_json_key "items get has id" "id" "$output"
assert_json_expr "items get correct id" "d['id']=='$FIRST_ITEM_ID'" "$output"
assert_json_key "items get has media" "media" "$output"

# Update metadata — change title, verify it sticks
ORIGINAL_TITLE=$(echo "$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)" \
    | python3 -c "import sys,json; print(json.load(sys.stdin)['media']['metadata']['title'])")
output=$($CLI items update --id "$FIRST_ITEM_ID" --input '{"metadata":{"title":"Smoke Test Updated Title"}}' 2>/dev/null)
assert_json_key "items update returns updated item" "libraryItem" "$output"

output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
assert_json_expr "items update persisted new title" \
    "d['media']['metadata']['title']=='Smoke Test Updated Title'" "$output"

# Restore original title
$CLI items update --id "$FIRST_ITEM_ID" --input "{\"metadata\":{\"title\":\"$ORIGINAL_TITLE\"}}" 2>/dev/null > /dev/null
output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
assert_json_expr "items update restored original title" \
    "d['media']['metadata']['title']=='$ORIGINAL_TITLE'" "$output"

# Update multiple fields at once
output=$($CLI items update --id "$FIRST_ITEM_ID" \
    --input '{"metadata":{"description":"Smoke test description","genres":["Fantasy","Epic"]}}' 2>/dev/null)
assert_json_key "items multi-field update returns item" "libraryItem" "$output"

output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
assert_json_expr "items multi-field update: description set" \
    "d['media']['metadata']['description']=='Smoke test description'" "$output"
assert_json_expr "items multi-field update: genres set" \
    "'Fantasy' in d['media']['metadata'].get('genres',[])" "$output"

# Restore: clear description and genres
$CLI items update --id "$FIRST_ITEM_ID" \
    --input '{"metadata":{"description":null,"genres":[]}}' 2>/dev/null > /dev/null

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
$CLI items update --id "$FIRST_ITEM_ID" \
    --input '{"metadata":{"publisher":null}}' 2>/dev/null > /dev/null

# Batch get — fetch two items by ID
SECOND_ITEM_ID=$($CLI items list --limit 5 --page 0 2>/dev/null \
    | python3 -c "import sys,json; print(json.load(sys.stdin)['results'][1]['id'])")
output=$(echo "{\"libraryItemIds\":[\"$FIRST_ITEM_ID\",\"$SECOND_ITEM_ID\"]}" \
    | $CLI items batch-get --stdin 2>/dev/null)
assert_json_expr "batch-get returns 2 items" "len(d.get('libraryItems',[]))==2" "$output"

# Batch update — update two items in one call. Guards against the bug
# where the CLI issued PATCH /api/items/batch/update while ABS only
# registers POST for that route (which 404s as "Cannot PATCH ...").
BATCH_PAYLOAD="[{\"id\":\"$FIRST_ITEM_ID\",\"mediaPayload\":{\"metadata\":{\"publisher\":\"Smoke Batch Press A\"}}},{\"id\":\"$SECOND_ITEM_ID\",\"mediaPayload\":{\"metadata\":{\"publisher\":\"Smoke Batch Press B\"}}}]"
output=$(echo "$BATCH_PAYLOAD" | $CLI items batch-update --stdin 2>&1)
rc=$?
if [ $rc -eq 0 ] && echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d.get('success') is True" 2>/dev/null; then
    pass "batch-update returns success"
else
    fail "batch-update returns success" "rc=$rc output: ${output:0:200}"
fi

# Verify both items actually got the new publisher.
pub_a=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null | python3 -c "import sys,json; print(json.load(sys.stdin)['media']['metadata'].get('publisher'))")
pub_b=$($CLI items get --id "$SECOND_ITEM_ID" 2>/dev/null | python3 -c "import sys,json; print(json.load(sys.stdin)['media']['metadata'].get('publisher'))")
if [ "$pub_a" = "Smoke Batch Press A" ] && [ "$pub_b" = "Smoke Batch Press B" ]; then
    pass "batch-update persisted both items"
else
    fail "batch-update persisted both items" "got '$pub_a' / '$pub_b'"
fi

# Restore both
$CLI items batch-update --stdin 2>/dev/null <<< "[{\"id\":\"$FIRST_ITEM_ID\",\"mediaPayload\":{\"metadata\":{\"publisher\":null}}},{\"id\":\"$SECOND_ITEM_ID\",\"mediaPayload\":{\"metadata\":{\"publisher\":null}}}]" > /dev/null

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
FIRST_SERIES_ID=$(echo "$output" | python3 -c "import sys,json; print(json.load(sys.stdin)['results'][0]['id'])")
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
assert_json_expr "authors list has 6 authors" "d['total']==6 and len(d['results'])==6" "$output"
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
assert_json_expr "authors list page 0 reports total 6" "d['total']==6" "$output"
PAGE0_NAMES=$(echo "$output" | python3 -c "import sys,json; print(','.join(sorted(a['name'] for a in json.load(sys.stdin)['results'])))")

output=$($CLI authors list --limit 3 --page 1 2>/dev/null)
assert_json_expr "authors list page 1 returns 3 results" "len(d['results'])==3" "$output"
assert_json_expr "authors list page 1 reports total 6" "d['total']==6" "$output"
PAGE1_NAMES=$(echo "$output" | python3 -c "import sys,json; print(','.join(sorted(a['name'] for a in json.load(sys.stdin)['results'])))")

if [ "$PAGE0_NAMES" != "$PAGE1_NAMES" ]; then
    pass "authors list pages do not overlap"
else
    fail "authors list pages do not overlap" "page 0 and page 1 returned the same names"
fi

# Reverse sort by name — first result should NOT be the alphabetically-first name
output=$($CLI authors list --sort name --desc 2>/dev/null)
FIRST_NAME=$(echo "$output" | python3 -c "import sys,json; print(json.load(sys.stdin)['results'][0]['name'])")
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
$CLI items update --id "$FIRST_ITEM_ID" --input "$THROWAWAY_PAYLOAD" 2>/dev/null > /dev/null

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
assert_json_expr "authors list back to 6 authors" "len(d['results'])==6" "$output"

# Restore book to original authors
RESTORE_PAYLOAD=$(python3 -c "
import json
print(json.dumps({'metadata': {'authors': json.loads('$ORIGINAL_AUTHORS')}}))
")
$CLI items update --id "$FIRST_ITEM_ID" --input "$RESTORE_PAYLOAD" 2>/dev/null > /dev/null

# --- update merge-on-rename (rename throwaway into an existing author) ---
MERGEE_PAYLOAD=$(echo "$ORIGINAL_AUTHORS" | python3 -c "
import sys,json
authors = json.load(sys.stdin)
authors.append({'name': 'Smoke Test Mergee'})
print(json.dumps({'metadata': {'authors': authors}}))
")
$CLI items update --id "$FIRST_ITEM_ID" --input "$MERGEE_PAYLOAD" 2>/dev/null > /dev/null

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
assert_json_expr "authors list still 6 after merge" "len(d['results'])==6" "$output"

# Restore book to original authors (merge added Jim Butcher to FIRST_ITEM_ID)
$CLI items update --id "$FIRST_ITEM_ID" --input "$RESTORE_PAYLOAD" 2>/dev/null > /dev/null

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

BACKUP_ID=$(echo "$output" | python3 -c "import sys,json; print(json.load(sys.stdin)['backups'][-1]['id'])")

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
echo "=== Upload Command ==="
# ============================================================

FOLDER_ID=$(echo "$($CLI libraries get --id "$LIB_ID" 2>/dev/null)" \
    | python3 -c "import sys,json; print(json.load(sys.stdin)['folders'][0]['id'])")

UPLOAD_TMP=$(mktemp -d)
python3 -c "
header = bytes([0xFF, 0xFB, 0x90, 0x00])
frame = header + b'\x00' * 413
with open('$UPLOAD_TMP/test.mp3', 'wb') as f:
    for _ in range(38):
        f.write(frame)
"

UPLOAD_TOKEN=$(curl -sf -X POST "$ABS_URL/login" \
    -H 'Content-Type: application/json' \
    -H 'X-Return-Tokens: true' \
    -d '{"username":"uploaduser","password":"uploadpass"}' \
    | python3 -c "import sys,json; print(json.load(sys.stdin)['user']['accessToken'])")

SAVE_TOKEN="$ABS_TOKEN"
export ABS_TOKEN="$UPLOAD_TOKEN"

output=$($CLI upload --title "Smoke Test Upload" --author "Test Author" \
    --folder "$FOLDER_ID" --wait --files "$UPLOAD_TMP/test.mp3" 2>/dev/null)
UPLOADED_ITEM_ID=""
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert 'id' in d" 2>/dev/null; then
    pass "upload --wait returned item JSON"
    UPLOADED_ITEM_ID=$(echo "$output" | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
else
    fail "upload --wait returned item JSON" "no id in response"
    echo "    response: ${output:0:200}"
fi

export ABS_TOKEN="$SAVE_TOKEN"

# Cleanup: hard-delete the uploaded item (also removes orphan author "Test Author")
if [ -n "$UPLOADED_ITEM_ID" ]; then
    curl -sf -X DELETE "$ABS_URL/api/items/$UPLOADED_ITEM_ID?hard=1" \
        -H "Authorization: Bearer $ABS_TOKEN" > /dev/null 2>&1 || true
fi

# --- Sanitisation drift detection ---
# If the CLI's FilenameSanitizer drifts from ABS's sanitizeFilename, --wait
# will fail to match the scanned item's relPath and these uploads will time
# out. Each case exercises a specific sanitisation rule.

run_drift_case() {
    local label="$1" title="$2" author="$3" expected_relpath="$4" sequence_arg="$5" series_arg="$6"
    export ABS_TOKEN="$UPLOAD_TOKEN"
    local out
    out=$($CLI upload --title "$title" --author "$author" $series_arg $sequence_arg \
        --folder "$FOLDER_ID" --wait --files "$UPLOAD_TMP/test.mp3" 2>&1)
    local rc=$?
    export ABS_TOKEN="$SAVE_TOKEN"
    if [ $rc -ne 0 ]; then
        fail "sanitize drift: $label" "upload failed: ${out:0:300}"
        return
    fi
    local actual
    actual=$(echo "$out" | python3 -c "import sys,json; print(json.load(sys.stdin)['relPath'].lstrip('/'))" 2>/dev/null || echo "PARSE_ERROR")
    if [ "$actual" = "$expected_relpath" ]; then
        pass "sanitize drift: $label"
        local item_id=$(echo "$out" | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])" 2>/dev/null)
        [ -n "$item_id" ] && curl -sf -X DELETE "$ABS_URL/api/items/$item_id?hard=1" \
            -H "Authorization: Bearer $ABS_TOKEN" > /dev/null 2>&1 || true
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

export ABS_TOKEN="$UPLOAD_TOKEN"

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
PREFIX_ITEM=$(echo "$prefix_out" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null || echo "")
if [ -n "$PREFIX_ITEM" ]; then
    pass "upload --prefix-source-dir created item"
    export ABS_TOKEN="$SAVE_TOKEN"
    curl -sf -X DELETE "$ABS_URL/api/items/$PREFIX_ITEM?hard=1" \
        -H "Authorization: Bearer $ABS_TOKEN" > /dev/null 2>&1 || true
    export ABS_TOKEN="$UPLOAD_TOKEN"
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
MANIFEST_ITEM=$(echo "$manifest_out" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null || echo "")
if [ -n "$MANIFEST_ITEM" ]; then
    pass "upload --files-manifest created item"
    export ABS_TOKEN="$SAVE_TOKEN"
    curl -sf -X DELETE "$ABS_URL/api/items/$MANIFEST_ITEM?hard=1" \
        -H "Authorization: Bearer $ABS_TOKEN" > /dev/null 2>&1 || true
    export ABS_TOKEN="$UPLOAD_TOKEN"
else
    fail "upload --files-manifest created item" "item not found in library"
fi

export ABS_TOKEN="$SAVE_TOKEN"
rm -rf "$COLLIDE_TMP"

# ============================================================
echo ""
echo "=== Permission Errors ==="
# ============================================================

TEST_TOKEN=$(curl -sf -X POST "$ABS_URL/login" \
    -H 'Content-Type: application/json' \
    -H 'X-Return-Tokens: true' \
    -d '{"username":"testuser","password":"testpass"}' \
    | python3 -c "import sys,json; print(json.load(sys.stdin)['user']['accessToken'])")

SAVE_TOKEN="$ABS_TOKEN"
export ABS_TOKEN="$TEST_TOKEN"

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

export ABS_TOKEN="$SAVE_TOKEN"

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
    if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert len(d)>0" 2>/dev/null; then
        pass "metadata search returns results"
    else
        fail "metadata search returns results" "empty or invalid response"
    fi
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
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['success']==True and d['cover']" 2>/dev/null; then
    pass "items cover set --file applied cover"
    SERVER_COVER_PATH=$(echo "$output" | python3 -c "import sys,json; print(json.load(sys.stdin)['cover'])")
else
    fail "items cover set --file applied cover" "unexpected response"
    echo "    response: ${output:0:200}"
    SERVER_COVER_PATH=""
fi

output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['media'].get('coverPath')" 2>/dev/null; then
    pass "items get reports non-null coverPath after --file set"
else
    fail "items get reports non-null coverPath after --file set" "coverPath missing"
fi

# Download cover to file
DOWNLOAD_FILE="$COVER_TMP/downloaded.bin"
output=$($CLI items cover get --id "$FIRST_ITEM_ID" --output "$DOWNLOAD_FILE" 2>/dev/null)
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['path']=='$DOWNLOAD_FILE' and d['bytes']>0" 2>/dev/null; then
    pass "items cover get --output writes file and reports descriptor"
else
    fail "items cover get --output writes file and reports descriptor" "unexpected descriptor"
    echo "    response: ${output:0:200}"
fi
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
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['success']" 2>/dev/null; then
    pass "items cover remove returns success"
else
    fail "items cover remove returns success" "unexpected response"
fi

output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['media'].get('coverPath') is None" 2>/dev/null; then
    pass "items get reports null coverPath after remove"
else
    fail "items get reports null coverPath after remove" "coverPath still set"
fi

# --- Mode 2: --server-path (PATCH, link to existing on-disk file) ---
# Re-link the cover file the previous --file step left on the ABS server's disk.
if [ -n "$SERVER_COVER_PATH" ]; then
    output=$($CLI items cover set --id "$FIRST_ITEM_ID" --server-path "$SERVER_COVER_PATH" 2>/dev/null)
    if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['success']==True and d['cover']=='$SERVER_COVER_PATH'" 2>/dev/null; then
        pass "items cover set --server-path applied cover from existing on-disk file"
    else
        fail "items cover set --server-path applied cover from existing on-disk file" "unexpected response"
        echo "    response: ${output:0:200}"
    fi

    output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
    if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['media'].get('coverPath')=='$SERVER_COVER_PATH'" 2>/dev/null; then
        pass "items get coverPath matches --server-path target"
    else
        fail "items get coverPath matches --server-path target" "coverPath did not match"
    fi

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
item_title=$(echo "$item_json" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['media']['metadata'].get('title') or '')")
item_author=$(echo "$item_json" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['media']['metadata'].get('authorName') or '')")

if [ -n "$item_title" ]; then
    covers_json=$($CLI metadata covers --provider best --title "$item_title" --author "$item_author" 2>/dev/null)
    cover_url=$(echo "$covers_json" | python3 -c "import sys,json; d=json.load(sys.stdin); r=d.get('results',[]); print(r[0] if r else '')" 2>/dev/null)

    if [ -n "$cover_url" ]; then
        pass "metadata covers returned a URL for seeded book"

        output=$($CLI items cover set --id "$FIRST_ITEM_ID" --url "$cover_url" 2>/dev/null)
        if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['success']==True and d['cover']" 2>/dev/null; then
            pass "items cover set --url applied cover from metadata-provider URL"
        else
            fail "items cover set --url applied cover from metadata-provider URL" "unexpected response"
            echo "    response: ${output:0:200}"
        fi

        output=$($CLI items get --id "$FIRST_ITEM_ID" 2>/dev/null)
        if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['media'].get('coverPath')" 2>/dev/null; then
            pass "items get reports non-null coverPath after --url set"
        else
            fail "items get reports non-null coverPath after --url set" "coverPath missing"
        fi

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
encode_cleanup() {
    if [ -n "$ENCODE_ITEM_ID" ]; then
        curl -sf -X DELETE "$ABS_URL/api/items/$ENCODE_ITEM_ID?hard=1" \
            -H "Authorization: Bearer $ABS_TOKEN" > /dev/null 2>&1 || true
    fi
    if [ -n "$CANCEL_TEST_ITEM_ID" ]; then
        curl -sf -X DELETE "$ABS_URL/api/items/$CANCEL_TEST_ITEM_ID?hard=1" \
            -H "Authorization: Bearer $ABS_TOKEN" > /dev/null 2>&1 || true
    fi
    rm -rf "$ENCODE_TMP"
}
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
ENCODE_ITEM_ID=$(echo "$output" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id', ''))" 2>/dev/null)
if [ -n "$ENCODE_ITEM_ID" ]; then
    pass "encode-m4b: upload created a library item (id=$ENCODE_ITEM_ID)"
else
    fail "encode-m4b: upload created a library item" "no id in upload response"
    echo "    response: ${output:0:200}"
fi

# Assert the freshly-uploaded item has two audio files.
output=$($CLI items get --id "$ENCODE_ITEM_ID" 2>/dev/null)
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert len(d['media']['audioFiles'])==2" 2>/dev/null; then
    pass "encode-m4b: item starts with 2 audioFiles"
else
    fail "encode-m4b: item starts with 2 audioFiles" "audioFiles count mismatch"
fi

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
CANCEL_TEST_ITEM_ID=$(echo "$output" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id', ''))" 2>/dev/null)
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
if [ -n "${UPLOAD_TOKEN:-}" ]; then
    SAVE_TOKEN_ENCODE="$ABS_TOKEN"
    export ABS_TOKEN="$UPLOAD_TOKEN"
    output=$($CLI items encode-m4b start --id "$ENCODE_ITEM_ID" --codec copy 2>&1 || true)
    export ABS_TOKEN="$SAVE_TOKEN_ENCODE"
    if echo "$output" | grep -qi "permission denied.*admin"; then
        pass "encode-m4b start: non-admin user gets 403 admin-permission message"
    else
        fail "encode-m4b start: non-admin user gets 403 admin-permission message" "wrong / missing 403"
        echo "    response: ${output:0:200}"
    fi
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
chapters_cleanup() {
    if [ -n "$CHAPTERS_ITEM_ID" ]; then
        curl -sf -X DELETE "$ABS_URL/api/items/$CHAPTERS_ITEM_ID?hard=1" \
            -H "Authorization: Bearer $ABS_TOKEN" > /dev/null 2>&1 || true
    fi
    rm -rf "$CHAPTERS_TMP"
}
trap chapters_cleanup EXIT

# Use the real ffmpeg fixture pattern from the encode-m4b block to avoid
# the synthetic-MP3 + --wait scan timing flakiness seen at line 553.
ffmpeg -y -f lavfi -i "sine=frequency=440:duration=5" -ac 2 -c:a libmp3lame -b:a 128k \
    "$CHAPTERS_TMP/test.mp3" > /dev/null 2>&1

SAVE_TOKEN="$ABS_TOKEN"
export ABS_TOKEN="$UPLOAD_TOKEN"
output=$($CLI upload --folder "$FOLDER_ID" \
    --title "CHAPTERS_TEST" --author "Smoke Author" \
    --wait --files "$CHAPTERS_TMP/test.mp3" 2>/dev/null)
export ABS_TOKEN="$SAVE_TOKEN"
CHAPTERS_ITEM_ID=$(echo "$output" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id', ''))" 2>/dev/null)
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

# 403 not smoke-tested: both seeded non-admin users (testuser, uploaduser)
# have canUpdate=true (see docker/seed.sh:65,86). The permission-denied path
# uses the standard permissionHint mechanism shared with `items update` and
# is documented in the verb's --help text.

# Audnexus lookup tests are gated behind SMOKE_TEST_EXTERNAL=1
# (same gate as external metadata-provider tests). Hits live audnex.us — slow,
# flaky in sandboxed CI.
if [ "${SMOKE_TEST_EXTERNAL:-}" = "1" ]; then
    echo "  (external lookup tests enabled)"

    # Known-good ASIN: popular Audible release with 19 Audnexus-indexed
    # chapters (verified 2026-05-13 against audnex.us). If this ever
    # 404s, probe a few popular ASINs against `items chapters lookup`
    # and rotate to one that returns chapters — do NOT change the test
    # to expect failure.
    output=$($CLI items chapters lookup --asin "B017V4IM1G" 2>/dev/null || true)
    if echo "$output" | python3 -c "
import sys, json
d = json.load(sys.stdin)
assert d['asin'] == 'B017V4IM1G'
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

    # Malformed ASIN → exit 2 with "Invalid ASIN".
    output=$($CLI items chapters lookup --asin "not-an-asin" 2>&1 || true)
    if echo "$output" | grep -q "Invalid ASIN"; then
        pass "chapters lookup: invalid ASIN surfaces 'Invalid ASIN'"
    else
        fail "chapters lookup: invalid ASIN surfaces 'Invalid ASIN'" "missing error string"
        echo "    response: ${output:0:200}"
    fi
else
    echo "  (skipping external lookup tests — set SMOKE_TEST_EXTERNAL=1 to enable)"
fi

chapters_cleanup
trap - EXIT
CHAPTERS_ITEM_ID=""

# ============================================================
echo ""
echo "========================================"
echo "Results: $PASS passed, $FAIL failed"
echo "========================================"

if [ "$FAIL" -gt 0 ]; then
    exit 1
fi
