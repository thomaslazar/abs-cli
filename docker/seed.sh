#!/usr/bin/env bash
set -euo pipefail

ABS_URL="${ABS_URL:-http://localhost:13378}"
MAX_WAIT=30

echo "Waiting for ABS to start..."
for i in $(seq 1 $MAX_WAIT); do
    if curl -sf "$ABS_URL/healthcheck" > /dev/null 2>&1; then
        echo "ABS is ready."
        break
    fi
    if [ "$i" -eq "$MAX_WAIT" ]; then
        echo "ABS did not start within ${MAX_WAIT}s"
        exit 1
    fi
    sleep 1
done

# Initialize server if needed (first run)
echo "Initializing server..."
curl -sf -X POST "$ABS_URL/init" \
    -H 'Content-Type: application/json' \
    -d '{"newRoot":{"username":"root","password":"root"}}' \
    > /dev/null 2>&1 || true

# Login as root (X-Return-Tokens for proper accessToken, not legacy user.token)
echo "Logging in..."
TOKEN=$(curl -sf -X POST "$ABS_URL/login" \
    -H 'Content-Type: application/json' \
    -H 'X-Return-Tokens: true' \
    -d '{"username":"root","password":"root"}' \
    | python3 -c "import sys,json; print(json.load(sys.stdin)['user']['accessToken'])")

AUTH="Authorization: Bearer $TOKEN"

# Create test library
echo "Creating test library..."
LIBRARY_ID=$(curl -sf -X POST "$ABS_URL/api/libraries" \
    -H "$AUTH" \
    -H 'Content-Type: application/json' \
    -d '{
        "name": "Test Library",
        "folders": [{"fullPath": "/audiobooks"}],
        "mediaType": "book",
        "provider": "google"
    }' | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")

echo "Library created: $LIBRARY_ID"

# Create a test user with limited permissions
echo "Creating test user..."
curl -sf -X POST "$ABS_URL/api/users" \
    -H "$AUTH" \
    -H 'Content-Type: application/json' \
    -d '{
        "username": "testuser",
        "password": "testpass",
        "type": "user",
        "permissions": {
            "download": true,
            "update": true,
            "delete": false,
            "upload": false,
            "accessAllLibraries": true,
            "accessAllTags": true,
            "accessExplicitContent": true
        }
    }' > /dev/null 2>&1 || true

echo "Seed complete."
echo "ABS_URL=$ABS_URL"
echo "LIBRARY_ID=$LIBRARY_ID"
echo "Root credentials: root/root"
echo "Test credentials: testuser/testpass"
