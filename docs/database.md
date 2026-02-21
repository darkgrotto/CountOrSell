# Database

## Overview

CountOrSell uses **SQLite** via Entity Framework Core. There are no migration files to manage — the schema is created automatically using EF Core's `EnsureCreated()` plus a custom `EnsureSchemaUpToDate()` method for incremental additions to existing databases.

---

## Database File Location

The database file is named `CountOrSell.db` and lives next to the running binary.

| Context | Typical path |
|---------|-------------|
| API (development) | `src/CountOrSell.Api/bin/Debug/net8.0/CountOrSell.db` |
| API (published) | Same directory as `CountOrSell.Api.exe` / `CountOrSell.Api` |
| CLI (development) | `src/CountOrSell.Cli/bin/Debug/net8.0/CountOrSell.db` |

> In development the API and CLI each have their **own separate database file**. To share data, run both tools against the same file by publishing the API and placing the CLI output in the same directory.

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
| `CardOwnerships` | Which cards a user owns, per set |
| `ReserveListCardOwnerships` | Reserved-list card ownership per user |
| `BoosterDefinitions` | Tracked booster packs per user |
| `SlabbedCards` | Professionally graded cards per user |
| `UserSubmissions` | Proposed data changes awaiting admin review |
| `UserSubmissionItems` | Individual change items within a submission |

### Key constraints

- `Users.Username` — unique
- `RefreshTokens.Token` — unique
- `CardOwnerships (UserId, ScryfallCardId)` — unique
- `ReserveListCardOwnerships (UserId, ScryfallCardId)` — unique
- `BoosterDefinitions (UserId, SetCode, BoosterType, ArtVariant)` — unique
- `SlabbedCards (UserId, GradingCompany, CertificationNumber)` — unique
- `SetTags (SetCode, Tag)` — unique

---

## Schema Management

The database is never migrated through EF Core migrations. Instead, two methods run on every startup:

```
db.Database.EnsureCreated()   → creates all tables that EF knows about if the file is new
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

SQLite is a single file. Back it up by copying `CountOrSell.db` while the API is stopped, or use the [SQLite Online Backup API](https://www.sqlite.org/backup.html) for a hot backup.

```bash
# Simple file copy (stop API first, or use SQLite's backup mode)
cp CountOrSell.db "CountOrSell-$(date +%Y%m%d).db"
```

User data (accounts, ownerships, boosters, slabs) is only in this file. Card/set data can be re-synced from Scryfall at any time using the CLI.

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

The manifest JSON format that the API expects:

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
      "description": "Delta from 2025.05.01.0900: 12 new/updated sets, 400 new/updated cards",
      "downloadUrl": "https://example.com/dbupdate/packages/delta-2025.05.01.0900-to-2025.06.01.1200.zip",
      "fileSizeBytes": 524288,
      "checksum": "DEF456..."
    }
  ]
}
```

Host this file at the URL configured in `UpdatesController.cs` (`ManifestUrl` constant).

---

## Browsing the Database

Any SQLite client can open `CountOrSell.db` directly.

- [DB Browser for SQLite](https://sqlitebrowser.org/) — free GUI for Windows/macOS/Linux
- [DBeaver](https://dbeaver.io/) — full-featured IDE
- `sqlite3` — command line: `sqlite3 CountOrSell.db .tables`
