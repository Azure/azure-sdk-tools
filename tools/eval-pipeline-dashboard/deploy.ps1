# Deploys the vally eval dashboard to Azure App Service (Linux, Node).
#
# Prereqs:
#   - Azure CLI logged in:  az login
#   - eval.db already built (see README). It ships with the app.
#
# Usage:
#   ./deploy.ps1                       # uses defaults below
#   ./deploy.ps1 -AppName my-dash -ResourceGroup my-rg -Location eastus -Sku B1
#
# Notes:
#   - node_modules is NOT uploaded; Oryx runs `npm install` on Linux so the
#     native better-sqlite3 binary matches the host.
#   - Azure injects PORT; server.js binds 0.0.0.0:$PORT automatically.

param(
  [string]$AppName       = "vally-eval-dashboard-$((Get-Random -Maximum 99999))",
  [string]$ResourceGroup = "rg-haoling",
  [string]$Location      = "eastus",
  [string]$Sku           = "B1",
  [string]$Runtime       = "NODE:22-lts",
  # Optional: target a specific subscription (name or id) where you have rights.
  [string]$Subscription  = ""
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Test-Path "./eval.db")) {
  throw "eval.db not found. Build it first:  vally ingest <results-dir> --store eval.db"
}

if ($Subscription) {
  Write-Host "Selecting subscription '$Subscription'..." -ForegroundColor Cyan
  az account set --subscription $Subscription
}

# Reuse the resource group if it already exists (you may lack rights to create
# groups on shared subscriptions — in that case pass an RG you can write to).
$rgExists = az group exists --name $ResourceGroup | ConvertFrom-Json
if (-not $rgExists) {
  Write-Host "Creating resource group '$ResourceGroup' in '$Location'..." -ForegroundColor Cyan
  az group create --name $ResourceGroup --location $Location | Out-Null
} else {
  Write-Host "Using existing resource group '$ResourceGroup'." -ForegroundColor Cyan
}

# Ensure Oryx builds dependencies during deployment (compiles/fetches native module).
Write-Host "Deploying '$AppName' (this creates the plan + web app and uploads the code)..." -ForegroundColor Cyan
az webapp up `
  --name $AppName `
  --resource-group $ResourceGroup `
  --location $Location `
  --runtime $Runtime `
  --sku $Sku

# App settings:
#  - SCM_DO_BUILD_DURING_DEPLOYMENT: Oryx runs `npm install` (rebuilds native module)
#  - WEBSITES_CONTAINER_START_TIME_LIMIT: allow time for cold start + initial ingest
#  - VALLY_RESULTS / VALLY_DB: point at /home persistent storage so data and the
#    SQLite cache survive restarts/deploys, and new run folders uploaded to
#    /home/site/results are ingested live.
az webapp config appsettings set `
  --name $AppName `
  --resource-group $ResourceGroup `
  --settings `
    SCM_DO_BUILD_DURING_DEPLOYMENT=true `
    WEBSITES_CONTAINER_START_TIME_LIMIT=600 `
    VALLY_RESULTS=/home/site/results `
    VALLY_DB=/home/data/eval.db | Out-Null

# Explicit startup command is intentionally NOT set. Oryx compresses
# node_modules and wires up the runtime via its generated startup; our
# package.json `start` script runs `node start.js`, which extracts the
# compressed node_modules in place so ESM imports resolve (see start.js).

# Enable SCM (Kudu) basic publishing credentials. add-result.ps1 uploads new
# run folders into /home/site/results via the Kudu zip API, which needs these.
Write-Host "Enabling SCM basic publishing credentials (needed to upload results)..." -ForegroundColor Cyan
az resource update `
  --resource-group $ResourceGroup `
  --namespace Microsoft.Web `
  --resource-type basicPublishingCredentialsPolicies `
  --name scm `
  --parent "sites/$AppName" `
  --set properties.allow=true | Out-Null

$hostName = az webapp show --name $AppName --resource-group $ResourceGroup --query defaultHostName -o tsv
Write-Host ""
Write-Host "Done. Dashboard URL: https://$hostName/" -ForegroundColor Green
Write-Host ""
Write-Host "Next: upload run folders to /home/site/results to populate/update the dashboard, e.g.:" -ForegroundColor Yellow
Write-Host "  Compress-Archive -Path .\results\* -DestinationPath results.zip -Force" -ForegroundColor Yellow
Write-Host "  az webapp deploy --name $AppName --resource-group $ResourceGroup ``" -ForegroundColor Yellow
Write-Host "    --src-path results.zip --type zip --target-path /home/site/results" -ForegroundColor Yellow
Write-Host ""
Write-Host "Stream logs with:    az webapp log tail --name $AppName --resource-group $ResourceGroup" -ForegroundColor DarkGray
