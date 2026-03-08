<#
.SYNOPSIS
    Builds, pushes, and deploys the APIM MCP Agent to Azure.

.DESCRIPTION
    1. Builds the Docker image
    2. Pushes to Azure Container Registry
    3. Deploys infrastructure via Bicep
    4. Forces a new Container App revision to pick up the latest image

.PARAMETER ResourceGroup
    Azure resource group name. Default: aundydev

.PARAMETER SkipBuild
    Skip Docker build and push steps (deploy infra only).

.PARAMETER SkipInfra
    Skip Bicep deployment (build and restart only).
#>
param(
    [string]$ResourceGroup = 'aundydev',
    [switch]$SkipBuild,
    [switch]$SkipInfra
)

$ErrorActionPreference = 'Stop'

# Config from bicepparam
$EnvironmentName = 'apim-mcp'
$AcrName = 'aundyfirstregistry'
$AcrLoginServer = "$AcrName.azurecr.io"
$ImageName = "$AcrLoginServer/${EnvironmentName}:latest"
$ContainerAppName = "$EnvironmentName-app"
$DockerfilePath = 'src/AzureApimMcp.Functions/Dockerfile'

Write-Host '========================================' -ForegroundColor Cyan
Write-Host ' APIM MCP Agent - Deploy to Azure' -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan
Write-Host ''

# Step 1: Build Docker image
if (-not $SkipBuild) {
    Write-Host '[1/4] Building Docker image...' -ForegroundColor Yellow
    docker build -t $ImageName -f $DockerfilePath .
    if ($LASTEXITCODE -ne 0) { throw 'Docker build failed.' }
    Write-Host '      Build complete.' -ForegroundColor Green
    Write-Host ''

    # Step 2: Push to ACR
    Write-Host '[2/4] Logging into ACR and pushing image...' -ForegroundColor Yellow
    az acr login --name $AcrName
    if ($LASTEXITCODE -ne 0) { throw 'ACR login failed.' }

    docker push $ImageName
    if ($LASTEXITCODE -ne 0) { throw 'Docker push failed.' }
    Write-Host '      Push complete.' -ForegroundColor Green
    Write-Host ''
}
else {
    Write-Host '[1/4] Skipping Docker build (--SkipBuild)' -ForegroundColor DarkGray
    Write-Host '[2/4] Skipping Docker push (--SkipBuild)' -ForegroundColor DarkGray
    Write-Host ''
}

# Step 3: Deploy infrastructure
if (-not $SkipInfra) {
    Write-Host '[3/4] Deploying Bicep infrastructure...' -ForegroundColor Yellow
    az deployment group create `
        --resource-group $ResourceGroup `
        --template-file infra/main.bicep `
        --parameters infra/main.bicepparam
    if ($LASTEXITCODE -ne 0) { throw 'Bicep deployment failed.' }
    Write-Host '      Infrastructure deployed.' -ForegroundColor Green
    Write-Host ''
}
else {
    Write-Host '[3/4] Skipping Bicep deployment (--SkipInfra)' -ForegroundColor DarkGray
    Write-Host ''
}

# Step 4: Force new revision to pick up latest image
Write-Host '[4/4] Updating Container App to pull latest image...' -ForegroundColor Yellow
az containerapp update `
    --resource-group $ResourceGroup `
    --name $ContainerAppName `
    --image $ImageName
if ($LASTEXITCODE -ne 0) { throw 'Container App update failed.' }
Write-Host '      Container App updated.' -ForegroundColor Green
Write-Host ''

Write-Host '========================================' -ForegroundColor Cyan
Write-Host ' Deployment complete!' -ForegroundColor Green
Write-Host '========================================' -ForegroundColor Cyan
Write-Host ''

# Show the Container App FQDN
$fqdn = az containerapp show --resource-group $ResourceGroup --name $ContainerAppName --query 'properties.configuration.ingress.fqdn' -o tsv
Write-Host "Container App: https://$fqdn" -ForegroundColor Cyan
Write-Host "APIM endpoint: https://aundy-apim.azure-api.net/mcp-agent" -ForegroundColor Cyan
