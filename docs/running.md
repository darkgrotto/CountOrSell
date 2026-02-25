# Running

CountOrSell can be run in three modes. Choose the one that matches your install method.

---

## Local (start scripts)

The startup scripts manage the API and frontend dev server together.

### Linux / macOS / Git Bash

```bash
./start.sh                                    # defaults (API: 5000, frontend: 5173)
./start.sh --api-port 7000 --web-port 3000    # custom ports
```

### Windows (native cmd)

```
start.bat                                        :: defaults (5000 / 5173)
start.bat --api-port 7000 --web-port 3000        :: custom ports
```

On Windows, `start.bat` opens the API and frontend each in a separate console window.

### Port flags

| Flag | Default | Description |
|------|---------|-------------|
| `--api-port PORT` | `5000` | Port for the ASP.NET Core API |
| `--web-port PORT` | `5173` | Port for the Vite dev server |

### What the scripts do

1. Check that `dotnet` and `npm` are on the PATH
2. Kill any process already listening on port 5000 (or the chosen API port)
3. Clear the Vite dependency cache (prevents EPERM errors on OneDrive-synced paths)
4. Run `dotnet restore` on the solution
5. Start the API with `ASPNETCORE_ENVIRONMENT=Development`
6. Wait up to 30 seconds for the API to accept TCP connections
7. Run `npm install` in the frontend directory
8. Start the Vite dev server

Press **Ctrl+C** to stop both services.

### Services

| Service | Default URL | Notes |
|---------|------------|-------|
| API | `http://localhost:5000` | REST API + auth |
| Swagger UI | `http://localhost:5000/swagger` | Available in Development mode |
| Frontend | `http://localhost:5173` | Vite dev server; proxies `/api` to the API |

---

## Docker

### Start

```bash
docker compose up -d
```

The first run builds the image (~2–5 minutes). Subsequent starts are instant.

Open `http://localhost:8080` (or the port configured in `.env` as `COS_PORT`).

### Stop

```bash
docker compose down
```

This stops and removes the container but preserves the `countorsell_data` volume (your database and images).

To also remove the volume (destroys all data):

```bash
docker compose down -v
```

### Logs

```bash
docker compose logs -f              # follow live output
docker compose logs --tail=100      # last 100 lines
```

### Restart / update

```bash
# Restart the container without rebuilding
docker compose restart

# Rebuild the image after a code change, then restart
docker compose up -d --build
```

### Environment variables

Configuration is passed through `.env`. All options:

| Variable | Default | Description |
|----------|---------|-------------|
| `JWT_KEY` | *(required)* | JWT signing secret, passed as `Jwt__Key` inside the container |
| `COS_PORT` | `8080` | Host port mapped to the container's port 8080 |

Advanced overrides can be added directly under `environment:` in `docker-compose.yml`:

| Container env var | Default | Description |
|-------------------|---------|-------------|
| `Jwt__Issuer` | `CountOrSell` | JWT issuer claim |
| `Jwt__Audience` | `CountOrSellUsers` | JWT audience claim |
| `COS_DATABASE_PATH` | `/data/database/CountOrSell.db` | SQLite file path inside the container |
| `COS_IMAGES_PATH` | `/data/images` | Card images root inside the container |
| `ASPNETCORE_URLS` | `http://+:8080` | Listen address |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Set to `Development` to enable Swagger UI |

### Accessing the container

```bash
# Open a shell inside the running container
docker exec -it countorsell bash

# Run a one-off command
docker exec countorsell ls /data/database/
```

### Persistent data

The named volume `countorsell_data` is mounted at `/data` inside the container.

| Path in container | Contents |
|-------------------|---------|
| `/data/database/CountOrSell.db` | SQLite database |
| `/data/images/` | Cached card images |

The volume persists across container stops, restarts, and image updates. See [Database](database.md#docker-and-azure) for backup instructions.

---

## Azure (Container Apps)

### View the app URL

```bash
az containerapp show \
  --name <app-name> --resource-group <rg> \
  --query 'properties.configuration.ingress.fqdn' --output tsv
```

### Follow live logs

```bash
az containerapp logs show \
  --name <app-name> --resource-group <rg> \
  --follow
```

### Restart the container

```bash
az containerapp revision restart \
  --name <app-name> --resource-group <rg> \
  --revision $(az containerapp revision list \
      --name <app-name> --resource-group <rg> \
      --query '[0].name' --output tsv)
```

### Update to a new image

Build, push, and redeploy in three commands:

```bash
docker build -t <acr>.azurecr.io/countorsell:latest .
docker push     <acr>.azurecr.io/countorsell:latest
az containerapp update \
  --name <app-name> --resource-group <rg> \
  --image <acr>.azurecr.io/countorsell:latest
```

### Scale

The app is deployed with `--min-replicas 1 --max-replicas 1` to keep SQLite consistent (multiple replicas would each need their own database). If you migrate to a server-based database engine in the future, scaling becomes straightforward.

### Change a secret (e.g. JWT key rotation)

```bash
az containerapp secret set \
  --name <app-name> --resource-group <rg> \
  --secrets "jwt-key=<new-key>"

az containerapp update \
  --name <app-name> --resource-group <rg> \
  --set-env-vars "Jwt__Key=secretref:jwt-key"
```

### Tear down

```bash
# Delete the Container App only (keeps storage and ACR)
az containerapp delete --name <app-name> --resource-group <rg>

# Delete everything (irreversible — back up your database first)
az group delete --name <rg>
```

---

## Running services individually (local)

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

The API automatically creates the SQLite database and runs schema updates on startup.

### Frontend only

```bash
cd src/countorsell-web
npm install          # first time only
npm run dev
```

The Vite dev server starts on `http://localhost:5173` and proxies all `/api` requests to `http://localhost:5000`.

### CLI

```bash
dotnet run --project src/CountOrSell.Cli -- <command> [options]
```

See [CLI Reference](cli.md) for all commands.

---

## Production build (local / self-hosted)

To deploy without Docker on a server, build the frontend and publish the API:

```bash
# 1. Build the React frontend
cd src/countorsell-web
npm install
npm run build
# Output: src/countorsell-web/dist/

# 2. Publish the API
dotnet publish src/CountOrSell.Api \
  -c Release -o ./publish/api \
  --nologo

# 3. Copy the frontend build into wwwroot
cp -r src/countorsell-web/dist ./publish/api/wwwroot
```

The API serves both the REST endpoints and the static frontend files when `ASPNETCORE_ENVIRONMENT` is not `Development`.

Set the JWT key via environment variable rather than modifying `appsettings.json`:

```bash
export Jwt__Key="your-secret-key"
export ASPNETCORE_ENVIRONMENT=Production
./publish/api/CountOrSell.Api
```

---

## Swagger / OpenAPI

When `ASPNETCORE_ENVIRONMENT=Development`, the Swagger UI is available at:

```
http://localhost:5000/swagger
```

Endpoints marked with the lock icon require a JWT bearer token. Click **Authorize** and paste `Bearer <your-token>` (the token is returned by the login endpoint).

To enable Swagger in Docker, add `ASPNETCORE_ENVIRONMENT=Development` to the `environment:` section of `docker-compose.yml`.
