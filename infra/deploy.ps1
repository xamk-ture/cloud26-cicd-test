# =============================================================
# Assignment 4 — Model solution: deploy wrapper (Exercise 3)
#
# Encodes the deployment discipline:
#   1. what-if first, always
#   2. a human reads the diff and confirms
#   3. only then deploy; print the outputs
#
# The PostgreSQL password is NOT written here and NOT printed.
# dev.bicepparam reads POSTGRES_ADMIN_PASSWORD. Current Bicep
# errors with BCP258 if that parameter is missing from the file,
# and the CLI does not prompt for it.
#
# Usage:  .\deploy.ps1            (uses dev.bicepparam)
#         .\deploy.ps1 -ParamFile test.bicepparam
# =============================================================

param(
    [string]$ParamFile = "dev.bicepparam"
)

# ----- Variables: EDIT THIS ----------------------------------
$YourName      = "ture05"                    # same as in the param file
$ResourceGroup = "rg-cloud26-$YourName-dev"

$TemplateFile  = Join-Path $PSScriptRoot "main.bicep"
$ParamPath     = Join-Path $PSScriptRoot $ParamFile

# ----- Preconditions ------------------------------------------
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Not signed in. Run 'az login' first." -ForegroundColor Red
    exit 1
}
Write-Host "Signed in as: $($account.user.name)" -ForegroundColor Green
Write-Host "Target resource group: $ResourceGroup" -ForegroundColor Green
Write-Host "Parameters: $ParamFile" -ForegroundColor Green
Write-Host ""

# ----- Password: environment only, never a file or the host -----
if (-not $env:POSTGRES_ADMIN_PASSWORD) {
    $env:POSTGRES_ADMIN_PASSWORD = Read-Host "PostgreSQL admin password"
}

# ----- 1. What-if: preview the changes ------------------------
Write-Host "=== What-if (no changes are made yet) ===" -ForegroundColor Cyan
az deployment group what-if `
    --resource-group $ResourceGroup `
    --template-file $TemplateFile `
    --parameters $ParamPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "What-if failed — fix the template before deploying." -ForegroundColor Red
    exit 1
}

# ----- 2. Human confirmation ----------------------------------
Write-Host ""
$answer = Read-Host "Read the diff above. Deploy? (y/n)"
if ($answer -ne "y") {
    Write-Host "Aborted — nothing was changed." -ForegroundColor Yellow
    exit 0
}

# ----- 3. Deploy and show outputs -----------------------------
Write-Host "Deploying..." -ForegroundColor Cyan
$deploymentJson = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $TemplateFile `
    --parameters $ParamPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "Deployment failed — see the error above." -ForegroundColor Red
    exit 1
}

$deployment = $deploymentJson | ConvertFrom-Json

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Green
Write-Host "Web app:  $($deployment.properties.outputs.webAppUrl.value)"
Write-Host "Postgres: $($deployment.properties.outputs.postgresServerFqdn.value)"
Write-Host ""
Write-Host "Verify: curl $($deployment.properties.outputs.webAppUrl.value)/health" -ForegroundColor Yellow
