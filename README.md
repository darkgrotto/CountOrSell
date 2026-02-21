# CountOrSell

*Count or sell. We help you choose.*

CountOrSell is a self-hosted web application for tracking Magic: The Gathering collections. It covers owned cards per set, the Reserved List, booster packs, and professionally graded (slabbed) cards. Card and set data is cached locally from Scryfall so the app runs offline after the initial sync.

---

## Components

| Component | What it is |
|-----------|-----------|
| **API** (`CountOrSell.Api`) | ASP.NET Core 8 REST API — serves all data, handles auth |
| **Web frontend** (`countorsell-web`) | React 18 + TypeScript SPA — the browser UI |
| **Core library** (`CountOrSell.Core`) | Shared entities, services, and EF Core context |
| **CLI** (`CountOrSell.Cli`) | Admin tool — syncs Scryfall data, manages images, publishes update packages |
| **Database** | SQLite file, auto-created on first run |

---

## Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/) and npm

### 1. Clone and configure

```bash
git clone <repo-url>
cd CountOrSell
```

Edit `src/CountOrSell.Api/appsettings.json` and replace the JWT key with a random string of at least 32 characters:

```json
"Jwt": {
  "Key": "your-own-secret-key-at-least-32-chars"
}
```

### 2. Start everything

**Linux / macOS / Git Bash on Windows:**
```bash
./start.sh
```

**Windows (native cmd):**
```
start.bat
```

Both scripts start the API on `http://localhost:5000` and the frontend dev server on `http://localhost:5173`. Open `http://localhost:5173` in your browser.

### 3. Populate the database (first time only)

The database starts empty. Use the CLI to pull data from Scryfall:

```bash
# Sync all sets (fast, no cards yet)
dotnet run --project src/CountOrSell.Cli -- sync --sets

# Sync cards for a specific set
dotnet run --project src/CountOrSell.Cli -- sync --set-code mh3

# Or sync everything at once (slow, ~30 min for all sets + cards)
dotnet run --project src/CountOrSell.Cli -- sync --all
```

---

## Documentation

| Document | Contents |
|----------|---------|
| [Installation](docs/installation.md) | System requirements, first-time setup, configuration |
| [Running](docs/running.md) | Starting each service, ports, environment variables |
| [CLI Reference](docs/cli.md) | All `countorsell` CLI commands and options |
| [Database](docs/database.md) | Schema, location, update system, backups |
| [User Guide](docs/user-guide.md) | How to use the web application |

---

## Tech Stack

**Backend** — .NET 8, ASP.NET Core, Entity Framework Core, SQLite, QuestPDF, BCrypt.Net
**Frontend** — React 18, TypeScript, Vite, TanStack Query, React Router, Tailwind CSS
**Mobile** — Capacitor (iOS / Android shells, optional)
**Data** — Scryfall API (card/set data), MTGJSON (supplemental)
