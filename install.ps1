#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    CountOrSell installation and deployment script.

.DESCRIPTION
    Interactive script that configures and deploys CountOrSell in one of
    three modes:

      Local  — configure appsettings.json and run via start.sh / start.bat
      Docker — build a Docker image and run with docker compose
      Azure  — build, push to Azure Container Registry, and deploy to
               Azure Container Apps with persistent Azure Files storage

.PARAMETER Mode
    Pre-select a mode: local, docker, or azure (skips the menu).

.EXAMPLE
    ./install.ps1
    ./install.ps1 -Mode docker
    ./install.ps1 -Mode azure
#>
param(
    [ValidateSet('local','docker','azure','')]
    [string]$Mode = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Colour helpers ────────────────────────────────────────────────────────────
function Write-Banner {
    Write-Host ''
    Write-Host '╔══════════════════════════════════════════════════╗' -ForegroundColor Cyan
    Write-Host '║          CountOrSell  Install Script             ║' -ForegroundColor Cyan
    Write-Host '║  Local · Docker · Azure                         ║' -ForegroundColor Cyan
    Write-Host '╚══════════════════════════════════════════════════╝' -ForegroundColor Cyan
    Write-Host ''
}

function Write-Section  { param($t) Write-Host "`n── $t " -ForegroundColor Cyan }
function Write-Ok       { param($t) Write-Host "  ✓ $t"  -ForegroundColor Green }
function Write-Warn     { param($t) Write-Host "  ⚠ $t"  -ForegroundColor Yellow }
function Write-Err      { param($t) Write-Host "  ✗ $t"  -ForegroundColor Red }
function Write-Info     { param($t) Write-Host "  · $t"  -ForegroundColor Gray }

# ── Utility helpers ───────────────────────────────────────────────────────────

function Assert-Command {
    param([string]$Cmd, [string]$InstallHint)
    if (-not (Get-Command $Cmd -ErrorAction SilentlyContinue)) {
        Write-Err "'$Cmd' not found. $InstallHint"
        exit 1
    }
    Write-Ok "$Cmd found"
}

function New-JwtKey {
    $bytes = New-Object byte[] 48
    $rng   = [System.Security.Cryptography.RNGCryptoServiceProvider]::new()
    $rng.GetBytes($bytes)
    $rng.Dispose()
    return [Convert]::ToBase64String($bytes)
}

function Read-NonEmpty {
    param([string]$Prompt, [string]$Default = '')
    while ($true) {
        $raw = Read-Host $Prompt
        if ($raw.Trim() -ne '') { return $raw.Trim() }
        if ($Default -ne '')     { return $Default }
        Write-Warn 'This field is required.'
    }
}

function Read-Secret {
    param([string]$Prompt, [int]$MinLen = 0)
    while ($true) {
        $ss  = Read-Host $Prompt -AsSecureString
        $val = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
                   [Runtime.InteropServices.Marshal]::SecureStringToBSTR($ss))
        if ($val.Length -ge $MinLen) { return $val }
        Write-Warn "Must be at least $MinLen characters."
    }
}

function Read-Port {
    param([string]$Prompt, [int]$Default)
    while ($true) {
        $raw = Read-Host "$Prompt [default: $Default]"
        if ($raw.Trim() -eq '') { return $Default }
        if ([int]::TryParse($raw.Trim(), [ref]$null) -and [int]$raw -gt 0 -and [int]$raw -le 65535) {
            return [int]$raw
        }
        Write-Warn 'Enter a valid port number (1-65535).'
    }
}

function Read-YesNo {
    param([string]$Prompt, [bool]$Default = $false)
    $hint = if ($Default) { '[Y/n]' } else { '[y/N]' }
    $raw  = Read-Host "$Prompt $hint"
    if ($raw.Trim() -eq '') { return $Default }
    return $raw.Trim() -match '^[Yy]'
}

# Wait for the API health endpoint to respond (up to $Seconds seconds)
function Wait-ForApi {
    param([string]$Url, [int]$Seconds = 60)
    Write-Info "Waiting for API at $Url ..."
    for ($i = 1; $i -le $Seconds; $i++) {
        try {
            $r = Invoke-RestMethod "$Url/api/auth/registration-status" -Method Get -TimeoutSec 3 -ErrorAction Stop
            Write-Ok "API is ready."
            return $true
        } catch { }
        Start-Sleep 1
    }
    Write-Warn "API did not respond within $Seconds s — you may need to change the admin password manually."
    return $false
}

# Log in as cosadm and return a bearer token
function Get-AdminToken {
    param([string]$BaseUrl, [string]$Password)
    try {
        $body = @{ username = 'cosadm'; password = $Password } | ConvertTo-Json
        $r    = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post `
                    -Body $body -ContentType 'application/json' -ErrorAction Stop
        return $r.token
    } catch {
        return $null
    }
}

# Change cosadm's password via the API
function Set-AdminPassword {
    param([string]$BaseUrl, [string]$CurrentPwd, [string]$NewPwd)
    $token = Get-AdminToken $BaseUrl $CurrentPwd
    if (-not $token) {
        Write-Warn "Could not log in as cosadm — password not changed."
        Write-Info "Log in at $BaseUrl and change it via Profile → Change Password."
        return
    }
    try {
        $body    = @{ currentPassword = $CurrentPwd; newPassword = $NewPwd } | ConvertTo-Json
        $headers = @{ Authorization = "Bearer $token" }
        Invoke-RestMethod "$BaseUrl/api/auth/password" -Method Put `
            -Body $body -ContentType 'application/json' -Headers $headers -ErrorAction Stop | Out-Null
        Write-Ok "cosadm password changed successfully."
    } catch {
        Write-Warn "Password change API call failed: $($_.Exception.Message)"
        Write-Info "Log in at $BaseUrl and change it via Profile → Change Password."
    }
}

# ── Common config collection ──────────────────────────────────────────────────

function Get-CommonConfig {
    Write-Section 'JWT Secret Key'
    Write-Info "The JWT key signs authentication tokens. Keep it private and never reuse it."
    Write-Info "Leave blank to generate one automatically."
    $raw = Read-Host '  JWT key (32+ chars, or Enter to generate)'
    if ($raw.Trim() -eq '') {
        $jwtKey = New-JwtKey
        Write-Ok "Generated: $jwtKey"
    } else {
        while ($raw.Trim().Length -lt 32) {
            Write-Warn "Key must be at least 32 characters."
            $raw = Read-Host '  JWT key'
        }
        $jwtKey = $raw.Trim()
    }

    Write-Section 'Admin Account'
    Write-Info "Default cosadm password: wholeftjaceinchargeofdesign"
    $changeAdminPwd = Read-YesNo 'Change the cosadm password now?'
    $newAdminPwd    = $null
    if ($changeAdminPwd) {
        Write-Info "Password must be at least 15 characters."
        $newAdminPwd = Read-Secret '  New cosadm password' -MinLen 15
        $confirm     = Read-Secret '  Confirm new password' -MinLen 1
        while ($newAdminPwd -ne $confirm) {
            Write-Warn "Passwords do not match."
            $newAdminPwd = Read-Secret '  New cosadm password' -MinLen 15
            $confirm     = Read-Secret '  Confirm new password' -MinLen 1
        }
    }

    return @{
        JwtKey      = $jwtKey
        NewAdminPwd = $newAdminPwd
    }
}

# ── LOCAL install ─────────────────────────────────────────────────────────────

function Install-Local {
    param($Config)

    Write-Section 'Prerequisites'
    Assert-Command 'dotnet' 'Download from https://dotnet.microsoft.com/download'
    Assert-Command 'npm'    'Download from https://nodejs.org/'

    Write-Section 'Ports'
    $apiPort = Read-Port 'API port'     5000
    $webPort = Read-Port 'Frontend port' 5173

    Write-Section 'Applying configuration'

    $settingsPath = Join-Path $PSScriptRoot 'src\CountOrSell.Api\appsettings.json'
    $json = Get-Content $settingsPath -Raw | ConvertFrom-Json

    $json.Jwt.Key = $Config.JwtKey
    $json | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding UTF8
    Write-Ok "appsettings.json updated with new JWT key."

    Write-Section 'Summary'
    Write-Ok "Configuration complete."
    Write-Host ''
    Write-Host "  Start the app:" -ForegroundColor White
    if ($IsWindows -or $env:OS -match 'Windows') {
        Write-Host "    start.bat --api-port $apiPort --web-port $webPort" -ForegroundColor Yellow
    } else {
        Write-Host "    ./start.sh --api-port $apiPort --web-port $webPort" -ForegroundColor Yellow
    }
    Write-Host ''
    Write-Host "  Then open: http://localhost:$webPort" -ForegroundColor White
    Write-Host ''

    if ($Config.NewAdminPwd) {
        $startNow = Read-YesNo 'Start the app now to change the admin password?'
        if ($startNow) {
            Write-Info "Starting API temporarily to change password..."
            $apiProc = Start-Process dotnet -ArgumentList "run --project src/CountOrSell.Api --no-launch-profile --urls http://localhost:$apiPort" `
                           -WorkingDirectory $PSScriptRoot -PassThru -WindowStyle Hidden
            try {
                if (Wait-ForApi "http://localhost:$apiPort") {
                    Set-AdminPassword "http://localhost:$apiPort" 'wholeftjaceinchargeofdesign' $Config.NewAdminPwd
                }
            } finally {
                $apiProc | Stop-Process -Force -ErrorAction SilentlyContinue
            }
        } else {
            Write-Warn "Remember to change the cosadm password after starting the app."
        }
    }
}

# ── DOCKER install ────────────────────────────────────────────────────────────

function Install-Docker {
    param($Config)

    Write-Section 'Prerequisites'
    Assert-Command 'docker' 'Download Docker Desktop from https://www.docker.com/products/docker-desktop/'

    # Check compose plugin
    $hasCompose = $false
    try { docker compose version 2>&1 | Out-Null; $hasCompose = $true } catch { }
    if (-not $hasCompose) {
        Write-Err "docker compose plugin not found. Update Docker Desktop or install the compose plugin."
        exit 1
    }
    Write-Ok "docker compose found"

    Write-Section 'Container port'
    $port = Read-Port 'Host port to expose CountOrSell on' 8080

    Write-Section 'Applying configuration'

    # Write .env file
    $envPath = Join-Path $PSScriptRoot '.env'
    @"
JWT_KEY=$($Config.JwtKey)
COS_PORT=$port
"@ | Set-Content $envPath -Encoding UTF8
    Write-Ok ".env written."

    Write-Section 'Building Docker image'
    Push-Location $PSScriptRoot
    try {
        & docker compose build
        if ($LASTEXITCODE -ne 0) { throw "docker compose build failed." }
        Write-Ok "Image built."
    } finally { Pop-Location }

    $startNow = Read-YesNo 'Start the container now?' $true
    if (-not $startNow) {
        Write-Section 'Done'
        Write-Host "  Start later with:" -ForegroundColor White
        Write-Host "    docker compose up -d" -ForegroundColor Yellow
        return
    }

    Push-Location $PSScriptRoot
    try {
        & docker compose up -d
        if ($LASTEXITCODE -ne 0) { throw "docker compose up failed." }
        Write-Ok "Container started."
    } finally { Pop-Location }

    $baseUrl = "http://localhost:$port"
    if (Wait-ForApi $baseUrl) {
        if ($Config.NewAdminPwd) {
            Set-AdminPassword $baseUrl 'wholeftjaceinchargeofdesign' $Config.NewAdminPwd
        }
    }

    Write-Section 'Done'
    Write-Host ''
    Write-Host "  CountOrSell is running at: $baseUrl" -ForegroundColor Green
    Write-Host ''
    Write-Host "  Useful commands:" -ForegroundColor White
    Write-Host "    docker compose logs -f          # live logs" -ForegroundColor Gray
    Write-Host "    docker compose down             # stop" -ForegroundColor Gray
    Write-Host "    docker compose pull && docker compose up -d   # update" -ForegroundColor Gray
    Write-Host ''
}

# ── AZURE install ─────────────────────────────────────────────────────────────

function Install-Azure {
    param($Config)

    Write-Section 'Prerequisites'
    Assert-Command 'az'     'Install the Azure CLI: https://docs.microsoft.com/cli/azure/install-azure-cli'
    Assert-Command 'docker' 'Install Docker Desktop: https://www.docker.com/products/docker-desktop/'

    # Verify az login
    try {
        $acct = az account show 2>&1 | ConvertFrom-Json
        Write-Ok "Logged in as: $($acct.user.name)"
    } catch {
        Write-Info "Not logged in — running 'az login'..."
        az login | Out-Null
        $acct = az account show 2>&1 | ConvertFrom-Json
        Write-Ok "Logged in as: $($acct.user.name)"
    }

    Write-Section 'Azure Subscription'
    $subs = az account list --output json 2>&1 | ConvertFrom-Json
    Write-Host ''
    $subs | ForEach-Object { Write-Host "  $($_.id)  $($_.name)" }
    Write-Host ''
    $currentId   = $acct.id
    $subscriptionId = Read-Host "  Subscription ID [default: $currentId]"
    if ($subscriptionId.Trim() -eq '') { $subscriptionId = $currentId }
    az account set --subscription $subscriptionId | Out-Null
    Write-Ok "Using subscription: $subscriptionId"

    Write-Section 'Resource Group'
    $location      = Read-NonEmpty '  Azure region (e.g. eastus, westeurope, australiaeast)'
    $resourceGroup = Read-NonEmpty '  Resource group name'
    Write-Info "Creating resource group '$resourceGroup' in '$location'..."
    az group create --name $resourceGroup --location $location --output none
    Write-Ok "Resource group ready."

    Write-Section 'Azure Container Registry'
    Write-Info "An ACR is needed to store the Docker image."
    $acrName = Read-NonEmpty '  ACR name (lowercase letters/numbers only, 5-50 chars)'
    az acr create --name $acrName --resource-group $resourceGroup --sku Basic --output none 2>&1 | Out-Null
    Write-Ok "ACR '$acrName' ready."

    Write-Section 'Azure Container Apps'
    $appName = Read-NonEmpty '  Container App name'
    $envName = "$appName-env"

    Write-Section 'Custom domain (optional)'
    Write-Info "Leave blank to use the default *.azurecontainerapps.io domain."
    $customDomain = Read-Host '  Custom domain (e.g. mtg.example.com)'
    $customDomain = $customDomain.Trim()

    Write-Section 'Persistent Storage'
    Write-Info "CountOrSell stores the SQLite database and card images in /data."
    Write-Info "An Azure Storage account + file share will be created for persistence."
    # Storage account name must be globally unique, lowercase, 3-24 chars
    $storageBase    = ($appName -replace '[^a-z0-9]', '').ToLower()
    $storageAccount = if ($storageBase.Length -le 18) { "${storageBase}cosdata" } else { $storageBase.Substring(0,18) + 'cos' }
    Write-Info "Storage account name: $storageAccount"
    $storageAccount = Read-Host "  Override storage account name [default: $storageAccount]"
    if ($storageAccount.Trim() -eq '') { $storageAccount = "$($appName -replace '[^a-z0-9]','')cosdata" }
    $storageAccount = $storageAccount.ToLower() -replace '[^a-z0-9]', ''

    Write-Section 'Building and pushing Docker image'
    Push-Location $PSScriptRoot
    try {
        $imageTag = "${acrName}.azurecr.io/countorsell:latest"

        Write-Info "Building image..."
        & docker build -t $imageTag .
        if ($LASTEXITCODE -ne 0) { throw "docker build failed." }

        Write-Info "Logging into ACR..."
        az acr login --name $acrName | Out-Null

        Write-Info "Pushing image..."
        & docker push $imageTag
        if ($LASTEXITCODE -ne 0) { throw "docker push failed." }
        Write-Ok "Image pushed: $imageTag"
    } finally { Pop-Location }

    Write-Section 'Creating storage'
    az storage account create `
        --name $storageAccount `
        --resource-group $resourceGroup `
        --location $location `
        --sku Standard_LRS `
        --kind StorageV2 `
        --output none
    Write-Ok "Storage account created."

    $storageKey = az storage account keys list `
        --account-name $storageAccount `
        --resource-group $resourceGroup `
        --query '[0].value' --output tsv

    az storage share create `
        --name 'cosdata' `
        --account-name $storageAccount `
        --account-key $storageKey `
        --output none
    Write-Ok "File share 'cosdata' created."

    Write-Section 'Creating Container Apps environment'
    az containerapp env create `
        --name $envName `
        --resource-group $resourceGroup `
        --location $location `
        --output none
    Write-Ok "Environment '$envName' created."

    # Attach the storage share to the environment
    az containerapp env storage set `
        --name $envName `
        --resource-group $resourceGroup `
        --storage-name cosdata `
        --azure-file-account-name $storageAccount `
        --azure-file-account-key $storageKey `
        --azure-file-share-name cosdata `
        --access-mode ReadWrite `
        --output none
    Write-Ok "Storage attached to environment."

    Write-Section 'Deploying Container App'

    # Enable ACR admin credentials so Container Apps can pull the image
    az acr update --name $acrName --admin-enabled true --output none
    $acrCreds    = az acr credential show --name $acrName --output json 2>&1 | ConvertFrom-Json
    $acrUsername = $acrCreds.username
    $acrPassword = $acrCreds.passwords[0].value

    az containerapp create `
        --name $appName `
        --resource-group $resourceGroup `
        --environment $envName `
        --image "${acrName}.azurecr.io/countorsell:latest" `
        --registry-server "${acrName}.azurecr.io" `
        --registry-username $acrUsername `
        --registry-password $acrPassword `
        --target-port 8080 `
        --ingress external `
        --min-replicas 1 `
        --max-replicas 1 `
        --cpu 1 --memory 2Gi `
        --secrets "jwt-key=$($Config.JwtKey)" `
        --env-vars `
            "ASPNETCORE_ENVIRONMENT=Production" `
            "Jwt__Issuer=CountOrSell" `
            "Jwt__Audience=CountOrSellUsers" `
            "Jwt__Key=secretref:jwt-key" `
            "COS_DATABASE_PATH=/data/database/CountOrSell.db" `
            "COS_IMAGES_PATH=/data/images" `
        --output none
    Write-Ok "Container App '$appName' created."

    # Mount the Azure Files share at /data
    az containerapp update `
        --name $appName `
        --resource-group $resourceGroup `
        --volume name=cosdata,storageType=AzureFile,storageName=cosdata `
        --mount name=cosdata,mountPath=/data `
        --output none 2>&1 | Out-Null
    Write-Ok "Persistent storage mounted at /data."

    # Get the FQDN
    $fqdn = az containerapp show `
        --name $appName `
        --resource-group $resourceGroup `
        --query 'properties.configuration.ingress.fqdn' --output tsv
    $appUrl = "https://$fqdn"

    # Optionally bind a custom domain
    if ($customDomain -ne '') {
        Write-Section "Custom domain: $customDomain"
        Write-Info "Add a CNAME record pointing '$customDomain' → '$fqdn'"
        Write-Info "Then press Enter to continue with domain binding..."
        Read-Host '  Press Enter when DNS is configured'

        az containerapp hostname add `
            --hostname $customDomain `
            --name $appName `
            --resource-group $resourceGroup `
            --output none
        az containerapp hostname bind `
            --hostname $customDomain `
            --name $appName `
            --resource-group $resourceGroup `
            --validation-method CNAME `
            --output none
        Write-Ok "Custom domain bound and SSL certificate issued."
        $appUrl = "https://$customDomain"
    }

    # Change admin password
    if ($Config.NewAdminPwd) {
        Write-Section 'Setting admin password'
        if (Wait-ForApi $appUrl 120) {
            Set-AdminPassword $appUrl 'wholeftjaceinchargeofdesign' $Config.NewAdminPwd
        }
    }

    Write-Section 'Azure deployment complete'
    Write-Host ''
    Write-Host "  CountOrSell is live at: $appUrl" -ForegroundColor Green
    Write-Host ''
    Write-Host "  Useful Azure CLI commands:" -ForegroundColor White
    Write-Host "    az containerapp logs show -n $appName -g $resourceGroup --follow" -ForegroundColor Gray
    Write-Host "    az containerapp update -n $appName -g $resourceGroup --image <new-image-tag>" -ForegroundColor Gray
    Write-Host "    az containerapp delete -n $appName -g $resourceGroup" -ForegroundColor Gray
    Write-Host ''
}

# ── Main ──────────────────────────────────────────────────────────────────────

Write-Banner

Write-Section 'Installation mode'
if ($Mode -eq '') {
    Write-Host "  [1] Local    — configure and run with start.sh / start.bat" -ForegroundColor White
    Write-Host "  [2] Docker   — build a Docker image, run with docker compose" -ForegroundColor White
    Write-Host "  [3] Azure    — deploy to Azure Container Apps" -ForegroundColor White
    Write-Host ''
    $choice = Read-Host '  Choose [1/2/3]'
    $Mode   = switch ($choice) {
        '1' { 'local' }
        '2' { 'docker' }
        '3' { 'azure' }
        default { Write-Err "Invalid choice."; exit 1 }
    }
}

$config = Get-CommonConfig

switch ($Mode) {
    'local'  { Install-Local  $config }
    'docker' { Install-Docker $config }
    'azure'  { Install-Azure  $config }
}
