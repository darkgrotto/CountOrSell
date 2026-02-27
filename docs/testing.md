# CountOrSell Test Plan

This document defines the full manual and automated test suite for CountOrSell. It is intended to be used by Claude Code (or a human tester) to verify the application is working correctly after changes.

Each test includes: preconditions, steps, and expected output. Tests marked **[AUTH]** require a valid JWT bearer token. Tests marked **[ADMIN]** require an admin-level token.

---

## Setup

### Prerequisites
- .NET 8 SDK installed (`dotnet --version` → `8.x.x`)
- Node.js 18+ installed (`node --version` → `v18.x` or higher)
- API running on `http://localhost:5000` (Development mode for Swagger access)
- Frontend running on `http://localhost:5173`
- Database has been seeded with at least one set's cards (`sync --set-code` for a small set such as `lea`)

### Default admin credentials
| Username | Password |
|----------|----------|
| `cosadm` | `wholeftjaceinchargeofdesign` |

### Test user to create
| Username | Password | Display Name |
|----------|----------|--------------|
| `testuser` | `testpassword12345` | `Test User` |

### Base URL
```
API_BASE=http://localhost:5000/api
```

All `curl` examples below assume `API_BASE` is set and (where required) `TOKEN` holds a valid JWT bearer token obtained from the login endpoint.

---

## 1. Authentication

### 1.1 GET /api/auth/registration-status — no auth
**Expected:** HTTP 200
```json
{ "registrationsEnabled": true }
```

```bash
curl -s $API_BASE/auth/registration-status
```

---

### 1.2 POST /api/auth/register — valid registration
**Precondition:** Registration is enabled.

```bash
curl -s -X POST $API_BASE/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"testpassword12345","displayName":"Test User"}'
```

**Expected:** HTTP 200, body contains:
```json
{
  "accessToken": "<jwt>",
  "refreshToken": "<token>",
  "username": "testuser",
  "displayName": "Test User",
  "isAdmin": false
}
```

---

### 1.3 POST /api/auth/register — duplicate username
```bash
curl -s -X POST $API_BASE/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"testpassword12345"}'
```

**Expected:** HTTP 400 or 409, error message indicating username already exists.

---

### 1.4 POST /api/auth/register — password too short (< 15 chars)
```bash
curl -s -X POST $API_BASE/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"shortpwuser","password":"tooshort"}'
```

**Expected:** HTTP 400, error message about minimum password length.

---

### 1.5 POST /api/auth/login — valid credentials
```bash
curl -s -X POST $API_BASE/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"testpassword12345"}'
```

**Expected:** HTTP 200, body matches 1.2 structure. Save `accessToken` as `TOKEN` and `refreshToken` as `REFRESH_TOKEN` for subsequent tests.

---

### 1.6 POST /api/auth/login — wrong password
```bash
curl -s -X POST $API_BASE/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"wrongpassword"}'
```

**Expected:** HTTP 401.

---

### 1.7 POST /api/auth/login — nonexistent user
```bash
curl -s -X POST $API_BASE/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"doesnotexist","password":"anypassword12345"}'
```

**Expected:** HTTP 401.

---

### 1.8 GET /api/auth/me — authenticated [AUTH]
```bash
curl -s $API_BASE/auth/me \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200
```json
{
  "username": "testuser",
  "displayName": "Test User",
  "isAdmin": false
}
```

---

### 1.9 GET /api/auth/me — no token
```bash
curl -s $API_BASE/auth/me
```

**Expected:** HTTP 401.

---

### 1.10 POST /api/auth/refresh — valid refresh token
```bash
curl -s -X POST $API_BASE/auth/refresh \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$REFRESH_TOKEN\"}"
```

**Expected:** HTTP 200, new `accessToken` and `refreshToken`. Old refresh token should be invalid after this.

---

### 1.11 PUT /api/auth/profile — update display name [AUTH]
```bash
curl -s -X PUT $API_BASE/auth/profile \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"displayName":"Updated Name"}'
```

**Expected:** HTTP 200. Subsequent `GET /api/auth/me` returns `"displayName": "Updated Name"`.

---

### 1.12 PUT /api/auth/password — valid change [AUTH]
```bash
curl -s -X PUT $API_BASE/auth/password \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"currentPassword":"testpassword12345","newPassword":"newtestpassword12345"}'
```

**Expected:** HTTP 200. Old password no longer works for login; new password does.

*Reset the password back to `testpassword12345` before continuing.*

---

### 1.13 PUT /api/auth/password — wrong current password [AUTH]
```bash
curl -s -X PUT $API_BASE/auth/password \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"currentPassword":"wrongpassword","newPassword":"newtestpassword12345"}'
```

**Expected:** HTTP 400 or 401.

---

### 1.14 PUT /api/auth/password — new password too short [AUTH]
```bash
curl -s -X PUT $API_BASE/auth/password \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"currentPassword":"testpassword12345","newPassword":"tooshort"}'
```

**Expected:** HTTP 400, error about minimum password length (15 characters).

---

## 2. Sets

### 2.1 GET /api/sets — returns set list
```bash
curl -s $API_BASE/sets
```

**Expected:** HTTP 200, JSON array. Each item contains `code`, `name`, `setType`, `cardCount`, `iconSvgUri`. At least one set is present if data has been synced.

---

### 2.2 GET /api/sets/{setCode} — valid set
```bash
curl -s $API_BASE/sets/lea
```

**Expected:** HTTP 200, object with `code: "lea"`, `name: "Limited Edition Alpha"` (or whichever set was synced).

---

### 2.3 GET /api/sets/{setCode} — nonexistent set
```bash
curl -s $API_BASE/sets/zzz999
```

**Expected:** HTTP 404.

---

### 2.4 GET /api/sets/tags — returns tag list
```bash
curl -s $API_BASE/sets/tags
```

**Expected:** HTTP 200, JSON array of strings (e.g. `["commander","masters","core","draft-innovation"]`).

---

### 2.5 PUT /api/sets/{setCode}/tags/{tag} — add tag [AUTH]
```bash
curl -s -X PUT $API_BASE/sets/lea/tags/testTag \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200. Subsequent `GET /api/sets/lea` includes `testTag` in its `tags` array.

---

### 2.6 DELETE /api/sets/{setCode}/tags/{tag} — remove tag [AUTH]
```bash
curl -s -X DELETE $API_BASE/sets/lea/tags/testTag \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200. Subsequent `GET /api/sets/lea` does not include `testTag`.

---

## 3. Cards

### 3.1 GET /api/sets/{setCode}/cards — returns card list
```bash
curl -s $API_BASE/sets/lea/cards
```

**Expected:** HTTP 200, JSON array. Each card contains: `id`, `name`, `setCode`, `collectorNumber`, `rarity`, `priceUsd` (nullable), `isReserved` (boolean). Array is ordered by collector number. Minimum 1 result if set was synced.

---

### 3.2 GET /api/sets/{setCode}/cards — empty or unsynced set
```bash
curl -s $API_BASE/sets/zzz999/cards
```

**Expected:** HTTP 200 with empty array `[]`, or HTTP 404.

---

### 3.3 GET /api/cards/search — basic search
```bash
curl -s "$API_BASE/cards/search?q=Black+Lotus"
```

**Expected:** HTTP 200, array containing at least one card with `name` containing "Black Lotus" (if LEA is synced).

---

### 3.4 GET /api/cards/search — limit parameter
```bash
curl -s "$API_BASE/cards/search?q=Forest&limit=3"
```

**Expected:** HTTP 200, array with at most 3 results.

---

### 3.5 GET /api/cards/search — empty query
```bash
curl -s "$API_BASE/cards/search?q="
```

**Expected:** HTTP 200 with empty array, or HTTP 400.

---

### 3.6 GET /api/images/{cardId} — cached image
**Precondition:** Card has `LocalImagePath` set in database (images have been downloaded).

```bash
curl -s -o /dev/null -w "%{http_code} %{content_type}" \
  $API_BASE/images/<valid-scryfall-card-id>
```

**Expected:** HTTP 200, `Content-Type: image/jpeg`.

---

### 3.7 GET /api/images/{cardId} — no cached image, redirects to Scryfall
**Precondition:** Card exists in database but has no `LocalImagePath`.

**Expected:** HTTP 302 redirect to a Scryfall image URL, or HTTP 200 with image data, or HTTP 404.

---

### 3.8 GET /api/images/{cardId} — nonexistent card
```bash
curl -s -o /dev/null -w "%{http_code}" \
  $API_BASE/images/00000000-0000-0000-0000-000000000000
```

**Expected:** HTTP 404.

---

## 4. Card Ownership

### 4.1 GET /api/sets/{setCode}/owned-cards — returns empty for new user [AUTH]
```bash
curl -s $API_BASE/sets/lea/owned-cards \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, empty object `{}` or empty array. No cards owned yet.

---

### 4.2 PUT /api/cards/{scryfallCardId}/variant — mark card owned [AUTH]
```bash
# Get a card ID first
CARD_ID=$(curl -s "$API_BASE/cards/search?q=Black+Lotus&limit=1" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)

curl -s -X PUT "$API_BASE/cards/$CARD_ID/variant" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"variant":"Regular","quantity":1}'
```

**Expected:** HTTP 200, body: `{"scryfallCardId":"<id>","variant":"Regular","quantity":1}`.

---

### 4.3 GET /api/sets/{setCode}/owned-cards — reflects ownership [AUTH]
```bash
curl -s $API_BASE/sets/lea/owned-cards \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, contains an entry for the card set in 4.2 with `quantity: 1` and `variant: "Regular"`.

---

### 4.4 PUT /api/cards/{scryfallCardId}/variant — set quantity to 0 (unmark) [AUTH]
```bash
curl -s -X PUT "$API_BASE/cards/$CARD_ID/variant" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"variant":"Regular","quantity":0}'
```

**Expected:** HTTP 200. Subsequent owned-cards call returns 0 or omits the card.

---

### 4.5 PUT /api/cards/{scryfallCardId}/variant — foil variant [AUTH]
```bash
curl -s -X PUT "$API_BASE/cards/$CARD_ID/variant" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"variant":"Foil","quantity":2}'
```

**Expected:** HTTP 200, `{"variant":"Foil","quantity":2}`.

---

### 4.6 POST /api/cards/bulk-owned — mark multiple cards [AUTH]
```bash
curl -s -X POST $API_BASE/cards/bulk-owned \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "setCode": "lea",
    "cards": [
      {"scryfallCardId":"<id1>","cardName":"Card 1","collectorNumber":"1"},
      {"scryfallCardId":"<id2>","cardName":"Card 2","collectorNumber":"2"}
    ],
    "owned": true
  }'
```

**Expected:** HTTP 200. Both cards now appear in `GET /api/sets/lea/owned-cards` with `quantity >= 1`.

---

### 4.7 POST /api/cards/bulk-owned — unmark [AUTH]
Same as 4.6 but `"owned": false`.

**Expected:** HTTP 200. Cards no longer appear as owned.

---

## 5. Collection

### 5.1 GET /api/collection — returns owned cards [AUTH]
**Precondition:** At least one card is marked as owned (from section 4 tests).

```bash
curl -s $API_BASE/collection \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, array of `CollectionCardEntry` objects. Each entry has: `scryfallCardId`, `cardName`, `setCode`, `setName`, `collectorNumber`, `variant`, `rarity`, `quantity`, `priceUsd` (nullable), `isReserved`.

---

### 5.2 GET /api/collection — filter by rarity [AUTH]
```bash
curl -s "$API_BASE/collection?rarity=rare" \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, all returned cards have `rarity: "rare"`.

---

### 5.3 GET /api/collection — filter by setCode [AUTH]
```bash
curl -s "$API_BASE/collection?setCode=lea" \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, all returned cards have `setCode: "lea"`.

---

### 5.4 GET /api/collection/summary — returns stats [AUTH]
```bash
curl -s $API_BASE/collection/summary \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, object containing:
```json
{
  "totalCopies": <integer >= 0>,
  "totalUniqueCards": <integer >= 0>,
  "totalValue": <decimal >= 0>,
  "byRarity": { "rare": <int>, ... },
  "valueByRarity": { "rare": <decimal>, ... },
  "byType": { "creature": <int>, ... },
  "byVariant": { "Regular": <int>, ... },
  "reserveListOwned": <integer >= 0>,
  "reserveListValue": <decimal >= 0>,
  "boostersOwned": <integer >= 0>,
  "boostersTotal": <integer >= 0>
}
```

---

## 6. Reserve List

### 6.1 GET /api/reservelist — returns RL cards [AUTH]
```bash
curl -s $API_BASE/reservelist \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, array of ~600+ cards. Each has `scryfallCardId`, `name`, `setCode`, `rarity`, `priceUsd`, `owned` (boolean).

---

### 6.2 PATCH /api/reservelist/{id}/owned — mark owned [AUTH]
```bash
RL_ID=$(curl -s $API_BASE/reservelist \
  -H "Authorization: Bearer $TOKEN" | \
  grep -o '"scryfallCardId":"[^"]*"' | head -1 | cut -d'"' -f4)

curl -s -X PATCH "$API_BASE/reservelist/$RL_ID/owned" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"owned":true}'
```

**Expected:** HTTP 200. Subsequent `GET /api/reservelist` shows that card with `"owned": true`. Collection summary `reserveListOwned` increments by 1.

---

### 6.3 PATCH /api/reservelist/{id}/owned — mark not owned [AUTH]
Same as 6.2 but `"owned": false`.

**Expected:** HTTP 200. Card returns to `"owned": false`.

---

### 6.4 GET /api/reservelist/set/{setCode} — set-scoped RL [AUTH]
```bash
curl -s $API_BASE/reservelist/set/lea \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, array of Scryfall card IDs (strings) that are on the Reserve List and belong to the LEA set.

---

## 7. Boosters

### 7.1 GET /api/boosters — empty for new user [AUTH]
```bash
curl -s $API_BASE/boosters \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, empty array `[]`.

---

### 7.2 POST /api/boosters — create booster [AUTH]
```bash
curl -s -X POST $API_BASE/boosters \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"setCode":"mh3","boosterType":"Play","artVariant":"","owned":false}'
```

**Expected:** HTTP 200, object with `id`, `setCode: "mh3"`, `boosterType: "Play"`, `owned: false`.

Save the `id` as `BOOSTER_ID`.

---

### 7.3 GET /api/boosters — shows created booster [AUTH]
```bash
curl -s $API_BASE/boosters \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, array containing the booster created in 7.2.

---

### 7.4 PATCH /api/boosters/{id}/owned — mark owned [AUTH]
```bash
curl -s -X PATCH "$API_BASE/boosters/$BOOSTER_ID/owned" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"owned":true}'
```

**Expected:** HTTP 200. `GET /api/boosters` shows `"owned": true` for this booster. Collection summary `boostersOwned` increments.

---

### 7.5 POST /api/boosters — upsert idempotent [AUTH]
Posting the same `setCode`/`boosterType`/`artVariant` combination again.

**Expected:** HTTP 200, returns same or updated record (no duplicate created). `GET /api/boosters` still shows only one entry for that combination.

---

### 7.6 DELETE /api/boosters/{id} [AUTH]
```bash
curl -s -X DELETE "$API_BASE/boosters/$BOOSTER_ID" \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200. `GET /api/boosters` no longer contains this booster.

---

### 7.7 GET /api/boosters/set/{setCode} [AUTH]
```bash
# Re-create booster first
curl -s -X POST $API_BASE/boosters \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"setCode":"mh3","boosterType":"Collector","artVariant":"","owned":false}'

curl -s $API_BASE/boosters/set/mh3 \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, array containing only boosters with `setCode: "mh3"`.

---

## 8. Slabbed Cards

### 8.1 GET /api/slabbed — empty for new user [AUTH]
```bash
curl -s $API_BASE/slabbed \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, empty array `[]`.

---

### 8.2 POST /api/slabbed — add graded card [AUTH]
```bash
curl -s -X POST $API_BASE/slabbed \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "scryfallCardId": "<valid-card-id>",
    "cardName": "Black Lotus",
    "setCode": "lea",
    "setName": "Limited Edition Alpha",
    "collectorNumber": "232",
    "cardVariant": "Regular",
    "gradingCompany": "PSA",
    "grade": "9",
    "certificationNumber": "12345678",
    "purchaseDate": "2025-01-01",
    "purchasedFrom": "eBay",
    "purchaseCost": 150000.00,
    "notes": "Test slab"
  }'
```

**Expected:** HTTP 200 or 201, object with all fields plus `id` and `createdAt`. Save `id` as `SLAB_ID`.

---

### 8.3 GET /api/slabbed — shows created slab [AUTH]
```bash
curl -s $API_BASE/slabbed \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, array containing the slab from 8.2 with all fields present.

---

### 8.4 PUT /api/slabbed/{id} — update slab [AUTH]
```bash
curl -s -X PUT "$API_BASE/slabbed/$SLAB_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "scryfallCardId": "<valid-card-id>",
    "cardName": "Black Lotus",
    "setCode": "lea",
    "setName": "Limited Edition Alpha",
    "collectorNumber": "232",
    "cardVariant": "Regular",
    "gradingCompany": "PSA",
    "grade": "9.5",
    "certificationNumber": "12345678",
    "notes": "Updated note"
  }'
```

**Expected:** HTTP 200. `GET /api/slabbed` shows `grade: "9.5"` and updated notes.

---

### 8.5 POST /api/slabbed — duplicate cert rejected [AUTH]
Posting same `gradingCompany` + `certificationNumber` combination.

**Expected:** HTTP 400 or 409 (unique constraint violation).

---

### 8.6 DELETE /api/slabbed/{id} [AUTH]
```bash
curl -s -X DELETE "$API_BASE/slabbed/$SLAB_ID" \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200. `GET /api/slabbed` no longer contains this slab.

---

## 9. Exports

**Precondition:** At least one card owned, one booster, one RL card marked owned.

### 9.1 GET /api/export/cards/csv [AUTH]
```bash
curl -s $API_BASE/export/cards/csv \
  -H "Authorization: Bearer $TOKEN" \
  -o /tmp/cards.csv
file /tmp/cards.csv
head -2 /tmp/cards.csv
```

**Expected:** HTTP 200, `Content-Type: text/csv`. File contains a header row followed by data rows. Header includes `CardName`, `SetCode`, `CollectorNumber`, `Variant`, `Quantity`.

---

### 9.2 GET /api/export/cards/xml [AUTH]
```bash
curl -s $API_BASE/export/cards/xml \
  -H "Authorization: Bearer $TOKEN" \
  -o /tmp/cards.xml
head -5 /tmp/cards.xml
```

**Expected:** HTTP 200, `Content-Type: application/xml` or `text/xml`. Valid XML with root element and card child elements.

---

### 9.3 GET /api/export/boosters/csv [AUTH]
```bash
curl -s $API_BASE/export/boosters/csv \
  -H "Authorization: Bearer $TOKEN" \
  -o /tmp/boosters.csv
head -2 /tmp/boosters.csv
```

**Expected:** HTTP 200, CSV with header row containing booster fields.

---

### 9.4 GET /api/export/reservelist/csv [AUTH]
```bash
curl -s $API_BASE/export/reservelist/csv \
  -H "Authorization: Bearer $TOKEN" \
  -o /tmp/reservelist.csv
head -2 /tmp/reservelist.csv
```

**Expected:** HTTP 200, CSV with Reserve List ownership data.

---

### 9.5 GET /api/export/slabbed/pdf [AUTH]
**Precondition:** At least one slab exists.

```bash
curl -s $API_BASE/export/slabbed/pdf \
  -H "Authorization: Bearer $TOKEN" \
  -o /tmp/slabbed.pdf
file /tmp/slabbed.pdf
```

**Expected:** HTTP 200, `Content-Type: application/pdf`. Output is a valid PDF file.

---

### 9.6 GET /api/export/collection/summary/csv [AUTH]
```bash
curl -s $API_BASE/export/collection/summary/csv \
  -H "Authorization: Bearer $TOKEN" \
  -o /tmp/collection_summary.csv
head -5 /tmp/collection_summary.csv
```

**Expected:** HTTP 200, CSV with sections for Overall, By Rarity, By Type, By Variant, Reserve List, Boosters.

---

### 9.7 GET /api/export/collection/summary/pdf [AUTH]
```bash
curl -s $API_BASE/export/collection/summary/pdf \
  -H "Authorization: Bearer $TOKEN" \
  -o /tmp/collection_summary.pdf
file /tmp/collection_summary.pdf
```

**Expected:** HTTP 200, valid PDF.

---

### 9.8 GET /api/export/collection/detailed/csv — with filter [AUTH]
```bash
curl -s "$API_BASE/export/collection/detailed/csv?rarity=rare" \
  -H "Authorization: Bearer $TOKEN" \
  -o /tmp/collection_detailed.csv
head -2 /tmp/collection_detailed.csv
```

**Expected:** HTTP 200, CSV with header `CardName,SetCode,SetName,CollectorNumber,Rarity,TypeLine,Variant,Quantity,PriceEach,LineValue,IsReserved`. All data rows have rarity "rare".

---

### 9.9 Export endpoints — no auth
```bash
curl -s -o /dev/null -w "%{http_code}" $API_BASE/export/cards/csv
```

**Expected:** HTTP 401.

---

## 10. Labels

### 10.1 POST /api/labels/generate — set box label
```bash
curl -s -X POST $API_BASE/labels/generate \
  -H "Content-Type: application/json" \
  -d '{"setCode":"lea","labelType":"SetBox"}' \
  -o /tmp/label.pdf
file /tmp/label.pdf
```

**Expected:** HTTP 200, `Content-Type: application/pdf`, valid PDF file containing the set label.

---

### 10.2 POST /api/labels/generate — surplus box label
```bash
curl -s -X POST $API_BASE/labels/generate \
  -H "Content-Type: application/json" \
  -d '{"setCode":"lea","labelType":"SurplusBox"}' \
  -o /tmp/label_surplus.pdf
file /tmp/label_surplus.pdf
```

**Expected:** HTTP 200, valid PDF.

---

## 11. Import

### 11.1 POST /api/import/collection — CountOrSell CSV [AUTH]
**Precondition:** Export a CSV first from 9.1.

```bash
curl -s -X POST $API_BASE/import/collection \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@/tmp/cards.csv"
```

**Expected:** HTTP 200, summary object containing `imported` and `skipped` counts. No error message.

---

### 11.2 POST /api/import/collection — wrong file type [AUTH]
```bash
echo "not a valid file" > /tmp/bad.txt
curl -s -X POST $API_BASE/import/collection \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@/tmp/bad.txt"
```

**Expected:** HTTP 400, error message about unsupported format.

---

## 12. Updates

### 12.1 GET /api/updates/check — check for updates
```bash
curl -s $API_BASE/updates/check
```

**Expected:** HTTP 200, object with `currentVersion` (string, the local DB version), `updateAvailable` (boolean), and optionally `availableVersion` and package info. Does not require auth.

---

### 12.2 GET /api/updates/history — update history
```bash
curl -s $API_BASE/updates/history
```

**Expected:** HTTP 200, array of applied update packages. May be empty if no updates have been applied.

---

## 13. Admin

**Precondition:** Login as `cosadm` and save token as `ADMIN_TOKEN`.

```bash
ADMIN_TOKEN=$(curl -s -X POST $API_BASE/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"cosadm","password":"wholeftjaceinchargeofdesign"}' \
  | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)
```

---

### 13.1 GET /api/admin/users [ADMIN]
```bash
curl -s $API_BASE/admin/users \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

**Expected:** HTTP 200, array of user objects. Each has `id`, `username`, `displayName`, `isAdmin`, `isDisabled`, `createdAt`.

---

### 13.2 GET /api/admin/users — non-admin token rejected [AUTH]
```bash
curl -s -o /dev/null -w "%{http_code}" $API_BASE/admin/users \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 403.

---

### 13.3 GET /api/admin/status [ADMIN]
```bash
curl -s $API_BASE/admin/status \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

**Expected:** HTTP 200, object containing:
```json
{
  "totalUsers": <int>,
  "activeUsers": <int>,
  "disabledUsers": <int>,
  "adminUsers": <int>,
  "totalSets": <int>,
  "totalCards": <int>,
  "lastCardSyncedAt": "<datetime or null>",
  "cardsWithImages": <int>,
  "totalOwnershipRecords": <int>,
  "totalOwnedCopies": <int>,
  "totalUniqueCardsOwned": <int>,
  "reserveListCardsOwned": <int>,
  "totalBoostersDefined": <int>,
  "totalBoostersOwned": <int>
}
```

All integer fields ≥ 0. `totalUsers` ≥ 2 (cosadm + testuser). `totalSets` and `totalCards` ≥ 1 if data synced.

---

### 13.4 PUT /api/admin/users/{id} — promote to admin [ADMIN]
```bash
# Get testuser's id
USER_ID=$(curl -s $API_BASE/admin/users \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  | grep -A5 '"username":"testuser"' | grep '"id"' | grep -o '"[a-f0-9-]*"' | tail -1 | tr -d '"')

curl -s -X PUT "$API_BASE/admin/users/$USER_ID" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"isAdmin":true,"isDisabled":false}'
```

**Expected:** HTTP 200. `GET /api/admin/users` shows testuser with `"isAdmin": true`.

*Reset: set `isAdmin: false` before continuing.*

---

### 13.5 PUT /api/admin/settings — disable registrations [ADMIN]
```bash
curl -s -X PUT $API_BASE/admin/settings \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"registrationsEnabled":false}'
```

**Expected:** HTTP 200. `GET /api/auth/registration-status` returns `{"registrationsEnabled": false}`. Registration attempts return HTTP 403.

*Reset: set `registrationsEnabled: true` before continuing.*

---

### 13.6 DELETE /api/admin/users/{id} — delete user [ADMIN]
Create a throwaway user first, then delete it.

```bash
# Register a throwaway user
curl -s -X POST $API_BASE/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"throwaway","password":"throwawaypassword123"}'

# Get their ID
THROW_ID=$(curl -s $API_BASE/admin/users \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  | grep -A5 '"username":"throwaway"' | grep '"id"' | \
    grep -o '"[a-f0-9-]*"' | tail -1 | tr -d '"')

curl -s -X DELETE "$API_BASE/admin/users/$THROW_ID" \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

**Expected:** HTTP 200. `GET /api/admin/users` no longer contains the throwaway user.

---

## 14. CLI

Run from repository root. All commands use:
```bash
dotnet run --project src/CountOrSell.Cli -- <command> [options]
```

### 14.1 sync --sets
```bash
dotnet run --project src/CountOrSell.Cli -- sync --sets
```

**Expected:**
- Spectre.Console progress bars display (spinner then progress bar)
- Exits with code 0
- Output ends with: `Synced N sets.` where N ≥ 1
- Database `CachedSets` table is populated

---

### 14.2 sync --set-code {code}
```bash
dotnet run --project src/CountOrSell.Cli -- sync --set-code lea
```

**Expected:**
- Spectre.Console status spinner while running
- Exits with code 0
- Database `CachedCards` table contains cards with `setCode = "lea"`

---

### 14.3 sync --cards (after sets and at least one set synced)
```bash
dotnet run --project src/CountOrSell.Cli -- sync --cards
```

**Expected:**
- Progress bars with per-set counters
- Output ends with: `Synced N total cards across all sets.`
- Existing card data updated, `LastSyncedAt` refreshed

---

### 14.4 images --all --missing-only (default)
```bash
dotnet run --project src/CountOrSell.Cli -- images --all
```

**Expected:**
- Pre-scan spinner runs, heals any existing-but-untracked images
- Progress bar with `└ SETCODE: n/N` per-set counter
- Output line: `Downloaded X images.` optionally followed by `Y skipped (no image URL in Scryfall data).` and/or `Z failed (download error).`
- Image files created under `src/CountOrSell.Api/images/{setCode}/{cardId}.jpg`
- `LocalImagePath` populated in `CachedCards` for downloaded images

---

### 14.5 images --set-code {code}
```bash
dotnet run --project src/CountOrSell.Cli -- images --set-code lea
```

**Expected:** Same as 14.4 but scoped to LEA cards only.

---

### 14.6 publish --output-dir
```bash
mkdir -p /tmp/cos-publish
dotnet run --project src/CountOrSell.Cli -- publish --output-dir /tmp/cos-publish
```

**Expected:**
- Exits with code 0
- Creates `/tmp/cos-publish/dbupdate.json` — valid JSON with `currentVersion` and `packages` array
- Creates `/tmp/cos-publish/packages/full-*.zip`
- ZIP contains `data.db` (SQLite) and `manifest.json`

---

### 14.7 review --list
```bash
dotnet run --project src/CountOrSell.Cli -- review --list
```

**Expected:** Exits with code 0. Output shows list of pending submissions (may be empty).

---

### 14.8 --help
```bash
dotnet run --project src/CountOrSell.Cli -- --help
dotnet run --project src/CountOrSell.Cli -- sync --help
dotnet run --project src/CountOrSell.Cli -- images --help
dotnet run --project src/CountOrSell.Cli -- publish --help
dotnet run --project src/CountOrSell.Cli -- review --help
```

**Expected:** Each prints usage text for the respective command, exits 0.

---

## 15. Frontend UI (Manual Browser Tests)

Open `http://localhost:5173` before each test.

### 15.1 Navigation — unauthenticated
**Expected:** Nav bar shows: **CountOrSell** (logo), **Sets**, **Login / Register**. Collection, Reserve List, Boosters, Slabs links are hidden.

---

### 15.2 Navigation — authenticated
Login as testuser.

**Expected:** Nav bar shows: **Sets**, **Collection**, **Reserve List**, **Boosters**, **Slabs**, user dropdown (username). No Login link.

---

### 15.3 Sets page — browse and search
1. Navigate to `/`
2. Verify sets grid is populated
3. Type a set name in the search box
4. **Expected:** Grid filters in real time to matching sets.
5. Select a set type from the type filter dropdown
6. **Expected:** Grid shows only sets of that type.

---

### 15.4 Set Detail — card grid and ownership toggle
1. Click any set that has cards synced
2. **Expected:** Cards display with image, collector number, rarity badge, price. Progress counter shows "N/M owned".
3. Login and return to the set
4. Click **Own** on a card
5. **Expected:** Card gets green ring. Counter increments.
6. Click the card again (unown)
7. **Expected:** Green ring removed. Counter decrements.

---

### 15.5 Set Detail — Full Set button
1. Navigate to a set detail page while logged in
2. Click **Full Set ✓** button
3. Confirm the dialog
4. **Expected:** All cards get green rings. Counter shows full count.

---

### 15.6 Set Detail — card detail modal
1. Click a card image
2. **Expected:** Modal opens showing full card image, oracle text, type line, mana cost, regular and foil prices, Scryfall link, Oracle Rulings link.
3. Close the modal
4. **Expected:** Modal closes, underlying page unchanged.

---

### 15.7 Set Detail — box label generation
1. Navigate to a set detail page
2. Click **Generate Label** (or equivalent)
3. Select **Set Box**, click generate
4. **Expected:** PDF downloads or opens. Contains set name and set icon.

---

### 15.8 Collection page
1. Navigate to `/collection` while logged in (with some owned cards)
2. **Expected:** Stats panel shows total copies, unique cards, total value. Breakdown cards show By Rarity, By Type, By Variant.
3. Apply a rarity filter
4. **Expected:** Table updates. Stat panel totals remain unchanged (always full collection).
5. Click **Detailed CSV** export
6. **Expected:** CSV file downloads.

---

### 15.9 Reserve List page
1. Navigate to `/reservelist` while logged in
2. **Expected:** Cards display in a grid with name, set, rarity, price, Own/Want toggle.
3. Stats at top show total RL card count and how many are owned.
4. Toggle **Own** on a card
5. **Expected:** Toggle state changes. Owned count at top updates.

---

### 15.10 Boosters page
1. Navigate to `/boosters`
2. Click **+ Add Booster**
3. Select a set, booster type (e.g. "Collector"), click **Add**
4. **Expected:** New row appears in booster list.
5. Toggle the **Owned** switch
6. **Expected:** Switch toggles; change persists on page refresh.
7. Click delete (×)
8. **Expected:** Row removed.

---

### 15.11 Slabs page
1. Navigate to `/slabbed`
2. Click **+ Add Slab**
3. Search for a card name, select a result
4. Fill in grading company (PSA), grade (9), cert number (99999999)
5. Click **Add Slab**
6. **Expected:** New row appears with cert number as a link to `psacard.com/cert/99999999`.
7. Click **Edit**, change grade to 9.5, click **Save**
8. **Expected:** Row updates to show grade 9.5.
9. Click **Delete**, confirm
10. **Expected:** Row removed.

---

### 15.12 Slabs — Export PDF
1. With at least one slab present, click **Export PDF**
2. **Expected:** PDF downloads. Contains landscape table with columns: Cert ID, Card Name, Set, Variant, Company, Grade, Date Acquired, Price.

---

### 15.13 Login page — register tab
1. Navigate to `/login`
2. Switch to **Register** tab
3. Enter a new unique username, a password of at least 15 characters, optional display name
4. Click **Register**
5. **Expected:** Logged in immediately, redirected to home. Nav shows new username.

---

### 15.14 Login page — wrong password
1. Navigate to `/login`
2. Enter valid username with wrong password
3. **Expected:** Error message displayed. Not logged in.

---

### 15.15 Profile page — change password
1. Navigate to user menu → Profile
2. Enter current password and a new password (≥ 15 chars)
3. **Expected:** Success message. Old password no longer works for login.

---

### 15.16 Admin page — users tab
1. Login as `cosadm`, navigate to `/admin`
2. **Expected:** **Users & Settings** tab shows user table with username, display name, admin badge, disabled status, actions.
3. Click **Promote** on testuser
4. **Expected:** testuser now shows admin badge.

---

### 15.17 Admin page — status tab
1. Navigate to `/admin` → **System Status** tab
2. **Expected:** Stat cards display for: Users (total, active, disabled, admins), Card Data (sets, cards, last sync), Local Images (cached count with percentage), Collection Activity (ownerships, copies, unique, RL owned, boosters).
3. All values are non-negative integers.

---

### 15.18 Update check panel
1. Check the top-right update indicator
2. **Expected:** Shows current database version, or a notification that an update is available.
3. If update available, click **Apply Update** (requires login)
4. **Expected:** Progress indicator shows. Page reloads after completion.

---

### 15.19 Settings page — import collection
1. Navigate to `/settings`
2. Export a CSV from the Collection page
3. On Settings, use the Import panel to upload the CSV
4. **Expected:** Success message with import count. No duplicate entries created.

---

### 15.20 Footer
On any page:

**Expected:** Footer displays:
- A tagline (varies each load)
- "Data provided by Scryfall" with link
- "CountOrSell © 2026 by Brian Mork / Dark Grotto LLC is licensed under CC BY-NC-SA 4.0" with CC icons
- "Brian Mork" links to `https://www.darkgrotto.com`
- "Dark Grotto LLC" links to `https://www.darkgrotto.com`

---

### 15.21 Protected routes — unauthenticated redirect
While logged out, navigate directly to:
- `/collection`
- `/reservelist`
- `/boosters`
- `/slabbed`
- `/admin`

**Expected:** Each redirects to `/login`.

---

## 16. Build Verification

### 16.1 .NET solution build
```bash
dotnet build src/CountOrSell.sln --nologo -v q
```

**Expected:** `Build succeeded. 0 Warning(s). 0 Error(s).`

---

### 16.2 TypeScript check
```bash
cd src/countorsell-web && npx tsc --noEmit
```

**Expected:** No output (exit code 0). Zero TypeScript errors.

---

### 16.3 Frontend dev build
```bash
cd src/countorsell-web && npm run build
```

**Expected:** Vite build completes with no errors. Output in `dist/`.

---

### 16.4 Docker build
```bash
docker build -t countorsell:test .
```

**Expected:** All stages complete successfully. Image tagged `countorsell:test`.

---

### 16.5 Docker run
```bash
docker run -d --name cos-test \
  -e JWT_KEY="test-key-at-least-32-characters-long" \
  -p 8081:8080 \
  countorsell:test

sleep 5
curl -s http://localhost:8081/api/auth/registration-status

docker rm -f cos-test
```

**Expected:** Returns `{"registrationsEnabled":true}`. Container serves both API and frontend on port 8081.

---

## 17. Security

### 17.1 JWT required on protected endpoints
For each of the following, issue without `Authorization` header:
```bash
for ENDPOINT in collection reservelist boosters slabbed; do
  echo -n "$ENDPOINT: "
  curl -s -o /dev/null -w "%{http_code}\n" $API_BASE/$ENDPOINT
done
```

**Expected:** All return HTTP 401.

---

### 17.2 Admin endpoints return 403 for non-admin
```bash
for ENDPOINT in admin/users admin/status admin/settings; do
  echo -n "$ENDPOINT: "
  curl -s -o /dev/null -w "%{http_code}\n" $API_BASE/$ENDPOINT \
    -H "Authorization: Bearer $TOKEN"
done
```

**Expected:** All return HTTP 403.

---

### 17.3 Cross-user data isolation
**Scenario:** User A marks a card owned. User B queries the same endpoint.

1. Login as testuser, mark a card owned
2. Register a second user (`testuser2` / `testpassword12345x`)
3. Login as testuser2, get token as `TOKEN2`
4. `GET /api/sets/{setCode}/owned-cards` with TOKEN2

**Expected:** testuser2's result does not include testuser's owned cards. Data is fully user-scoped.

---

### 17.4 Swagger disabled in Production
If running with `ASPNETCORE_ENVIRONMENT=Production`:
```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/swagger/index.html
```

**Expected:** HTTP 404. Swagger is only available in Development mode.

---

## 18. Edge Cases

### 18.1 Empty collection exports
**Precondition:** A fresh user with no owned cards.

```bash
curl -s $API_BASE/export/cards/csv -H "Authorization: Bearer $TOKEN2"
```

**Expected:** HTTP 200, CSV with only header row (no data rows). Not an error.

---

### 18.2 Large quantity values
```bash
curl -s -X PUT "$API_BASE/cards/$CARD_ID/variant" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"variant":"Regular","quantity":999}'
```

**Expected:** HTTP 200, quantity stored as 999. Collection summary `totalCopies` reflects this.

---

### 18.3 Collection filter — no matches
```bash
curl -s "$API_BASE/collection?rarity=mythic&setCode=lea" \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:** HTTP 200, empty array `[]` (LEA predates mythic rarity).

---

### 18.4 Image download — cards with no URL
After `images --all`:

**Expected:** Output includes `N skipped (no image URL in Scryfall data).` for any cards whose `ImageUrisJson` and `CardFacesJson` are both null. This is normal for certain special card types. It is NOT an error.

---

### 18.5 CLI sync preserves LocalImagePath
1. Note `LocalImagePath` for a card that has been image-downloaded
2. Run `sync --set-code {setCode}` for that set
3. Re-check `LocalImagePath`

**Expected:** `LocalImagePath` is unchanged after sync. Sync does not reset image paths.

---

## Quick Smoke Test (5-minute check)

Run these in order to confirm the application is functional after any change:

```bash
# 1. Build checks
dotnet build src/CountOrSell.sln --nologo -v q
cd src/countorsell-web && npx tsc --noEmit && cd -

# 2. API health
curl -sf $API_BASE/auth/registration-status

# 3. Sets
curl -sf $API_BASE/sets | grep -q '"code"'

# 4. Login
TOKEN=$(curl -sf -X POST $API_BASE/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"cosadm","password":"wholeftjaceinchargeofdesign"}' \
  | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)

# 5. Auth works
curl -sf $API_BASE/auth/me -H "Authorization: Bearer $TOKEN" | grep -q '"username"'

# 6. Admin status
curl -sf $API_BASE/admin/status -H "Authorization: Bearer $TOKEN" | grep -q '"totalUsers"'

# 7. Collection
curl -sf $API_BASE/collection -H "Authorization: Bearer $TOKEN"

echo "Smoke test passed."
```

**Expected:** All commands exit 0 and produce expected output. Final line: `Smoke test passed.`

---

## Notes for Claude Code

- The default admin password is `wholeftjaceinchargeofdesign` (do not change it in tests — reset after any password test)
- Card IDs are Scryfall UUIDs; fetch live IDs from `GET /api/sets/{setCode}/cards` rather than hardcoding
- Tests that modify data (ownership, boosters, slabs) should clean up after themselves
- The database file is at `src/CountOrSell.Api/database/CountOrSell.db`; it is tracked via Git LFS
- Running `dotnet build src/CountOrSell.sln` while the API is running will fail (DLL locked); build `CountOrSell.Core.csproj` or `CountOrSell.Cli.csproj` instead, or stop the API first
- The Spectre.Console progress bars in the CLI require an ANSI terminal; output may differ in non-interactive shells
- `GET /api/updates/check` makes an outbound HTTP call to `countorsell.com`; it may return an error in offline environments — this is expected
