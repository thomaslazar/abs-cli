#!/usr/bin/env bash
# Smoke test: exercises every abs-cli command against a live ABS instance.
# Tests the actual binary (AOT or JIT) — not `dotnet run`.
#
# Expects a seeded ABS instance (15 books, 6 authors, 3 series).
#
# Usage:
#   ABS_URL=http://localhost:13378 CLI=./path/to/abs-cli bash docker/smoke-test.sh
#
# If CLI is not set, builds a Release binary via `dotnet publish` first.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ABS_URL="${ABS_URL:-http://localhost:13378}"

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

for cmd in "" "login" "config" "config get" "config set" \
           "libraries" "libraries list" "libraries get" \
           "items" "items list" "items get" "items search" \
           "items update" "items batch-update" "items batch-get" \
           "series" "series list" "series get" \
           "authors" "authors list" "authors get" \
           "search" "self-test"; do
    label="help: abs-cli $cmd --help"
    output=$($CLI $cmd --help 2>&1) || true
    if echo "$output" | grep -q "Description:\|Usage:"; then
        pass "$label"
    else
        fail "$label" "no help text"
    fi
done

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

# Search — find a known book by title
output=$($CLI items search --query "Final Empire" 2>/dev/null)
assert_json_key "items search has book key" "book" "$output"
assert_json_expr "items search finds Final Empire" "len(d.get('book',[]))>0" "$output"

# Search — no results
output=$($CLI items search --query "zzz_nonexistent_xyz" 2>/dev/null)
assert_json_expr "items search empty for garbage query" "len(d.get('book',[]))==0" "$output"

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
assert_json_key "authors list has authors" "authors" "$output"
assert_json_expr "authors list has 6 authors" "len(d['authors'])==6" "$output"

# Check a known author exists
assert_json_expr "authors list contains Brandon Sanderson" \
    "any(a['name']=='Brandon Sanderson' for a in d['authors'])" "$output"

# Get a specific author
AUTHOR_ID=$(echo "$output" | python3 -c "
import sys,json
authors = json.load(sys.stdin)['authors']
bs = next(a for a in authors if a['name']=='Brandon Sanderson')
print(bs['id'])
")
output=$($CLI authors get --id "$AUTHOR_ID" 2>/dev/null)
assert_json_key "authors get has id" "id" "$output"
assert_json_expr "authors get is Brandon Sanderson" "d['name']=='Brandon Sanderson'" "$output"

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
echo "========================================"
echo "Results: $PASS passed, $FAIL failed"
echo "========================================"

if [ "$FAIL" -gt 0 ]; then
    exit 1
fi
