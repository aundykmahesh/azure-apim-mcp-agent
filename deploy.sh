#!/usr/bin/env bash
# Builds, pushes, and deploys the APIM MCP Agent to Azure.
#
# Usage:
#   ./deploy.sh                  # Full deploy (build + infra + restart)
#   ./deploy.sh --skip-build     # Infra + restart only
#   ./deploy.sh --skip-infra     # Build + restart only
#   ./deploy.sh --resource-group my-rg

set -euo pipefail

# --- Config (mirrors deploy.ps1) ---
RESOURCE_GROUP='aundydev'
ENVIRONMENT_NAME='apim-mcp'
ACR_NAME='aundyfirstregistry'
ACR_LOGIN_SERVER="${ACR_NAME}.azurecr.io"
IMAGE_NAME="${ACR_LOGIN_SERVER}/${ENVIRONMENT_NAME}:latest"
CONTAINER_APP_NAME="${ENVIRONMENT_NAME}-app"
DOCKERFILE_PATH='src/AzureApimMcp.Functions/Dockerfile'
APIM_SERVICE_NAME='aundy-ingress'
SKIP_BUILD=false
SKIP_INFRA=false

# --- Arg parsing ---
while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-build)   SKIP_BUILD=true ;;
        --skip-infra)   SKIP_INFRA=true ;;
        --resource-group) RESOURCE_GROUP="$2"; shift ;;
        *) echo "Unknown argument: $1"; exit 1 ;;
    esac
    shift
done

echo '========================================'
echo ' APIM MCP Agent - Deploy to Azure'
echo '========================================'
echo ''

# Step 1 & 2: Build and push
if [ "$SKIP_BUILD" = false ]; then
    echo '[1/4] Building Docker image...'
    docker build -t "$IMAGE_NAME" -f "$DOCKERFILE_PATH" .
    echo '      Build complete.'
    echo ''

    echo '[2/4] Logging into ACR and pushing image...'
    az acr login --name "$ACR_NAME"
    docker push "$IMAGE_NAME"
    echo '      Push complete.'
    echo ''
else
    echo '[1/4] Skipping Docker build (--skip-build)'
    echo '[2/4] Skipping Docker push (--skip-build)'
    echo ''
fi

# Step 3: Deploy infrastructure
if [ "$SKIP_INFRA" = false ]; then
    echo '[3/4] Deploying Bicep infrastructure...'
    az deployment group create \
        --resource-group "$RESOURCE_GROUP" \
        --template-file infra/main.bicep \
        --parameters infra/main.bicepparam \
        --mode Incremental
    echo '      Infrastructure deployed.'
    echo ''
else
    echo '[3/4] Skipping Bicep deployment (--skip-infra)'
    echo ''
fi

# Step 4: Force new revision
echo '[4/4] Updating Container App to pull latest image...'
az containerapp update \
    --resource-group "$RESOURCE_GROUP" \
    --name "$CONTAINER_APP_NAME" \
    --image "$IMAGE_NAME"
echo '      Container App updated.'
echo ''

echo '========================================'
echo ' Deployment complete!'
echo '========================================'
echo ''

fqdn=$(az containerapp show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$CONTAINER_APP_NAME" \
    --query 'properties.configuration.ingress.fqdn' \
    -o tsv)

echo "Functions App:  https://${fqdn}"
echo "APIM endpoint:  https://${APIM_SERVICE_NAME}.azure-api.net/mcp-agent"
