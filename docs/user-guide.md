# User Guide

## Getting Around

The navigation bar at the top of every page provides access to all sections. Some sections require you to be logged in.

| Nav link | Login required | Description |
|----------|---------------|-------------|
| **CountOrSell** (logo) | No | Returns to the Sets list |
| **Sets** | No | Browse all MTG sets |
| **Reserve List** | Yes | Track your Reserved List cards |
| **Boosters** | Yes | Track your booster pack collection |
| **Slabs** | Yes | Track professionally graded cards |
| **Login / Logout** | — | Authentication |

The **Update** indicator (top-right area) shows when a newer card database is available.

---

## Authentication

### Registering

1. Click **Login** in the navigation bar
2. Switch to the **Register** tab
3. Enter a username (must be unique), password (minimum 6 characters), and an optional display name
4. Click **Register** — you are logged in immediately

### Logging in

1. Click **Login**
2. Enter your username and password
3. Click **Login**

Sessions use JWT tokens that expire after 60 minutes by default. The frontend automatically refreshes the token in the background, so you will not be logged out mid-session under normal use.

---

## Sets

The **Sets** page is the home screen. It lists every MTG set that has been synced into the database.

### Browsing sets

- Sets are displayed as cards with the set icon, name, type, and card count
- Use the **search box** to filter by name
- Use the **type filter** dropdown to narrow down by set type (expansion, masters, commander, etc.)
- Sets can be tagged for custom grouping (see below)

### Opening a set

Click any set card to open the **Set Detail** page, which shows every card in the set.

### Set tags

Sets can be tagged to help with organisation. Tags are visible in the set list and can be used for filtering.

- **Adding a tag:** On the Set Detail page, type a tag name in the tag input and press Enter
- **Removing a tag:** Click the × next to an existing tag
- Tags are shared across all users (they are not per-account)
- The sync CLI applies standard tags automatically based on Scryfall set type

---

## Set Detail

The Set Detail page shows every card in a set. It requires card data to have been synced for that set (see [CLI Reference](cli.md#sync--pull-data-from-scryfall)).

### Browsing cards

Cards are displayed in collector-number order. Each card shows:
- Card image (from local cache or Scryfall directly)
- Collector number
- Rarity badge
- Price (USD, from Scryfall)
- Reserved List badge (for RL cards)

### Marking cards as owned

- Click **Own** on a card to mark it as owned
- Click again to unmark it
- Owned cards are highlighted with a green ring
- A progress counter at the top shows how many cards you own vs. the total

### Bulk actions

- **Select All / Deselect All** — toggle ownership of all visible cards
- Filter by rarity or search by name before using bulk select to target a subset

### Card detail

Click a card image to open the **Card Detail Modal**, which shows:
- Full card image
- Oracle text, type line, mana cost
- Current prices (regular and foil)
- Scryfall link

### Box labels

The Set Detail page includes a **Generate Label** button that produces a printable PDF label for a storage box. Labels show the set name, icon, and a barcode. Two label types are available:
- **Set Box** — for a complete set box
- **Surplus Box** — for overflow cards

---

## Reserve List

The **Reserve List** page tracks your ownership of cards on the MTG Reserved List. These cards will never be officially reprinted.

### Browsing

- All ~600+ Reserved List cards are shown in a grid
- Filter by name, set, rarity, or owned/not-owned status
- Sort by name, price, or set

### Marking as owned

Click the **Own / Want** toggle on any card. Changes are saved immediately.

### Stats

The top of the page shows:
- Total Reserved List card count
- How many you own
- The total market value (USD) of your owned cards

---

## Boosters

The **Boosters** page tracks your booster pack collection.

### Adding a booster

1. Click **+ Add Booster**
2. Select the set from the dropdown
3. Choose the booster type (Collector, Play, Draft, Set, Jumpstart, Sample)
4. Enter an art variant name if there are multiple arts (e.g. "Ashiok Art")
5. Click **Add**

### Marking as owned

Toggle the **Owned** switch on any booster entry to record that you have it.

### Deleting a booster

Click the delete (×) button on any entry.

---

## Slabs

The **Slabs** page tracks your professionally graded (slabbed) cards. A slabbed card has been authenticated and encapsulated by a grading company and assigned a unique certification number.

### Adding a slab

1. Click **+ Add Slab**
2. **Search for the card** — type the card name in the search box. A dropdown appears with matching results from the local card database. Selecting a result auto-fills the set code, set name, and collector number
3. Select the **card variant** (Regular, Foil, Etched Foil, etc.)
4. Select the **grading company** (PSA, BGS, CGC, SGC, GAI, CSG)
5. Enter the **grade** (e.g. `9`, `9.5`, `10`)
6. Enter the **certification number** exactly as it appears on the label
7. Optionally fill in purchase date, purchased from, purchase cost, and notes
8. Click **Add Slab**

### Viewing the cert

In the slab table, the **Cert #** column links directly to the grading company's cert verification page for supported companies:

| Company | Cert lookup URL |
|---------|----------------|
| PSA | `psacard.com/cert/{number}` |
| BGS | `beckett.com/grading/cert/{number}` |
| CGC | `cgccards.com/certlookup/{number}/` |
| SGC | `sgccard.com/cert/{number}` |

Click the cert number to open the verification page in a new tab.

### Editing a slab

Click **Edit** in the row to open the edit modal. All fields can be updated.

### Deleting a slab

Click **Delete** in the row. A confirmation dialog appears before anything is removed.

### Exporting to PDF

Click **Export PDF** at the top of the page to download a PDF of your entire slabbed collection. The PDF is formatted as a landscape table with columns: Cert ID, Card Name, Set, Variant, Company, Grade, Date Acquired, Price.

---

## Data Updates

The **Update** panel in the navigation bar shows a notification when a newer card database is available from the update server.

### Checking for updates

The panel automatically checks on page load. You can also expand it to see the current database version.

### Applying an update

1. When an update is available, the panel shows the new version and package size
2. Click **Apply Update** (you must be logged in)
3. The API downloads the update package and applies it in the background
4. The page reloads once the update is complete

Updates only affect the shared card and set data. Your personal collection data (owned cards, boosters, slabs) is not changed.

---

## Exporting Your Collection

Several export formats are available from `Settings → Export` (accessible via the API directly, or from any export panel in the UI if present):

| Endpoint | Format | Contents |
|----------|--------|---------|
| `GET /api/export/cards/csv` | CSV | All owned cards |
| `GET /api/export/cards/xml` | XML | All owned cards |
| `GET /api/export/boosters/csv` | CSV | All tracked boosters |
| `GET /api/export/reservelist/csv` | CSV | Owned Reserved List cards |
| `GET /api/export/slabbed/pdf` | PDF | Full slabbed card collection |

These endpoints require a valid JWT token. You can access them in the Swagger UI (`http://localhost:5000/swagger`) by authorizing with your token first.
