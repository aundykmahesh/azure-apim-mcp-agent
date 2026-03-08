# azure-apim-mcp-agent

A reference implementation for exposing Azure API Management as an MCP (Model Context Protocol) server, enabling AI agents like Claude and M365 Copilot to discover, query, and interact with your APIs using natural language — essentially a **Copilot for your API Catalogue**.

## Architecture

```
Claude / Copilot / VS Code (MCP Client)
        |
Azure API Management (MCP Server feature)
  - Auth: subscription key + Entra ID
  - Maps REST operations -> MCP tools
        |
Azure Container Apps (Functions, scale-to-zero)
  - RESTful API endpoints
        |
Azure ARM APIs (managed identity)
  - Reads API metadata from APIM instances
```

APIM's built-in MCP Server feature handles the MCP protocol. The Functions app is a plain REST API.

## REST API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/instances` | List all configured APIM instances |
| GET | `/instances/{name}/apis` | List all APIs in an instance |
| GET | `/instances/{name}/apis/search?keyword={kw}` | Search APIs by keyword |
| GET | `/instances/{name}/apis/{apiId}` | Get API metadata |
| GET | `/instances/{name}/apis/{apiId}/spec` | Download OpenAPI spec |
| GET | `/instances/{name}/apis/{apiId}/operations` | List all operations (endpoints) in an API |
| GET | `/instances/{name}/catalog` | Full catalog: all APIs with their operations |
| POST | `/chat` | Natural language API query (Azure OpenAI) |
| GET | `/health` | Health check |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Docker](https://docs.docker.com/get-docker/)
- An Azure subscription with at least one API Management instance
- Azure CLI logged in (`az login`)
- (Optional) Azure OpenAI resource for the `/chat` endpoint

## Local Development

1. Clone the repo and restore packages:

```bash
dotnet restore
```

2. Configure your APIM instances in `src/AzureApimMcp.Functions/local.settings.json`:

```json
{
  "Values": {
    "Apim__Instances__0__Name": "dev",
    "Apim__Instances__0__SubscriptionId": "<your-subscription-id>",
    "Apim__Instances__0__ResourceGroup": "<your-resource-group>",
    "Apim__Instances__0__ServiceName": "<your-apim-name>",
    "AzureOpenAI__Endpoint": "<your-azure-openai-endpoint>",
    "AzureOpenAI__DeploymentName": "<your-deployment-name>"
  }
}
```

The Azure OpenAI settings are optional. If omitted, the `/chat` endpoint will be unavailable but all other endpoints will work.

3. Run locally:

```bash
cd src/AzureApimMcp.Functions
func start
```

4. Test the endpoints:

```bash
curl http://localhost:7071/health
curl http://localhost:7071/instances
curl http://localhost:7071/instances/dev/apis
curl "http://localhost:7071/instances/dev/apis/search?keyword=payment"
curl http://localhost:7071/instances/dev/apis/my-api/operations
curl http://localhost:7071/instances/dev/catalog
curl -X POST http://localhost:7071/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "which API gives me a customer transaction history?", "sessionId": "test1"}'
```

## Deployment

### Quick deploy (recommended)

Use the included deployment script which builds, pushes, and deploys everything:

```powershell
./deploy.ps1
```

Options:
- `./deploy.ps1 -SkipBuild` — Skip Docker build/push, deploy infra and restart only
- `./deploy.ps1 -SkipInfra` — Skip Bicep deployment, build and restart only

### Manual deployment

#### Build and push the container image

```bash
az acr login --name <acr-name>
docker build -f src/AzureApimMcp.Functions/Dockerfile -t <acr-name>.azurecr.io/apim-mcp:latest .
docker push <acr-name>.azurecr.io/apim-mcp:latest
```

#### Deploy infrastructure with Bicep

```bash
az deployment group create \
  --resource-group <resource-group> \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam
```

#### Update the Container App

```bash
az containerapp update \
  --resource-group <resource-group> \
  --name apim-mcp-app \
  --image <acr-name>.azurecr.io/apim-mcp:latest
```

### Configure APIM MCP Server

After deployment, configure APIM to expose the API as an MCP server:

1. Navigate to your APIM instance in Azure Portal
2. Go to **MCP Servers** under the APIs section
3. Add the deployed API as an MCP server
4. Configure the MCP tools mapping from the REST operations

## Adding Multiple APIM Instances

Add more instances via environment variables or `local.settings.json`:

```json
{
  "Apim__Instances__0__Name": "production",
  "Apim__Instances__0__SubscriptionId": "...",
  "Apim__Instances__0__ResourceGroup": "...",
  "Apim__Instances__0__ServiceName": "...",
  "Apim__Instances__1__Name": "staging",
  "Apim__Instances__1__SubscriptionId": "...",
  "Apim__Instances__1__ResourceGroup": "...",
  "Apim__Instances__1__ServiceName": "..."
}
```

## Project Structure

```
src/AzureApimMcp.Functions/
  Configuration/     - Strongly-typed config models
  Services/          - IApimService + ApimService (ARM SDK)
  Functions/         - HTTP trigger functions (REST + Chat endpoints)
  Program.cs         - Host builder + DI registration
  openapi.json       - OpenAPI specification

infra/
  main.bicep         - Orchestrator
  main.bicepparam    - Parameter values
  modules/           - ACR, ACA, identity, RBAC, APIM

deploy.ps1           - Build, push, and deploy script
```
