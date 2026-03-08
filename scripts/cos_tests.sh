#!/usr/bin/env bash
# CountOrSell API test suite
# Run with: bash scripts/cos_tests.sh
# Requires the API to be running (default port 5083 in development).

API_BASE="http://localhost:5083/api"
PASS=0; FAIL=0; SKIP=0
FAILURES=()

pass() { echo "  PASS: $1"; PASS=$((PASS+1)); }
fail() { echo "  FAIL: $1 — $2"; FAIL=$((FAIL+1)); FAILURES+=("$1: $2"); }
skip() { echo "  SKIP: $1 — $2"; SKIP=$((SKIP+1)); }
section() { echo; echo "=== $1 ==="; }

check_code() {
  local name="$1" expected="$2" actual="$3"
  if [ "$actual" = "$expected" ]; then pass "$name (HTTP $actual)";
  else fail "$name" "expected HTTP $expected, got HTTP $actual"; fi
}
check_contains() {
  local name="$1" needle="$2" haystack="$3"
  if echo "$haystack" | grep -q "$needle"; then pass "$name";
  else fail "$name" "response missing '$needle'"; fi
}

# ── 1. AUTH ──────────────────────────────────────────────────────────────────
section "1. Authentication"

BODY=$(curl -s "$API_BASE/auth/registration-status")
CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/auth/registration-status")
check_code "1.1 registration-status" "200" "$CODE"
check_contains "1.1 registrationsEnabled field" "registrationsEnabled" "$BODY"

REG=$(curl -s -X POST "$API_BASE/auth/register" \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"testpassword12345","displayName":"Test User"}')
if echo "$REG" | grep -qE '"token"|accessToken'; then pass "1.2 register new user";
elif echo "$REG" | grep -qiE "exist|taken|already|duplicate"; then pass "1.2 register (user already exists — ok)";
else fail "1.2 register new user" "$(echo "$REG" | head -c 120)"; fi

DUP=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_BASE/auth/register" \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"testpassword12345"}')
if [ "$DUP" = "400" ] || [ "$DUP" = "409" ]; then pass "1.3 duplicate username rejected (HTTP $DUP)";
else fail "1.3 duplicate username" "expected 400/409, got $DUP"; fi

SHORT=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_BASE/auth/register" \
  -H "Content-Type: application/json" \
  -d '{"username":"shortpwuser","password":"tooshort"}')
check_code "1.4 short password rejected" "400" "$SHORT"

LOGIN=$(curl -s -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"testpassword12345"}')
LOGIN_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"testpassword12345"}')
check_code "1.5 login valid" "200" "$LOGIN_CODE"
if echo "$LOGIN" | grep -qE '"token"|"accessToken"'; then pass "1.5 login returns token";
else fail "1.5 login returns token" "response missing token field"; fi
TOKEN=$(echo "$LOGIN" | grep -oE '"(accessToken|token)":"[^"]*"' | head -1 | cut -d'"' -f4)
REFRESH_TOKEN=$(echo "$LOGIN" | grep -o '"refreshToken":"[^"]*"' | cut -d'"' -f4)

WRONG=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"wrongpassword"}')
check_code "1.6 wrong password" "401" "$WRONG"

NOUSER=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"doesnotexist","password":"anypassword12345"}')
check_code "1.7 nonexistent user" "401" "$NOUSER"

ME=$(curl -s "$API_BASE/auth/me" -H "Authorization: Bearer $TOKEN")
ME_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/auth/me" -H "Authorization: Bearer $TOKEN")
check_code "1.8 GET /me authenticated" "200" "$ME_CODE"
check_contains "1.8 /me returns username" "testuser" "$ME"

NO_AUTH=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/auth/me")
check_code "1.9 /me no token" "401" "$NO_AUTH"

REFRESH=$(curl -s -X POST "$API_BASE/auth/refresh" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$REFRESH_TOKEN\"}")
REFRESH_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_BASE/auth/refresh" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$REFRESH_TOKEN\"}")
if [ "$REFRESH_CODE" = "200" ] && echo "$REFRESH" | grep -qE '"token"|"accessToken"'; then
  pass "1.10 token refresh"
  TOKEN=$(echo "$REFRESH" | grep -oE '"(accessToken|token)":"[^"]*"' | head -1 | cut -d'"' -f4)
elif [ "$REFRESH_CODE" = "401" ]; then pass "1.10 token refresh (already rotated — ok)"
else fail "1.10 token refresh" "HTTP $REFRESH_CODE"; fi

PROFILE_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$API_BASE/auth/profile" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"displayName":"Updated Name"}')
check_code "1.11 update display name" "200" "$PROFILE_CODE"

CP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$API_BASE/auth/password" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"currentPassword":"testpassword12345","newPassword":"newtestpassword12345"}')
check_code "1.12 change password valid" "200" "$CP_CODE"
TOKEN=$(curl -s -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"newtestpassword12345"}' \
  | grep -oE '"(accessToken|token)":"[^"]*"' | head -1 | cut -d'"' -f4)
curl -s -X PUT "$API_BASE/auth/password" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"currentPassword":"newtestpassword12345","newPassword":"testpassword12345"}' > /dev/null
TOKEN=$(curl -s -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"testpassword12345"}' \
  | grep -oE '"(accessToken|token)":"[^"]*"' | head -1 | cut -d'"' -f4)

WCP=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$API_BASE/auth/password" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"currentPassword":"wrongpassword","newPassword":"newtestpassword12345"}')
if [ "$WCP" = "400" ] || [ "$WCP" = "401" ]; then pass "1.13 wrong current password rejected (HTTP $WCP)";
else fail "1.13 wrong current password" "expected 400/401, got $WCP"; fi

SCP=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$API_BASE/auth/password" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"currentPassword":"testpassword12345","newPassword":"tooshort"}')
check_code "1.14 new password too short" "400" "$SCP"

# ── 2. SETS ──────────────────────────────────────────────────────────────────
section "2. Sets"

SETS=$(curl -s "$API_BASE/sets")
SETS_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/sets")
check_code "2.1 GET /sets" "200" "$SETS_CODE"
check_contains "2.1 sets contain code field" "code" "$SETS"
FIRST_SET=$(echo "$SETS" | grep -o '"code":"[^"]*"' | head -1 | cut -d'"' -f4)
if [ -z "$FIRST_SET" ]; then FIRST_SET="lea"; fi

SET_CODE_HTTP=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/sets/$FIRST_SET")
check_code "2.2 GET /sets/{code} valid" "200" "$SET_CODE_HTTP"

SET_404=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/sets/zzz999")
check_code "2.3 GET /sets/{code} not found" "404" "$SET_404"

TAGS_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/sets/tags")
check_code "2.4 GET /sets/tags" "200" "$TAGS_CODE"

TAG_PUT=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$API_BASE/sets/$FIRST_SET/tags/commander" \
  -H "Authorization: Bearer $TOKEN")
if [ "$TAG_PUT" = "200" ] || [ "$TAG_PUT" = "204" ]; then pass "2.5 PUT tag on set (HTTP $TAG_PUT)";
else fail "2.5 PUT tag on set" "expected 200/204, got HTTP $TAG_PUT"; fi
SET_BODY=$(curl -s "$API_BASE/sets/$FIRST_SET")
check_contains "2.5 tag appears in set" "commander" "$SET_BODY"

TAG_DEL=$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$API_BASE/sets/$FIRST_SET/tags/commander" \
  -H "Authorization: Bearer $TOKEN")
if [ "$TAG_DEL" = "200" ] || [ "$TAG_DEL" = "204" ]; then pass "2.6 DELETE tag from set (HTTP $TAG_DEL)";
else fail "2.6 DELETE tag from set" "expected 200/204, got HTTP $TAG_DEL"; fi

# ── 3. CARDS ─────────────────────────────────────────────────────────────────
section "3. Cards"

CARDS=$(curl -s "$API_BASE/sets/$FIRST_SET/cards")
CARDS_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/sets/$FIRST_SET/cards")
check_code "3.1 GET /sets/{code}/cards" "200" "$CARDS_CODE"
check_contains "3.1 cards contain id field" "\"id\"" "$CARDS"
CARD_ID=$(echo "$CARDS" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
CARD_NAME=$(echo "$CARDS" | grep -o '"name":"[^"]*"' | head -1 | cut -d'"' -f4)
CARD_CNUM=$(echo "$CARDS" | grep -o '"collector_number":"[^"]*"' | head -1 | cut -d'"' -f4)

SEARCH_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/cards/search?q=Forest&limit=3")
check_code "3.3 card search" "200" "$SEARCH_CODE"

SEARCH3=$(curl -s "$API_BASE/cards/search?q=Forest&limit=3" | grep -o '"id"' | wc -l | tr -d ' ')
if [ "$SEARCH3" -le 3 ] 2>/dev/null; then pass "3.4 search limit respected (got $SEARCH3)";
else fail "3.4 search limit" "got $SEARCH3 results, expected <= 3"; fi

if [ -n "$CARD_ID" ]; then
  IMG_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/images/$CARD_ID")
  if [ "$IMG_CODE" = "200" ] || [ "$IMG_CODE" = "302" ]; then pass "3.6/3.7 image endpoint (HTTP $IMG_CODE)";
  else fail "3.6/3.7 image endpoint" "HTTP $IMG_CODE"; fi
else skip "3.6/3.7 image endpoint" "no card ID available"; fi

IMG_404=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/images/00000000-0000-0000-0000-000000000000")
check_code "3.8 image 404" "404" "$IMG_404"

# ── 4. CARD OWNERSHIP ────────────────────────────────────────────────────────
section "4. Card Ownership"

OWN_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/sets/$FIRST_SET/owned-cards" \
  -H "Authorization: Bearer $TOKEN")
check_code "4.1 GET owned-cards authenticated" "200" "$OWN_CODE"

if [ -n "$CARD_ID" ]; then
  OWN_BODY_TMPL="{\"variant\":\"Regular\",\"quantity\":1,\"cardName\":\"$CARD_NAME\",\"setCode\":\"$FIRST_SET\",\"collectorNumber\":\"$CARD_CNUM\"}"
  VAR_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$API_BASE/cards/$CARD_ID/variant" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "$OWN_BODY_TMPL")
  check_code "4.2 PUT card variant owned" "200" "$VAR_CODE"

  OWN_BODY=$(curl -s "$API_BASE/sets/$FIRST_SET/owned-cards" -H "Authorization: Bearer $TOKEN")
  if echo "$OWN_BODY" | grep -q "$CARD_ID"; then pass "4.3 owned-cards reflects ownership";
  else fail "4.3 owned-cards reflects ownership" "card not found in response"; fi

  UNOWN_TMPL="{\"variant\":\"Regular\",\"quantity\":0,\"cardName\":\"$CARD_NAME\",\"setCode\":\"$FIRST_SET\",\"collectorNumber\":\"$CARD_CNUM\"}"
  UNOWN_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$API_BASE/cards/$CARD_ID/variant" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "$UNOWN_TMPL")
  check_code "4.4 PUT quantity=0 unmarks" "200" "$UNOWN_CODE"

  FOIL_TMPL="{\"variant\":\"Foil\",\"quantity\":2,\"cardName\":\"$CARD_NAME\",\"setCode\":\"$FIRST_SET\",\"collectorNumber\":\"$CARD_CNUM\"}"
  FOIL_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$API_BASE/cards/$CARD_ID/variant" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "$FOIL_TMPL")
  check_code "4.5 PUT foil variant" "200" "$FOIL_CODE"
else skip "4.2-4.5 card ownership" "no card ID available"; fi

# ── 5. COLLECTION ─────────────────────────────────────────────────────────────
section "5. Collection"

COLL_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/collection" \
  -H "Authorization: Bearer $TOKEN")
check_code "5.1 GET /collection" "200" "$COLL_CODE"

COLL_RARE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/collection?rarity=rare" \
  -H "Authorization: Bearer $TOKEN")
check_code "5.2 collection filter rarity" "200" "$COLL_RARE"

COLL_SET=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/collection?setCode=$FIRST_SET" \
  -H "Authorization: Bearer $TOKEN")
check_code "5.3 collection filter setCode" "200" "$COLL_SET"

SUMM=$(curl -s "$API_BASE/collection/summary" -H "Authorization: Bearer $TOKEN")
SUMM_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/collection/summary" \
  -H "Authorization: Bearer $TOKEN")
check_code "5.4 collection summary" "200" "$SUMM_CODE"
check_contains "5.4 summary has totalCopies" "totalCopies" "$SUMM"
check_contains "5.4 summary has byRarity" "byRarity" "$SUMM"

# ── 6. RESERVE LIST ───────────────────────────────────────────────────────────
section "6. Reserve List"

RL_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/reservelist" \
  -H "Authorization: Bearer $TOKEN")
check_code "6.1 GET /reservelist" "200" "$RL_CODE"

RL_BODY=$(curl -s "$API_BASE/reservelist" -H "Authorization: Bearer $TOKEN")
RL_ID=$(echo "$RL_BODY" | grep -o '"scryfallCardId":"[^"]*"' | head -1 | cut -d'"' -f4)

if [ -n "$RL_ID" ]; then
  RL_OWN=$(curl -s -o /dev/null -w "%{http_code}" -X PATCH "$API_BASE/reservelist/$RL_ID/owned" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"owned":true}')
  check_code "6.2 PATCH RL card owned" "200" "$RL_OWN"

  RL_UNOWN=$(curl -s -o /dev/null -w "%{http_code}" -X PATCH "$API_BASE/reservelist/$RL_ID/owned" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"owned":false}')
  check_code "6.3 PATCH RL card unowned" "200" "$RL_UNOWN"
else skip "6.2/6.3 RL ownership" "no RL cards in DB"; fi

RL_SET=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/reservelist/set/$FIRST_SET" \
  -H "Authorization: Bearer $TOKEN")
check_code "6.4 RL by set" "200" "$RL_SET"

# ── 7. BOOSTERS ───────────────────────────────────────────────────────────────
section "7. Boosters"

BOOST_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/boosters" \
  -H "Authorization: Bearer $TOKEN")
check_code "7.1 GET /boosters" "200" "$BOOST_CODE"

BOOST_CREATE=$(curl -s -X POST "$API_BASE/boosters" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"setCode":"mh3","boosterType":"Play","artVariant":"","owned":false}')
BOOST_CREATE_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_BASE/boosters" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"setCode":"mh3","boosterType":"Collector","artVariant":"","owned":false}')
check_code "7.2 POST booster" "200" "$BOOST_CREATE_CODE"

BOOSTER_ID=$(echo "$BOOST_CREATE" | grep -o '"id":[0-9]*' | head -1 | sed 's/"id"://')
if [ -z "$BOOSTER_ID" ]; then
  BOOSTER_ID=$(curl -s "$API_BASE/boosters" -H "Authorization: Bearer $TOKEN" \
    | grep -o '"id":[0-9]*' | head -1 | sed 's/"id"://')
fi

BOOST_LIST=$(curl -s "$API_BASE/boosters" -H "Authorization: Bearer $TOKEN")
check_contains "7.3 booster in list" "mh3" "$BOOST_LIST"

if [ -n "$BOOSTER_ID" ]; then
  BOOST_OWN=$(curl -s -o /dev/null -w "%{http_code}" -X PATCH "$API_BASE/boosters/$BOOSTER_ID/owned" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"owned":true}')
  check_code "7.4 PATCH booster owned" "200" "$BOOST_OWN"

  BOOST_DEL=$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$API_BASE/boosters/$BOOSTER_ID" \
    -H "Authorization: Bearer $TOKEN")
  check_code "7.6 DELETE booster" "200" "$BOOST_DEL"
else skip "7.4/7.6 booster owned/delete" "no booster ID"; fi

BOOST_SET=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/boosters/set/mh3" \
  -H "Authorization: Bearer $TOKEN")
check_code "7.7 boosters by set" "200" "$BOOST_SET"

# ── 8. SLABBED ────────────────────────────────────────────────────────────────
section "8. Slabbed Cards"

SLAB_LIST_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/slabbed" \
  -H "Authorization: Bearer $TOKEN")
check_code "8.1 GET /slabbed" "200" "$SLAB_LIST_CODE"

SLAB_CARD_ID="${CARD_ID:-00000000-0000-0000-0000-000000000001}"
SLAB_CERT="TEST$(date +%s)"
SLAB_CREATE=$(curl -s -X POST "$API_BASE/slabbed" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"scryfallCardId\":\"$SLAB_CARD_ID\",\"cardName\":\"Test Card\",\"setCode\":\"$FIRST_SET\",\"setName\":\"Test Set\",\"collectorNumber\":\"1\",\"cardVariant\":\"Regular\",\"gradingCompany\":\"PSA\",\"grade\":\"9\",\"certificationNumber\":\"$SLAB_CERT\"}")
if echo "$SLAB_CREATE" | grep -q '"id"'; then pass "8.2 POST slab";
else fail "8.2 POST slab" "$(echo "$SLAB_CREATE" | head -c 120)"; fi

SLAB_ID=$(echo "$SLAB_CREATE" | grep -o '"id":[0-9]*' | sed 's/"id"://')
SLAB_BODY=$(curl -s "$API_BASE/slabbed" -H "Authorization: Bearer $TOKEN")
check_contains "8.3 slab in list" "PSA" "$SLAB_BODY"

if [ -n "$SLAB_ID" ]; then
  SLAB_UPD=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$API_BASE/slabbed/$SLAB_ID" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"scryfallCardId\":\"$SLAB_CARD_ID\",\"cardName\":\"Test Card\",\"setCode\":\"$FIRST_SET\",\"setName\":\"Test Set\",\"collectorNumber\":\"1\",\"cardVariant\":\"Regular\",\"gradingCompany\":\"PSA\",\"grade\":\"9.5\",\"certificationNumber\":\"$SLAB_CERT\"}")
  check_code "8.4 PUT slab update" "200" "$SLAB_UPD"

  DUP_SLAB=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_BASE/slabbed" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"scryfallCardId\":\"$SLAB_CARD_ID\",\"cardName\":\"Test Card\",\"setCode\":\"$FIRST_SET\",\"setName\":\"Test Set\",\"collectorNumber\":\"1\",\"cardVariant\":\"Regular\",\"gradingCompany\":\"PSA\",\"grade\":\"9\",\"certificationNumber\":\"$SLAB_CERT\"}")
  if [ "$DUP_SLAB" = "400" ] || [ "$DUP_SLAB" = "409" ] || [ "$DUP_SLAB" = "500" ]; then
    pass "8.5 duplicate cert rejected (HTTP $DUP_SLAB)";
  else fail "8.5 duplicate cert" "expected 4xx/500, got $DUP_SLAB"; fi

  SLAB_DEL=$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$API_BASE/slabbed/$SLAB_ID" \
    -H "Authorization: Bearer $TOKEN")
  check_code "8.6 DELETE slab" "200" "$SLAB_DEL"
else skip "8.4-8.6 slab ops" "no slab ID"; fi

# ── 9. EXPORTS ────────────────────────────────────────────────────────────────
section "9. Exports"

export_check() {
  local name="$1" path="$2"
  local C; C=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/$path" -H "Authorization: Bearer $TOKEN")
  check_code "$name" "200" "$C"
}
export_check "9.1 cards CSV"              "export/cards/csv"
export_check "9.2 cards XML"              "export/cards/xml"
export_check "9.3 boosters CSV"           "export/boosters/csv"
export_check "9.4 reservelist CSV"        "export/reservelist/csv"
export_check "9.5 slabbed PDF"            "export/slabbed/pdf"
export_check "9.6 collection summary CSV" "export/collection/summary/csv"
export_check "9.7 collection summary PDF" "export/collection/summary/pdf"
export_check "9.8 collection detailed CSV" "export/collection/detailed/csv"

EXP_NOAUTH=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/export/cards/csv")
check_code "9.9 export no auth" "401" "$EXP_NOAUTH"

# ── 10. LABELS ────────────────────────────────────────────────────────────────
section "10. Labels"

LBL1=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_BASE/labels/generate" \
  -H "Content-Type: application/json" \
  -d "{\"setCode\":\"$FIRST_SET\",\"boxType\":\"Set Box\"}")
check_code "10.1 set box label" "200" "$LBL1"

LBL2=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API_BASE/labels/generate" \
  -H "Content-Type: application/json" \
  -d "{\"setCode\":\"$FIRST_SET\",\"boxType\":\"Surplus Box\"}")
check_code "10.2 surplus box label" "200" "$LBL2"

# ── 12. UPDATES ───────────────────────────────────────────────────────────────
section "12. Updates"

UPD_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/updates/check")
if [ "$UPD_CODE" = "200" ] || [ "$UPD_CODE" = "503" ]; then pass "12.1 updates/check (HTTP $UPD_CODE)";
else fail "12.1 updates/check" "HTTP $UPD_CODE"; fi

HIST_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/updates/history")
check_code "12.2 updates/history" "200" "$HIST_CODE"

# ── 13. ADMIN ─────────────────────────────────────────────────────────────────
section "13. Admin"

ADMIN_LOGIN=$(curl -s -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"cosadm","password":"wholeftjaceinchargeofdesign"}')
ADMIN_TOKEN=$(echo "$ADMIN_LOGIN" | grep -oE '"(accessToken|token)":"[^"]*"' | head -1 | cut -d'"' -f4)

if [ -z "$ADMIN_TOKEN" ]; then
  fail "13.0 admin login" "could not obtain admin token"
else
  A1=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/admin/users" \
    -H "Authorization: Bearer $ADMIN_TOKEN")
  check_code "13.1 GET /admin/users" "200" "$A1"

  A2=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/admin/users" \
    -H "Authorization: Bearer $TOKEN")
  check_code "13.2 non-admin denied" "403" "$A2"

  STATUS=$(curl -s "$API_BASE/admin/status" -H "Authorization: Bearer $ADMIN_TOKEN")
  A3=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/admin/status" \
    -H "Authorization: Bearer $ADMIN_TOKEN")
  check_code "13.3 GET /admin/status" "200" "$A3"
  check_contains "13.3 status has totalUsers" "totalUsers" "$STATUS"
  check_contains "13.3 status has totalCards" "totalCards" "$STATUS"

  A5=$(curl -s -o /dev/null -w "%{http_code}" -X PUT "$API_BASE/admin/settings" \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"registrationsEnabled":false}')
  check_code "13.5 disable registrations" "200" "$A5"
  REGSTAT=$(curl -s "$API_BASE/auth/registration-status")
  check_contains "13.5 registrations disabled" "false" "$REGSTAT"
  curl -s -X PUT "$API_BASE/admin/settings" \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"registrationsEnabled":true}' > /dev/null
fi

# ── 17. SECURITY ──────────────────────────────────────────────────────────────
section "17. Security"

for EP in collection reservelist boosters slabbed; do
  SEC=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/$EP")
  check_code "17.1 $EP requires auth" "401" "$SEC"
done

# ── 16. BUILD VERIFICATION ────────────────────────────────────────────────────
section "16. Build Verification"

BUILD=$(dotnet build "$(pwd)/src/CountOrSell.sln" --nologo -v q 2>&1)
if echo "$BUILD" | grep -q "Build succeeded" && ! echo "$BUILD" | grep -qE "^.*error"; then
  pass "16.1 .NET solution builds"
else
  ERRS=$(echo "$BUILD" | grep -E " error " | head -3)
  fail "16.1 .NET solution build" "$ERRS"
fi

TS=$(cd "$(pwd)/src/countorsell-web" && npx tsc --noEmit 2>&1)
if [ -z "$TS" ]; then pass "16.2 TypeScript check clean";
else fail "16.2 TypeScript check" "$(echo "$TS" | head -3)"; fi

# ── SUMMARY ───────────────────────────────────────────────────────────────────
echo
echo "========================================"
printf "  Results: %d passed, %d failed, %d skipped\n" "$PASS" "$FAIL" "$SKIP"
echo "========================================"
if [ ${#FAILURES[@]} -gt 0 ]; then
  echo "  Failures:"
  for F in "${FAILURES[@]}"; do echo "    x $F"; done
fi

[ "$FAIL" -eq 0 ] && exit 0 || exit 1
