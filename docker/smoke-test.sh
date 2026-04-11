#!/usr/bin/env bash
# Smoke test: exercises every abs-cli command against a live ABS instance.
# Tests the actual binary (AOT or JIT) — not `dotnet run`.
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
           "search"; do
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

# config get — should return JSON with expected keys
output=$($CLI config get 2>/dev/null)
assert_json_key "config get returns JSON" "server" "$output"
assert_json_key "config get has configPath" "configPath" "$output"

# config set + get roundtrip
$CLI config set server "http://smoke-test.example.com" 2>&1
output=$($CLI config get 2>/dev/null)
if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['server']=='http://smoke-test.example.com'" 2>/dev/null; then
    pass "config set/get roundtrip"
else
    fail "config set/get roundtrip" "server value not persisted"
fi

# Restore for subsequent tests
$CLI config set server "$ABS_URL" 2>&1

# ============================================================
echo ""
echo "=== Libraries Commands ==="
# ============================================================

output=$($CLI libraries list 2>/dev/null)
assert_json_key "libraries list returns JSON" "libraries" "$output"

if LIB_COUNT=$(echo "$output" | python3 -c "import sys,json; print(len(json.load(sys.stdin)['libraries']))" 2>/dev/null) && [ "$LIB_COUNT" -gt 0 ]; then
    pass "libraries list has entries ($LIB_COUNT)"
else
    fail "libraries list has entries" "count=${LIB_COUNT:-?}"
fi

output=$($CLI libraries get --id "$LIB_ID" 2>/dev/null)
assert_json_key "libraries get returns library" "name" "$output"

if echo "$output" | python3 -c "import sys,json; d=json.load(sys.stdin); assert d['id']=='$LIB_ID'" 2>/dev/null; then
    pass "libraries get returns correct library"
else
    fail "libraries get returns correct library" "id mismatch"
fi

# ============================================================
echo ""
echo "=== Items Commands ==="
# ============================================================

output=$($CLI items list --limit 5 2>/dev/null)
assert_json_key "items list returns paginated" "results" "$output"
assert_json_key "items list has total" "total" "$output"

output=$($CLI items search --query "nonexistent_book_xyz" 2>/dev/null)
assert_json_key "items search returns result" "book" "$output"

# ============================================================
echo ""
echo "=== Series Commands ==="
# ============================================================

output=$($CLI series list 2>/dev/null)
assert_json_key "series list returns paginated" "results" "$output"
assert_json_key "series list has total" "total" "$output"

# ============================================================
echo ""
echo "=== Authors Commands ==="
# ============================================================

output=$($CLI authors list 2>/dev/null)
assert_json_key "authors list returns JSON" "authors" "$output"

# ============================================================
echo ""
echo "=== Search Command ==="
# ============================================================

output=$($CLI search --query "test" 2>/dev/null)
assert_json_key "search returns result" "book" "$output"
assert_json_key "search has authors key" "authors" "$output"

# ============================================================
echo ""
echo "========================================"
echo "Results: $PASS passed, $FAIL failed"
echo "========================================"

if [ "$FAIL" -gt 0 ]; then
    exit 1
fi
