# Installation

CountOrSell can be installed in three ways. The interactive `install.ps1` script is the recommended starting point for all three modes — it prompts for every required value and handles configuration automatically.

---

## Choosing an install method

| Method | Best for | Requirements |
|--------|----------|-------------|
| [Local](#local-install) | Development, single-machine use | .NET 8 SDK, Node.js 18+ |
| [Docker](#docker-install) | Self-hosted on any machine, consistent environment | Docker Desktop (or Docker Engine on Linux) |
| [Azure](#azure-install) | Cloud hosting, publicly accessible URL | Docker Desktop, Azure CLI, Azure subscription |

---

## Using `install.ps1`

The installer is a PowerShell script that works on Windows (5.1+), Linux, and macOS (PowerShell Core 7+).

```powershell
# Windows PowerShell / PowerShell Core
./install.ps1

# Linux / macOS
pwsh ./install.ps1

# Pre-select a mode and skip the menu
./install.ps1 -Mode local
./install.ps1 -Mode docker
./install.ps1 -Mode azure
```

**What it prompts for (all modes):**
- JWT secret key — enter your own (32+ chars) or press Enter to generate one
- New `cosadm` admin password — optional; leave blank and change it via the UI after first login

**Additional prompts by mode:**
- **Local** — API port, frontend port
- **Docker** — host port to expose (default 8080)
- **Azure** — subscription ID, resource group, region, ACR name, Container App name, custom domain (optional)

If you prefer to configure manually, follow the sections below.

---

## Local install

### System requirements

| Requirement | Minimum | Notes |
|-------------|---------|-------|
| .NET SDK | 8.0 | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Node.js | 18 LTS | [Download](https://nodejs.org/) — includes npm |
| Disk space | ~500 MB base | Grows with card image cache (~11 GB for full Scryfall image set) |
| RAM | 512 MB | API is lightweight; CLI sync peaks higher |
| OS | Windows 10+, macOS 12+, Ubuntu 20.04+ | |

Verify your tools are installed:

```bash
dotnet --version   # should print 8.x.x
node --version     # should print 18.x or higher
npm --version
```

### Get the code

```bash
git clone https://github.com/darkgrotto/CountOrSell
cd CountOrSell
```

Repository layout:

```
CountOrSell/
├── src/
│   ├── CountOrSell.Api/        # ASP.NET Core REST API
│   ├── CountOrSell.Core/       # Shared library (entities, services, DB context)
│   ├── CountOrSell.Cli/        # Admin CLI tool
│   └── countorsell-web/        # React frontend
├── docs/                       # This documentation
├── Dockerfile                  # Container build
├── docker-compose.yml          # Docker Compose configuration
├── .env.example                # Docker environment template
├── install.ps1                 # Interactive installer
├── start.sh                    # Linux/macOS/Git Bash startup script
├── start.bat                   # Windows cmd startup script
└── CountOrSell.sln
```

### Configure the JWT key

Open `src/CountOrSell.Api/appsettings.json` and replace the placeholder:

```json
{
  "Jwt": {
    "Key": "replace-this-with-a-random-string-of-at-least-32-characters",
    "Issuer": "CountOrSell",
    "Audience": "CountOrSellUsers",
    "AccessTokenExpirationMinutes": 60
  }
}
```

The key must be **at least 32 characters**. Anyone who knows this value can forge authentication tokens — keep it private. Generate one with:

```bash
# Linux / macOS
openssl rand -base64 48

# PowerShell
[Convert]::ToBase64String((New-Object byte[] 48 | ForEach-Object { Get-Random -Max 256 }))
```

### Start the app

```bash
./start.sh                                  # Linux / macOS / Git Bash
start.bat                                   # Windows cmd
```

Both scripts start the API on `http://localhost:5000` and the Vite dev server on `http://localhost:5173`. Open `http://localhost:5173` in your browser.

Custom ports:

```bash
./start.sh --api-port 7000 --web-port 3000
start.bat  --api-port 7000 --web-port 3000
```

See [Running](running.md) for more options.

---

## Docker install

### System requirements

| Requirement | Notes |
|-------------|-------|
| Docker Desktop | [Download](https://www.docker.com/products/docker-desktop/) — use **Linux container mode** (default on Windows) |
| Docker Compose | Included with Docker Desktop |

Docker Desktop for Windows runs Linux containers by default via WSL 2. CountOrSell uses Linux-based images; Windows container mode is not supported.

### Get the code

```bash
git clone https://github.com/darkgrotto/CountOrSell
cd CountOrSell
```

### Configure

Copy the environment template and set your JWT key:

```bash
cp .env.example .env
```

Edit `.env`:

```
JWT_KEY=your-secret-key-here-at-least-32-characters-long
COS_PORT=8080
```

`JWT_KEY` is required and must be at least 32 characters. `COS_PORT` is the host port — the container always listens on 8080 internally.

> **For Docker and Azure the JWT key is passed as an environment variable** (`Jwt__Key`), not written to `appsettings.json`. ASP.NET Core automatically reads `Jwt__Key` and maps it to `Jwt.Key` in configuration.

### Build and run

```bash
docker compose up -d
```

This builds the image (first run takes 2–5 minutes), starts the container, and mounts a named Docker volume (`countorsell_data`) at `/data` inside the container for persistent storage of the database and card images.

The app is available at `http://localhost:8080` (or the port you set in `.env`).

### Environment variables

All configuration is passed via environment variables. The full list:

| Variable | Default | Description |
|----------|---------|-------------|
| `Jwt__Key` | *(required)* | JWT signing secret — at least 32 chars |
| `Jwt__Issuer` | `CountOrSell` | JWT issuer claim |
| `Jwt__Audience` | `CountOrSellUsers` | JWT audience claim |
| `COS_DATABASE_PATH` | `/data/database/CountOrSell.db` | Path inside the container for the SQLite file |
| `COS_IMAGES_PATH` | `/data/images` | Path inside the container for card images |
| `ASPNETCORE_URLS` | `http://+:8080` | Listen address |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Set to `Development` to enable Swagger UI |

### How the container serves content

In production mode (the default), the ASP.NET Core API serves both:
- The REST API at `/api/...`
- The React frontend as static files from `wwwroot/`

A single port (8080) handles everything. No separate frontend server or reverse proxy is needed. The SPA fallback routes all non-API requests to `index.html` so React Router works correctly.

---

## Azure install

`install.ps1 -Mode azure` is strongly recommended for Azure deployments — it automates every step below and handles credentials, resource creation, image building, and custom domain binding in a single interactive session.

### What the installer creates

| Azure resource | Purpose |
|----------------|---------|
| Resource group | Logical container for all resources |
| Azure Container Registry (ACR) | Stores the Docker image |
| Storage account + file share | Persistent volume for the database and images (mounted at `/data`) |
| Container Apps environment | The managed execution environment |
| Container App | The running instance with 1 replica |

### Prerequisites

- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) (`az`)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (to build and push the image)
- An Azure subscription

### Manual steps (if not using `install.ps1`)

**1. Log in and select subscription**

```bash
az login
az account set --subscription <subscription-id>
```

**2. Create resource group**

```bash
az group create --name cos-rg --location eastus
```

**3. Create and populate ACR**

```bash
az acr create --name cosregistry --resource-group cos-rg --sku Basic
az acr login  --name cosregistry
docker build -t cosregistry.azurecr.io/countorsell:latest .
docker push     cosregistry.azurecr.io/countorsell:latest
```

**4. Create persistent storage**

```bash
az storage account create --name cosstorageacct --resource-group cos-rg \
  --location eastus --sku Standard_LRS

STORAGE_KEY=$(az storage account keys list --account-name cosstorageacct \
  --resource-group cos-rg --query '[0].value' --output tsv)

az storage share create --name cosdata --account-name cosstorageacct \
  --account-key $STORAGE_KEY
```

**5. Create Container Apps environment with storage**

```bash
az containerapp env create --name cos-env --resource-group cos-rg --location eastus

az containerapp env storage set \
  --name cos-env --resource-group cos-rg \
  --storage-name cosdata \
  --azure-file-account-name cosstorageacct \
  --azure-file-account-key $STORAGE_KEY \
  --azure-file-share-name cosdata \
  --access-mode ReadWrite
```

**6. Deploy the Container App**

```bash
az acr update --name cosregistry --admin-enabled true
ACR_USER=$(az acr credential show --name cosregistry --query username --output tsv)
ACR_PASS=$(az acr credential show --name cosregistry --query 'passwords[0].value' --output tsv)

az containerapp create \
  --name countorsell --resource-group cos-rg \
  --environment cos-env \
  --image cosregistry.azurecr.io/countorsell:latest \
  --registry-server cosregistry.azurecr.io \
  --registry-username $ACR_USER --registry-password $ACR_PASS \
  --target-port 8080 --ingress external \
  --min-replicas 1 --max-replicas 1 \
  --cpu 1 --memory 2Gi \
  --secrets "jwt-key=<your-jwt-key>" \
  --env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "Jwt__Issuer=CountOrSell" \
    "Jwt__Audience=CountOrSellUsers" \
    "Jwt__Key=secretref:jwt-key" \
    "COS_DATABASE_PATH=/data/database/CountOrSell.db" \
    "COS_IMAGES_PATH=/data/images"
```

**7. Mount the file share**

```bash
az containerapp update \
  --name countorsell --resource-group cos-rg \
  --volume name=cosdata,storageType=AzureFile,storageName=cosdata \
  --mount name=cosdata,mountPath=/data
```

**8. Get the URL**

```bash
az containerapp show --name countorsell --resource-group cos-rg \
  --query 'properties.configuration.ingress.fqdn' --output tsv
```

### Custom domain and SSL

Container Apps provides managed TLS for custom domains. Add a CNAME record pointing your domain to the Container App FQDN, then:

```bash
az containerapp hostname add \
  --hostname your.domain.com --name countorsell --resource-group cos-rg

az containerapp hostname bind \
  --hostname your.domain.com --name countorsell --resource-group cos-rg \
  --validation-method CNAME
```

Azure automatically provisions and renews an SSL certificate.

---

## First-time data setup

Regardless of install method, the database starts empty. Use the **Synchronize** option in the web UI to pull the current maintained dataset, or use the CLI:

```bash
# Sync set list only (fast, shows sets in UI immediately)
dotnet run --project src/CountOrSell.Cli -- sync --sets

# Sync cards for one set
dotnet run --project src/CountOrSell.Cli -- sync --set-code mh3

# Sync everything (20–40 minutes)
dotnet run --project src/CountOrSell.Cli -- sync --all
```

See [CLI Reference](cli.md) for full details, including how to point the CLI at a Docker or published deployment's database.

---

## Default admin account

A built-in admin account is created automatically on first startup:

| Username | Default password |
|----------|-----------------|
| `cosadm` | `wholeftjaceinchargeofdesign` |

**Change this password immediately.** The minimum password length is 15 characters.

After logging in:

1. Open the user menu (top-right)
2. Go to **Profile → Change Password**
3. Enter the current password and your new password

You can then create additional user accounts via the **Register** page and promote them to admin via **Admin → Users & Settings**.
