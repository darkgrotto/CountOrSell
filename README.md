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
| **Docker** | `Dockerfile` + `docker-compose.yml` for containerised deployment |

---

## Quick Start

### Option A — Automated installer (recommended)

`install.ps1` is an interactive PowerShell script that works on Windows, Linux, and macOS (PowerShell 5.1+ or PowerShell Core 7+). It guides you through every step and supports three deployment modes.

```powershell
# Windows PowerShell / PowerShell Core
./install.ps1

# Linux / macOS (requires PowerShell Core)
pwsh ./install.ps1
```

Choose your mode at the prompt:

| Mode | Description |
|------|-------------|
| **Local** | Configure and run with `start.sh` / `start.bat` on the current machine |
| **Docker** | Build a Docker image and run with `docker compose` |
| **Azure** | Build, push to Azure Container Registry, and deploy to Azure Container Apps |

The script prompts for all required values — JWT secret, admin password, ports, Azure credentials — and handles every configuration step automatically.

---

### Option B — Manual local setup

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and [Node.js 18+](https://nodejs.org/)

**1. Clone**

```bash
git clone <repo-url>
cd CountOrSell
```

**2. Configure**

Edit `src/CountOrSell.Api/appsettings.json` and replace the JWT key with a random string of at least 32 characters:

```json
"Jwt": {
  "Key": "your-own-secret-key-at-least-32-chars"
}
```

**3. Start**

```bash
./start.sh                                  # Linux / macOS / Git Bash
start.bat                                   # Windows cmd
```

Both scripts start the API on `http://localhost:5000` and the frontend on `http://localhost:5173`.
Open `http://localhost:5173` in your browser.

To use different ports:

```bash
./start.sh --api-port 7000 --web-port 3000   # Linux / macOS / Git Bash
start.bat --api-port 7000 --web-port 3000    # Windows
```

---

### Option C — Docker

**Prerequisites:** [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Linux container mode)

```bash
cp .env.example .env
# Edit .env — set JWT_KEY to a random 32+ character string
docker compose up -d
```

The app is available at `http://localhost:8080`. Change the port by setting `COS_PORT` in `.env`.

---

## Populate the database (first time only)

The database starts empty. Use the **Synchronize** option in the web UI to pull the current maintained dataset, or use the CLI to build from Scryfall directly:

```bash
# Sync all sets (fast)
dotnet run --project src/CountOrSell.Cli -- sync --sets

# Sync cards for a specific set
dotnet run --project src/CountOrSell.Cli -- sync --set-code mh3

# Sync everything (slow — 20-40 minutes)
dotnet run --project src/CountOrSell.Cli -- sync --all
```

Card images are optional — without them the app falls back to direct Scryfall URLs. The full image set is ~11 GB and ~112,000 files. Use the UI sync or:

```bash
dotnet run --project src/CountOrSell.Cli -- images --set-code mh3   # one set
dotnet run --project src/CountOrSell.Cli -- images --all             # everything
```

---

## Default Admin Account

A built-in admin account is created on first startup:

| Username | Password |
|----------|----------|
| `cosadm` | `wholeftjaceinchargeofdesign` |

**Change this password immediately after first login.** Go to the user menu → **Profile** → **Change Password**. The minimum password length is 15 characters.

Once the password has been changed, you can create regular user accounts and promote them to admin via the **Admin** panel, or continue using the `cosadm` account.

---

## Documentation

| Document | Contents |
|----------|---------|
| [Installation](docs/installation.md) | All install methods — local, Docker, Azure — with full configuration reference |
| [Running](docs/running.md) | Starting, stopping, and managing each deployment mode |
| [CLI Reference](docs/cli.md) | All `countorsell` CLI commands and options |
| [Database](docs/database.md) | Schema, location, backups, update system |
| [User Guide](docs/user-guide.md) | How to use the web application |

---

## Tech Stack

**Backend**

.NET 8, ASP.NET Core, Entity Framework Core, SQLite, QuestPDF, BCrypt.Net

**Frontend**

React 18, TypeScript, Vite, TanStack Query, React Router, Tailwind CSS

**Mobile**

Capacitor (iOS / Android shells, optional)

**Deployment**

Docker (Linux containers), Azure Container Apps, Azure Container Registry, Azure Files

**Data**

Scryfall API (card/set data), MTGJSON (supplemental)
