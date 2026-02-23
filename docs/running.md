# Running

## The Startup Scripts

The easiest way to run both services together is the included startup script.

Both scripts accept optional flags to override the default ports:

| Flag | Default | Description |
|------|---------|-------------|
| `--api-port PORT` | `5000` | Port for the ASP.NET Core API |
| `--web-port PORT` | `5173` | Port for the Vite dev server |

### Linux / macOS / Git Bash

```bash
./start.sh                                    # defaults (5000 / 5173)
./start.sh --api-port 7000 --web-port 3000    # custom ports
```

```
[12:00:00] CountOrSell — starting services
────────────────────────────────────────
[12:00:00] Restoring .NET packages...
[12:00:02] Starting API on http://localhost:5000 ...
[12:00:04] API is ready.
[12:00:04] Installing frontend dependencies...
[12:00:06] Starting frontend dev server...

════════════════════════════════════════
  API           http://localhost:5000
  API (Swagger)  http://localhost:5000/swagger
  Frontend      http://localhost:5173
════════════════════════════════════════
  Press Ctrl+C to stop all services.
```

Press **Ctrl+C** to stop both services cleanly.

### Windows (native cmd)

```
start.bat                                        :: defaults (5000 / 5173)
start.bat --api-port 7000 --web-port 3000        :: custom ports
```

On Windows, `start.bat` opens the API and frontend each in their own console window. Close those windows to stop the services. The original window can be closed once both services are running.

### What the scripts do

1. Check that `dotnet` and `npm` are on the PATH
2. Kill any process already listening on port 5000
3. Clear the Vite dependency cache (prevents EPERM errors on OneDrive-synced paths)
4. Run `dotnet restore` on the solution
5. Start the API with `ASPNETCORE_ENVIRONMENT=Development`
6. Wait up to 30 seconds for the API to accept TCP connections
7. Run `npm install` in the frontend directory
8. Start the Vite dev server

---

## Running Services Individually

### API only

```bash
cd src/CountOrSell.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --urls http://localhost:5000
```

On Windows PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/CountOrSell.Api --urls http://localhost:5000
```

The API automatically creates the SQLite database and runs schema migrations on startup. No manual database setup is needed.

### Frontend only

```bash
cd src/countorsell-web
npm install          # first time only
npm run dev
```

The frontend dev server starts on `http://localhost:5173`. It proxies all `/api` requests to `http://localhost:5000`, so the API must be running separately.

### CLI

The CLI is run with `dotnet run` against the `CountOrSell.Cli` project. See [CLI Reference](cli.md) for all commands.

```bash
dotnet run --project src/CountOrSell.Cli -- <command> [options]
```

---

## Ports

| Service | Default port | Configurable |
|---------|-------------|--------------|
| API | 5000 | `--api-port` flag on startup scripts, or `--urls` / `ASPNETCORE_URLS` when running manually |
| Frontend dev server | 5173 | `--web-port` flag on startup scripts, or `--port` passed to Vite when running manually |
| Swagger UI | 5000 | Same as API, at `/swagger` |

If port 5000 is already in use, the startup scripts will attempt to kill the conflicting process. To use a different port, pass the flags directly:

```bash
# Linux / macOS / Git Bash
./start.sh --api-port 7000 --web-port 3000

# Windows
start.bat --api-port 7000 --web-port 3000
```

> **Note:** When changing the API port, the Vite proxy must also know the new address. The `--api-port` flag updates the API process only; if you run the frontend separately you will need to update `vite.config.ts` → `server.proxy['/api'].target` to match. When using the startup scripts the proxy target is not automatically updated — this is only relevant if you run the two services independently.

---

## Environment Variables

| Variable | Default | Effect |
|----------|---------|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Set to `Development` to enable Swagger UI and detailed error pages |
| `ASPNETCORE_URLS` | `http://localhost:5000` | Override the API listen address |

---

## Production Build

To build the frontend for production:

```bash
cd src/countorsell-web
npm run build
```

Output goes to `src/countorsell-web/dist/`. Serve those static files with any web server and configure a `/api` reverse proxy to the API.

To publish the API:

```bash
dotnet publish src/CountOrSell.Api \
  -c Release \
  -o ./publish/api \
  --nologo
```

The `CountOrSell.db` file will be created in the same directory as the published executable on first run.

---

## Swagger / OpenAPI

When running in `Development` mode, the full Swagger UI is available at:

```
http://localhost:5000/swagger
```

All API endpoints are documented there. Endpoints marked with the lock icon require a JWT bearer token. Click **Authorize** and paste `Bearer <your-token>` (the token is returned by the login or register endpoints).
