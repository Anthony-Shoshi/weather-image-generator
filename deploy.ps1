# deploy.ps1
param(
    [string]$ResourceGroupName = "AssignmentWeatherAppRG",
    [string]$Location = "Spain Central",
    [string]$BicepFile = ".\bicep\main.bicep",
    [string]$FunctionProjectPath = ".\src"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Deploying Azure infrastructure using Bicep..." -ForegroundColor Cyan

# Create resource group
az group create --name $ResourceGroupName --location $Location | Out-Null

# Deploy Bicep template
$deploymentOutput = az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file $BicepFile `
    --output json

# Save outputs to JSON file
$deploymentOutput | Out-File -FilePath "deployment_outputs.json" -Encoding utf8

# Extract values from outputs
$deploymentJson = Get-Content -Raw -Path "deployment_outputs.json" | ConvertFrom-Json
$FunctionAppName = $deploymentJson.properties.outputs.functionAppName.value
$StorageAccountName = $deploymentJson.properties.outputs.storageAccountName.value

Write-Host "Function App: $FunctionAppName" -ForegroundColor Green
Write-Host "Storage Account: $StorageAccountName" -ForegroundColor Green

# === FUNCTION DEPLOYMENT ===
Write-Host "Publishing Azure Function from: $FunctionProjectPath" -ForegroundColor Cyan

if (-not (Test-Path "$FunctionProjectPath\host.json")) {
    Write-Error "Error: host.json not found in $FunctionProjectPath"
    exit 1
}

Set-Location $FunctionProjectPath

Write-Host "Building function..." -ForegroundColor Yellow
dotnet publish -c Release -o ./publish

Write-Host "Deploying to Azure Function App: $FunctionAppName" -ForegroundColor Cyan
func azure functionapp publish $FunctionAppName --dotnet-isolated --force

Write-Host "Deployment complete!" -ForegroundColor Green
