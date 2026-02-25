# Database

## Overview

CountOrSell uses **SQLite** via Entity Framework Core. There are no migration files to manage — the schema is created automatically using EF Core's `EnsureCreated()` plus a custom `EnsureSchemaUpToDate()` method for incremental additions to existing databases.

---

## Database File Location

The database file is named `CountOrSell.db`. Its location depends on the deployment mode.

| Deployment | Path |
|------------|------|
| Local — development | `src/CountOrSell.Api/database/CountOrSell.db` |
| Local — published | Same directory as the `CountOrSell.Api` executable |
| CLI — development | `src/CountOrSell.Cli/bin/Debug/net8.0/CountOrSell.db` |
| Docker | `/data/database/CountOrSell.db` inside the container (named volume `countorsell_data`) |
| Azure Container Apps | `/data/database/CountOrSell.db` inside the container (Azure Files share `cosdata`) |

The path can be overridden with the `COS_DATABASE_PATH` environment variable — the API and CLI both respect it.

> **Development note:** In development the API and CLI each use their own separate database file. To share data, run both against the same file by using `COS_DATABASE_PATH` or by publishing the API and placing the CLI output in the same directory.

---

## Schema

### Shared / cached data (read-only for users)

| Table | Purpose |
|-------|---------|
| `CachedSets` | All MTG sets fetched from Scryfall |
| `CachedCards` | All MTG cards fetched from Scryfall |
| `SetTags` | Tags applied to sets (e.g. `commander`, `masters`) |
| `DatabaseUpdatePackages` | Record of applied update packages |

### User data

| Table | Purpose |
|-------|---------|
| `Users` | Registered accounts (username, bcrypt password hash) |
| `RefreshTokens` | JWT refresh tokens (rotated on each use) |
| `CardOwnerships` | Which cards a user owns, including variant and quantity |
| `ReserveListCardOwnerships` | Reserved-list card ownership per user |
| `BoosterDefinitions` | Tracked booster packs per user |
| `SlabbedCards` | Professionally graded cards per user |
| `UserSubmissions` | Proposed data changes awaiting admin review |
| `UserSubmissionItems` | Individual change items within a submission |
| `AppSettings` | Global settings (e.g. registrations enabled) |

### Key constraints

- `Users.Username` — unique
- `RefreshTokens.Token` — unique
- `CardOwnerships (UserId, ScryfallCardId, Variant)` — unique
- `ReserveListCardOwnerships (UserId, ScryfallCardId)` — unique
- `BoosterDefinitions (UserId, SetCode, BoosterType, ArtVariant)` — unique
- `SlabbedCards (UserId, GradingCompany, CertificationNumber)` — unique
- `SetTags (SetCode, Tag)` — unique

---

## Schema Management

The database is never migrated through EF Core migrations. Instead, two methods run on every API startup:

```
db.Database.EnsureCreated()   → creates all tables if the file is new
db.EnsureSchemaUpToDate()     → runs CREATE TABLE IF NOT EXISTS for tables added after initial creation
```

When adding a new table:

1. Create the entity class in `CountOrSell.Core/Entities/`
2. Add a `DbSet<T>` property in `CountOrSellDbContext`
3. Add entity configuration in `OnModelCreating`
4. Add a `CREATE TABLE IF NOT EXISTS` block in `EnsureSchemaUpToDate()` with any needed indexes

This pattern is safe to run repeatedly — `IF NOT EXISTS` makes it idempotent.

---

## Backups

### Local

SQLite is a single file. Back it up by copying `CountOrSell.db` while the API is stopped, or use the [SQLite Online Backup API](https://www.sqlite.org/backup.html) for a hot backup.

```bash
# Simple copy (stop the API first, or use SQLite backup mode for a hot copy)
cp src/CountOrSell.Api/database/CountOrSell.db \
   "CountOrSell-$(date +%Y%m%d).db"
```

### Docker

The database lives inside the named volume `countorsell_data`. Back it up by copying the file out of the volume:

```bash
# Copy the database to the host
docker run --rm \
  -v countorsell_data:/data \
  -v $(pwd):/backup \
  alpine cp /data/database/CountOrSell.db /backup/CountOrSell-backup.db
```

Or stop the container and access the volume directly:

```bash
docker compose down
docker run --rm \
  -v countorsell_data:/data \
  -v $(pwd):/backup \
  alpine cp /data/database/CountOrSell.db /backup/CountOrSell-backup.db
docker compose up -d
```

To restore:

```bash
docker compose down
docker run --rm \
  -v countorsell_data:/data \
  -v $(pwd):/backup \
  alpine cp /backup/CountOrSell-backup.db /data/database/CountOrSell.db
docker compose up -d
```

### Azure

The database is stored on an Azure Files share (`cosdata`). Back it up using `azcopy` or the Azure Portal:

```bash
# Using azcopy (recommended for large databases)
azcopy copy \
  'https://<storage-account>.file.core.windows.net/cosdata/database/CountOrSell.db<SAS-token>' \
  './CountOrSell-backup.db'
```

Or download via the Azure Portal: **Storage Account → File shares → cosdata → database → CountOrSell.db → Download**.

User data (accounts, ownerships, boosters, slabs) is only in this file. Card and set data can be re-synced from Scryfall at any time using the CLI.

---

## Docker and Azure

### Persistent storage

The `/data` directory inside the container is the root for all persistent data. It contains two subdirectories:

| Path | Contents |
|------|---------|
| `/data/database/` | SQLite database file |
| `/data/images/` | Cached card images |

**Docker:** Mounted from the named volume `countorsell_data`.
**Azure:** Mounted from the Azure Files share `cosdata`.

Neither the database nor the images are baked into the Docker image — they are always stored in the mounted volume.

### Running the CLI against a Docker database

The CLI runs on the host and cannot directly access the container's volume path. Options:

**Option 1 — Copy the database out, operate, copy back:**

```bash
# Extract
docker run --rm -v countorsell_data:/data -v $(pwd):/work \
  alpine cp /data/database/CountOrSell.db /work/CountOrSell.db

# Run CLI against the copy
COS_DATABASE_PATH=./CountOrSell.db \
  dotnet run --project src/CountOrSell.Cli -- sync --sets

# Push back (stop the API container first)
docker compose down
docker run --rm -v countorsell_data:/data -v $(pwd):/work \
  alpine cp /work/CountOrSell.db /data/database/CountOrSell.db
docker compose up -d
```

**Option 2 — Run the CLI from inside the container:**

```bash
# Install the .NET SDK in the container (temporary)
docker exec -it countorsell bash
# Then run dotnet commands against the database at /data/database/CountOrSell.db
```

**Option 3 — Use the web UI sync** (recommended for card data updates):

The in-app **Synchronize** feature downloads a pre-built database package directly, which is faster and requires no CLI access.

---

## Update System

CountOrSell includes an in-app update mechanism for keeping card and set data current without requiring users to run the CLI.

### How it works

1. The frontend shows an **Update** indicator (the `UpdateCheckPanel` component)
2. It calls `GET /api/updates/check`, which fetches a manifest from `https://www.countorsell.com/dbupdate`
3. If a newer version exists, the user is prompted to apply it
4. Clicking apply calls `POST /api/updates/apply` (requires login)
5. The API downloads the best available package (delta preferred, full fallback), extracts `data.db`, and upserts all records into the local database

### Package types

| Type | Description |
|------|-------------|
| **Full** | Complete snapshot of all sets and cards |
| **Delta** | Only records changed since a specific previous version |

The client automatically prefers a delta from the current version. If no matching delta exists, it falls back to the full package.

### Building your own update packages

If you are self-hosting and want to distribute data updates to multiple instances, use the CLI `publish` command. See [CLI Reference](cli.md#publish--build-update-packages) for details.

The manifest JSON format:

```json
{
  "currentVersion": "2025.06.01.1200",
  "packages": [
    {
      "version": "2025.06.01.1200",
      "type": "full",
      "description": "Full database: 750 sets, 30000 cards",
      "downloadUrl": "https://example.com/dbupdate/packages/full-2025.06.01.1200.zip",
      "fileSizeBytes": 4194304,
      "checksum": "ABC123..."
    },
    {
      "version": "2025.06.01.1200",
      "fromVersion": "2025.05.01.0900",
      "type": "delta",
      "description": "Delta from 2025.05.01.0900: 12 sets, 400 cards",
      "downloadUrl": "https://example.com/dbupdate/packages/delta-2025.05.01.0900-to-2025.06.01.1200.zip",
      "fileSizeBytes": 524288,
      "checksum": "DEF456..."
    }
  ]
}
```

---

## Browsing the Database

Any SQLite client can open `CountOrSell.db` directly.

- [DB Browser for SQLite](https://sqlitebrowser.org/) — free GUI for Windows/macOS/Linux
- [DBeaver](https://dbeaver.io/) — full-featured IDE
- `sqlite3` — command line: `sqlite3 CountOrSell.db .tables`
