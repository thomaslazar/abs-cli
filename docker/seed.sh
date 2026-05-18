#!/usr/bin/env bash
set -euo pipefail

ABS_URL="${ABS_URL:-http://host.docker.internal:13378}"
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
LIBRARY_RESPONSE=$(curl -sf -X POST "$ABS_URL/api/libraries" \
    -H "$AUTH" \
    -H 'Content-Type: application/json' \
    -d '{
        "name": "Test Library",
        "folders": [{"fullPath": "/audiobooks"}],
        "mediaType": "book",
        "provider": "google"
    }')

LIBRARY_ID=$(echo "$LIBRARY_RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
FOLDER_ID=$(echo "$LIBRARY_RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin)['folders'][0]['id'])")
echo "Library: $LIBRARY_ID  Folder: $FOLDER_ID"

# Create a test user with limited permissions
echo "Creating test user..."
curl -sf -X POST "$ABS_URL/api/users" \
    -H "$AUTH" \
    -H 'Content-Type: application/json' \
    -d '{
        "username": "testuser",
        "password": "testpass",
        "type": "user",
        "isActive": true,
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

# Create upload test user (can upload but not delete)
echo "Creating upload test user..."
curl -sf -X POST "$ABS_URL/api/users" \
    -H "$AUTH" \
    -H 'Content-Type: application/json' \
    -d '{
        "username": "uploaduser",
        "password": "uploadpass",
        "type": "user",
        "isActive": true,
        "permissions": {
            "download": true,
            "update": true,
            "delete": false,
            "upload": true,
            "accessAllLibraries": true,
            "accessAllTags": true,
            "accessExplicitContent": true
        }
    }' > /dev/null 2>&1 || true

# Create read-only test user (no update/upload/delete) so smoke tests
# can exercise 'canUpdate' permission denial on items update, authors
# update, items batch-update, and items chapters set.
echo "Creating read-only test user..."
curl -sf -X POST "$ABS_URL/api/users" \
    -H "$AUTH" \
    -H 'Content-Type: application/json' \
    -d '{
        "username": "readonlyuser",
        "password": "readonlypass",
        "type": "user",
        "isActive": true,
        "permissions": {
            "download": true,
            "update": false,
            "delete": false,
            "upload": false,
            "accessAllLibraries": true,
            "accessAllTags": true,
            "accessExplicitContent": true
        }
    }' > /dev/null 2>&1 || true

# --- Upload test audiobooks ---
# Create a tiny silent MP3 (1 second) for uploads
TMPDIR=$(mktemp -d)
python3 -c "
# Minimal valid MP3 frame (MPEG1 Layer3 128kbps 44100Hz stereo, silence)
import struct, sys
# MP3 frame header: sync=0xFFE0, MPEG1, Layer3, 128kbps, 44100Hz, stereo
header = bytes([0xFF, 0xFB, 0x90, 0x00])
# Pad to one full frame (417 bytes for 128kbps/44100Hz)
frame = header + b'\x00' * 413
# Write ~1 second worth of frames (about 38 frames)
with open('$TMPDIR/silence.mp3', 'wb') as f:
    for _ in range(38):
        f.write(frame)
"

upload_book() {
    local title="$1" author="$2" series="${3:-}"
    echo "  Uploading: $author — $title${series:+ ($series)}"
    curl -sf -X POST "$ABS_URL/api/upload" \
        -H "$AUTH" \
        -F "title=$title" \
        -F "author=$author" \
        ${series:+-F "series=$series"} \
        -F "library=$LIBRARY_ID" \
        -F "folder=$FOLDER_ID" \
        -F "0=@$TMPDIR/silence.mp3;filename=audiobook.mp3" \
        > /dev/null
}

echo ""
echo "Uploading test audiobooks..."

# Brandon Sanderson — Mistborn series + standalone
upload_book "The Final Empire"          "Brandon Sanderson" "Mistborn"
upload_book "The Well of Ascension"     "Brandon Sanderson" "Mistborn"
upload_book "The Hero of Ages"          "Brandon Sanderson" "Mistborn"
upload_book "Warbreaker"                "Brandon Sanderson"

# Jim Butcher — Dresden Files series
upload_book "Storm Front"               "Jim Butcher"       "The Dresden Files"
upload_book "Fool Moon"                 "Jim Butcher"       "The Dresden Files"
upload_book "Grave Peril"               "Jim Butcher"       "The Dresden Files"

# Ben Aaronovitch — Rivers of London series
upload_book "Rivers of London"          "Ben Aaronovitch"   "Rivers of London"
upload_book "Moon Over Soho"            "Ben Aaronovitch"   "Rivers of London"

# Naomi Novik — standalone novels
upload_book "Uprooted"                  "Naomi Novik"
upload_book "Spinning Silver"           "Naomi Novik"

# John Scalzi — standalone novels
upload_book "Old Man's War"             "John Scalzi"
upload_book "Redshirts"                 "John Scalzi"

# Cory Doctorow — standalone novels
upload_book "Little Brother"            "Cory Doctorow"
upload_book "Walkaway"                  "Cory Doctorow"

rm -rf "$TMPDIR"

# Trigger library scan to create items from uploaded files
echo ""
echo "Scanning library..."
curl -sf -X POST "$ABS_URL/api/libraries/$LIBRARY_ID/scan" \
    -H "$AUTH" > /dev/null

# Wait for scan to complete
echo "Waiting for scan..."
for i in $(seq 1 30); do
    ITEM_COUNT=$(curl -sf "$ABS_URL/api/libraries/$LIBRARY_ID/items?limit=0" \
        -H "$AUTH" \
        | python3 -c "import sys,json; print(json.load(sys.stdin)['total'])")
    if [ "$ITEM_COUNT" -ge 15 ]; then
        echo "Scan complete: $ITEM_COUNT items"
        break
    fi
    if [ "$i" -eq 30 ]; then
        echo "Warning: scan may not be complete (found $ITEM_COUNT items, expected 15)"
    fi
    sleep 1
done

# --- Multi-ebook fixture (for items toggle-ebook-status smoke) ---
echo ""
echo "Creating multi-ebook fixture..."

EBOOK_TMP=$(mktemp -d)

# Minimal valid EPUB: ZIP with mimetype, container.xml, content.opf, one xhtml.
python3 <<EOF
import os, zipfile
os.makedirs('$EBOOK_TMP/epub-build/META-INF', exist_ok=True)
with open('$EBOOK_TMP/epub-build/mimetype', 'w') as f:
    f.write('application/epub+zip')
with open('$EBOOK_TMP/epub-build/META-INF/container.xml', 'w') as f:
    f.write('<?xml version="1.0"?><container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container"><rootfiles><rootfile full-path="content.opf" media-type="application/oebps-package+xml"/></rootfiles></container>')
with open('$EBOOK_TMP/epub-build/content.opf', 'w') as f:
    f.write('<?xml version="1.0"?><package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="id"><metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="id">test</dc:identifier><dc:title>Multi Ebook Test</dc:title><dc:language>en</dc:language></metadata><manifest><item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/></manifest><spine><itemref idref="ch1"/></spine></package>')
with open('$EBOOK_TMP/epub-build/ch1.xhtml', 'w') as f:
    f.write('<?xml version="1.0"?><!DOCTYPE html><html xmlns="http://www.w3.org/1999/xhtml"><head><title>Ch1</title></head><body><p>Ch1.</p></body></html>')
with zipfile.ZipFile('$EBOOK_TMP/multi.epub', 'w') as z:
    z.write('$EBOOK_TMP/epub-build/mimetype', 'mimetype', compress_type=zipfile.ZIP_STORED)
    z.write('$EBOOK_TMP/epub-build/META-INF/container.xml', 'META-INF/container.xml')
    z.write('$EBOOK_TMP/epub-build/content.opf', 'content.opf')
    z.write('$EBOOK_TMP/epub-build/ch1.xhtml', 'ch1.xhtml')
EOF

# Minimal PDF: enough raw PDF objects for ABS's scanner to detect.
python3 <<EOF
with open('$EBOOK_TMP/multi.pdf', 'wb') as f:
    f.write(b'%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]>>endobj\nxref\n0 4\n0000000000 65535 f\n0000000009 00000 n\n0000000054 00000 n\n0000000098 00000 n\ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n147\n%%EOF\n')
EOF

# Upload both ebooks as a single library item via the upload endpoint.
curl -sf -X POST "$ABS_URL/api/upload" \
    -H "$AUTH" \
    -F "library=$LIBRARY_ID" \
    -F "folder=$FOLDER_ID" \
    -F "title=Multi Ebook Test" \
    -F "author=Ebook Smoke Author" \
    -F "0=@$EBOOK_TMP/multi.epub" \
    -F "1=@$EBOOK_TMP/multi.pdf" \
    > /dev/null 2>&1 || echo "  (warning: multi-ebook upload may have failed; check ABS logs)"

rm -rf "$EBOOK_TMP"

# Trigger another scan to pick up the ebook item.
curl -sf -X POST "$ABS_URL/api/libraries/$LIBRARY_ID/scan" -H "$AUTH" > /dev/null
# Wait for scan to add a 16th item.
for i in $(seq 1 20); do
    ITEM_COUNT=$(curl -sf "$ABS_URL/api/libraries/$LIBRARY_ID/items?limit=0" -H "$AUTH" \
        | python3 -c "import sys,json; print(json.load(sys.stdin).get('total',0))" 2>/dev/null)
    if [ "${ITEM_COUNT:-0}" -ge 16 ]; then
        echo "Multi-ebook fixture indexed (total items: $ITEM_COUNT)"
        break
    fi
    sleep 1
done

echo ""
echo "Seed complete."
echo "ABS_URL=$ABS_URL"
echo "LIBRARY_ID=$LIBRARY_ID"
echo "Items: $ITEM_COUNT (6 authors, 3 series, 15 books)"
echo "Root credentials: root/root"
echo "Test credentials: testuser/testpass, uploaduser/uploadpass, readonlyuser/readonlypass"
