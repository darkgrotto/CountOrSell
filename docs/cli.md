# CLI Reference

The `CountOrSell.Cli` tool is the admin companion to the API. It handles data management tasks that are not exposed through the web UI: syncing card data from Scryfall, downloading card images, building update packages for distribution, and reviewing user submissions.

## Running the CLI

```bash
dotnet run --project src/CountOrSell.Cli -- <command> [options]
```

The CLI operates on the **same database** as the API. By default that file is resolved relative to the CLI binary's output directory:

```
src/CountOrSell.Cli/bin/Debug/net8.0/CountOrSell.db
```

> **Note:** The CLI and the API each look for `CountOrSell.db` next to their own binary. In development they use **separate database files**. If you want the CLI to populate data that the API serves, point the CLI to the API's database by copying/symlinking, or always run the CLI against the published API's database file.
>
> A common workflow: run `dotnet publish` for the API to a shared output folder, place the CLI binary in the same folder, and run both against the same `CountOrSell.db`.

---

## Commands

### `sync` — Pull data from Scryfall

Downloads and caches set and card metadata into the local SQLite database. Scryfall is the authoritative source; the CLI upserts (insert or update) every record it fetches.

```bash
dotnet run --project src/CountOrSell.Cli -- sync [options]
```

| Option | Description |
|--------|-------------|
| `--sets` | Sync the set list only (fast, ~5 seconds) |
| `--cards` | Refresh cards for every set that already has cached cards |
| `--set-code <code>` | Sync cards for one specific set (e.g. `mh3`, `2x2`) |
| `--all` | Sync the full set list **and** all cards for every previously-cached set |

Running `sync` with no options is equivalent to `--all`.

**Examples:**

```bash
# Quickest way to see sets in the UI
dotnet run --project src/CountOrSell.Cli -- sync --sets

# Add cards for one set
dotnet run --project src/CountOrSell.Cli -- sync --set-code dsk

# Full refresh of everything (slow, 20–40 minutes)
dotnet run --project src/CountOrSell.Cli -- sync --all

# Re-sync just the cards you already have cached
dotnet run --project src/CountOrSell.Cli -- sync --cards
```

**Auto-tagging:** After syncing sets the CLI automatically applies tags (e.g. `commander`, `masters`, `core`, `draft-innovation`) based on Scryfall set-type metadata. These tags are displayed in the web UI and used for filtering.

---

### `images` — Download card images

Downloads card face images from Scryfall and stores them locally. The API serves these cached images at `/api/images/{cardId}`. Without cached images the frontend falls back to direct Scryfall URLs (requires internet while browsing).

```bash
dotnet run --project src/CountOrSell.Cli -- images [options]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--set-code <code>` | — | Download images for a specific set only |
| `--all` | — | Download images for every card in the database |
| `--missing-only` | `true` | Skip cards that already have a local image |

**Examples:**

```bash
# Download only what's missing (safe to run repeatedly)
dotnet run --project src/CountOrSell.Cli -- images --all

# Re-download all images for one set (overwrites existing)
dotnet run --project src/CountOrSell.Cli -- images --set-code mh3 --missing-only false

# Download images for a new set after syncing its cards
dotnet run --project src/CountOrSell.Cli -- images --set-code dsk
```

Images are saved as `<base-dir>/images/<scryfallCardId>.jpg`. The CLI rate-limits requests to ~13 per second (75 ms delay) to respect Scryfall's guidelines. A full image download for all MTG sets takes several hours.

---

### `publish` — Build update packages

Packages the current database contents into distributable ZIP files that other CountOrSell instances can download and apply via the in-app update system. Produces a `dbupdate.json` manifest and one or more package ZIPs.

```bash
dotnet run --project src/CountOrSell.Cli -- publish --output-dir <path> [options]
```

| Option | Required | Description |
|--------|----------|-------------|
| `--output-dir <path>` | Yes | Where to write packages and the manifest |
| `--version <ver>` | No | Version string (default: `yyyy.MM.dd.HHmm`) |
| `--delta-from <ver>` | No | Also generate a delta package from this version |
| `--base-db <path>` | With `--delta-from` | Path to the previous version's database file |

**Full package only:**

```bash
dotnet run --project src/CountOrSell.Cli -- publish \
  --output-dir /var/www/countorsell/dbupdate
```

Produces:
```
/var/www/countorsell/dbupdate/
├── dbupdate.json              ← manifest (host this at countorsell.com/dbupdate)
└── packages/
    └── full-2025.06.01.1200.zip
```

**Full + delta package:**

```bash
dotnet run --project src/CountOrSell.Cli -- publish \
  --output-dir /var/www/countorsell/dbupdate \
  --version 2025.06.15.0900 \
  --delta-from 2025.06.01.1200 \
  --base-db /archive/CountOrSell-2025.06.01.db
```

The delta ZIP contains only sets and cards that changed since the base database, making the download much smaller. Clients automatically prefer a matching delta over a full package.

**Package format:**

Each ZIP contains:
- `data.db` — a stripped SQLite file with only `CachedSets` and `CachedCards` (no user data)
- `manifest.json` — version, type, and record counts

---

### `review` — Review user submissions

Users can submit proposed changes (card data corrections, tag suggestions) through the web UI. This command lets an admin inspect and act on those submissions.

```bash
dotnet run --project src/CountOrSell.Cli -- review [options]
```

| Option | Description |
|--------|-------------|
| `--list` | List all pending submissions (default if no option given) |
| `--show <id>` | Print full details of a submission |
| `--approve <id>` | Approve a submission |
| `--reject <id>` | Reject a submission |
| `--approve-all` | Approve every pending submission at once |

**Examples:**

```bash
# See what's waiting
dotnet run --project src/CountOrSell.Cli -- review --list

# Inspect before deciding
dotnet run --project src/CountOrSell.Cli -- review --show 42

# Approve it
dotnet run --project src/CountOrSell.Cli -- review --approve 42

# Reject it
dotnet run --project src/CountOrSell.Cli -- review --reject 42

# Bulk approve everything
dotnet run --project src/CountOrSell.Cli -- review --approve-all
```

---

## Global Help

```bash
# Top-level help
dotnet run --project src/CountOrSell.Cli -- --help

# Command-level help
dotnet run --project src/CountOrSell.Cli -- sync --help
dotnet run --project src/CountOrSell.Cli -- images --help
dotnet run --project src/CountOrSell.Cli -- publish --help
dotnet run --project src/CountOrSell.Cli -- review --help
```
