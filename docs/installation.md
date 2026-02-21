# Installation

## System Requirements

| Requirement | Minimum | Notes |
|-------------|---------|-------|
| .NET SDK | 8.0 | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Node.js | 18 LTS | [Download](https://nodejs.org/) — includes npm |
| Disk space | ~500 MB | Grows with card image cache (~200 MB for full Scryfall image set) |
| RAM | 512 MB | API is lightweight; CLI sync peaks higher |
| OS | Windows 10+, macOS 12+, Ubuntu 20.04+ | |

Verify your tools are installed:

```bash
dotnet --version   # should print 8.x.x
node --version     # should print 18.x or higher
npm --version
```

---

## Getting the Code

```bash
git clone <repo-url>
cd CountOrSell
```

The repository layout:

```
CountOrSell/
├── src/
│   ├── CountOrSell.Api/        # ASP.NET Core REST API
│   ├── CountOrSell.Core/       # Shared library (entities, services, DB context)
│   ├── CountOrSell.Cli/        # Admin CLI tool
│   └── countorsell-web/        # React frontend
├── docs/                       # This documentation
├── start.sh                    # Linux/macOS/Git Bash startup script
├── start.bat                   # Windows cmd startup script
└── CountOrSell.sln
```

---

## Configuration

### JWT Secret Key

Open `src/CountOrSell.Api/appsettings.json` and replace the placeholder key:

```json
{
  "Jwt": {
    "Key": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "CountOrSell",
    "Audience": "CountOrSellUsers",
    "AccessTokenExpirationMinutes": 60
  }
}
```

**Required changes for production:**

- `Key` — Replace with a randomly generated string of **at least 32 characters**. Anyone who knows this key can forge authentication tokens. Generate one with:
  ```bash
  # Linux/macOS
  openssl rand -base64 32

  # PowerShell
  [Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Max 256 }))
  ```
- `AccessTokenExpirationMinutes` — Default is 60 minutes. Increase for convenience, decrease for tighter security.

The `Issuer` and `Audience` values can remain as-is for a self-hosted instance.

### Database Location

The SQLite database file (`CountOrSell.db`) is created automatically alongside the API binary when it first runs. In development this is typically:

```
src/CountOrSell.Api/bin/Debug/net8.0/CountOrSell.db
```

In a published deployment the file sits next to the API executable. See [Database](database.md) for details.

### Frontend Proxy

The Vite dev server proxies all `/api` requests to `http://localhost:5000`. This is configured in `src/countorsell-web/vite.config.ts` and requires no changes for local development. For a production deployment you would configure a reverse proxy (nginx, Caddy, IIS) to forward `/api` to the API service.

---

## First-Time Setup

After starting the services (see [Running](running.md)), the database is empty. You must populate it from Scryfall before the app is useful.

### Minimal setup (one set)

```bash
# Sync just the set list first
dotnet run --project src/CountOrSell.Cli -- sync --sets

# Then sync cards for one set, e.g. Modern Horizons 3
dotnet run --project src/CountOrSell.Cli -- sync --set-code mh3
```

### Full setup (all sets and cards)

```bash
dotnet run --project src/CountOrSell.Cli -- sync --all
```

This downloads metadata for every MTG set and all their cards. It takes 20–40 minutes depending on your internet speed due to Scryfall API rate limiting.

### Downloading card images (optional)

Card images are served through the API from a local cache. Without images the app falls back to direct Scryfall image URLs (requires internet while browsing).

```bash
# Download images for one set
dotnet run --project src/CountOrSell.Cli -- images --set-code mh3

# Download all missing images across every cached set
dotnet run --project src/CountOrSell.Cli -- images --all
```

See [CLI Reference](cli.md) for all sync and image options.

---

## Creating Your Account

There is no pre-seeded admin account. Register through the web UI:

1. Open `http://localhost:5173`
2. Click **Login** in the top-right corner
3. Switch to the **Register** tab
4. Enter a username, password (minimum 6 characters), and an optional display name

The first registered account has no special privileges — all accounts are equal. If you want to restrict registration, you can disable the registration endpoint in `AuthController.cs` after creating your account.
