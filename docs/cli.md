# CLI Reference

The `CountOrSell.Cli` tool is the admin companion to the API. It handles data management tasks that are not exposed through the web UI: syncing card data from Scryfall, downloading card images, building update packages for distribution, and reviewing user submissions.

## Running the CLI

```bash
dotnet run --project src/CountOrSell.Cli -- <command> [options]
```

## Database path

The CLI and the API each resolve the database path independently. By default both use a heuristic to find the repository root and look for the database there.

| Context | Default database path |
|---------|-----------------------|
| CLI (development) | `src/CountOrSell.Api/database/CountOrSell.db` (resolved from repo root) |
| API (development) | `src/CountOrSell.Api/database/CountOrSell.db` (resolved from repo root) |
| Docker / published | Set via `COS_DATABASE_PATH` environment variable |

In normal local development the CLI and API share the same database automatically because both look for the same file relative to the repository root.

**Targeting a different database** — set `COS_DATABASE_PATH` before running the CLI:

```bash
# Linux / macOS
COS_DATABASE_PATH=/path/to/other/CountOrSell.db \
  dotnet run --project src/CountOrSell.Cli -- sync --sets

# Windows PowerShell
$env:COS_DATABASE_PATH = 'C:\path\to\other\CountOrSell.db'
dotnet run --project src/CountOrSell.Cli -- sync --sets
```

**Docker deployments** — the database lives inside the container's named volume. The recommended approach is to use the web UI's built-in **Synchronize** feature to update card data. If you need CLI access, see [Database → Running the CLI against a Docker database](database.md#running-the-cli-against-a-docker-database).

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
| `--all` | Sync the full set list **and** all cards for every set with a non-zero card count |

Running `sync` with no options is equivalent to `--all`.

Progress is shown as a live status bar:
- A spinner while sets are being fetched from Scryfall
- A progress bar with a set counter and elapsed time while upserting
- For `--all` and `--cards`, the current set code is shown inline (`└ MH3`)

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

Progress is shown as a live status bar with a per-set counter (`└ DSK: 25/268`).

Before the main download loop, the CLI performs a **pre-scan**: it checks whether any image files already exist on disk but are not recorded in the database (e.g. after a fresh sync), and heals those entries automatically.

**Examples:**

```bash
# Download only what's missing (safe to run repeatedly — default behaviour)
dotnet run --project src/CountOrSell.Cli -- images --all

# Download images for a new set after syncing its cards
dotnet run --project src/CountOrSell.Cli -- images --set-code dsk

# Re-download all images for one set (overwrites existing)
dotnet run --project src/CountOrSell.Cli -- images --set-code mh3 --missing-only false
```

Images are saved under `<images-root>/<set-code>/<scryfallCardId>.jpg`. The images root defaults to `src/CountOrSell.Api/images/` and can be overridden with `COS_IMAGES_PATH`. The CLI rate-limits requests to ~13 per second (75 ms delay) to respect Scryfall's guidelines. A full download for all MTG sets takes several hours and totals approximately 11 GB.

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
├── dbupdate.json              ← manifest (host at the configured manifest URL)
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
- `data.db` — a stripped SQLite file with only `CachedSets`, `CachedCards`, and `SetTags` (no user data)
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

## Global help

```bash
# Top-level help
dotnet run --project src/CountOrSell.Cli -- --help

# Command-level help
dotnet run --project src/CountOrSell.Cli -- sync --help
dotnet run --project src/CountOrSell.Cli -- images --help
dotnet run --project src/CountOrSell.Cli -- publish --help
dotnet run --project src/CountOrSell.Cli -- review --help
```
