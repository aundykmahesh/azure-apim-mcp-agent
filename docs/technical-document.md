# Azure APIM MCP Agent - Technical Document

## 1. Overview

The **Azure APIM MCP Agent** is a reference implementation that exposes Azure API Management (APIM) metadata and OpenAPI specifications to AI agents via the **Model Context Protocol (MCP)**. It enables AI-powered tools such as Claude and Microsoft 365 Copilot to discover, query, and understand enterprise APIs using natural language.

The solution consists of an Azure Functions app deployed on Azure Container Apps, fronted by Azure API Management which acts as the MCP Server. APIM's built-in MCP Server feature handles the protocol translation — the Functions app itself is a plain REST API.

---

## 2. Architecture

### 2.1 High-Level Flow

```
AI Agent (Claude / Copilot / VS Code)
    |   MCP Protocol
    v
Azure API Management (MCP Server)
    |   REST over HTTPS
    v
Azure Container Apps (Azure Functions)
    |   Azure SDK (Managed Identity)
    v
Azure Resource Manager (ARM) APIs
    |
    v
Target APIM Instance(s) - API Metadata & OpenAPI Specs
```

### 2.2 Component Responsibilities

| Component | Role |
|---|---|
| **AI Agent** (MCP Client) | Sends natural language requests; invokes MCP tools |
| **Azure API Management** | MCP Server protocol handler; auth gateway (subscription key + Entra ID); routes REST calls to backend |
| **Azure Container Apps** | Hosts the .NET 10 Azure Functions app; scale-to-zero; consumption workload |
| **Azure Functions** | REST API endpoints for API discovery + AI chat agent with function calling |
| **Azure Resource Manager** | Provides API metadata, descriptions, and OpenAPI export links from target APIM instances |
| **Azure OpenAI** (optional) | Powers the `/chat` endpoint with GPT-4o-mini and function calling |

### 2.3 Key Design Decisions

- **APIM as MCP Server**: Leverages APIM's native MCP Server feature rather than implementing MCP protocol in code. APIM maps REST operations to MCP tools automatically.
- **REST-only backend**: The Functions app exposes standard REST endpoints, making it testable and reusable outside of MCP.
- **Managed Identity everywhere**: No secrets stored anywhere. `DefaultAzureCredential` used for all Azure SDK calls.
- **Scale-to-zero**: Container Apps scale down to 0 replicas when idle, keeping costs minimal.

---

## 3. API Endpoints

### 3.1 REST Operations

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Health check, returns `{"status":"ok"}` |
| `GET` | `/instances` | Lists all configured APIM instances |
| `GET` | `/instances/{instanceName}/apis` | Lists all APIs in an instance |
| `GET` | `/instances/{instanceName}/apis/search?keyword={kw}` | Searches APIs by keyword across name, description, and path |
| `GET` | `/instances/{instanceName}/apis/{apiId}` | Gets detailed metadata for a specific API |
| `GET` | `/instances/{instanceName}/apis/{apiId}/spec` | Downloads the full OpenAPI specification |
| `GET` | `/instances/{instanceName}/apis/{apiId}/operations` | Lists all HTTP operations (endpoints) in an API |
| `GET` | `/instances/{instanceName}/catalog` | Full catalog: all APIs with their operations in one call |
| `POST` | `/chat` | Natural language API query agent (Azure OpenAI) |

### 3.2 MCP Tools (Exposed via APIM MCP Server)

When APIM's MCP Server feature is enabled, the REST endpoints are automatically mapped to these MCP tools:

| Tool Name | Maps To | Description |
|---|---|---|
| `list_apim_instances` | `GET /instances` | List available APIM instances |
| `list_apis` | `GET /instances/{instanceName}/apis` | List all APIs in an instance |
| `search_apis` | `GET /instances/{...}/apis/search` | Search APIs by keyword |
| `get_api_details` | `GET /instances/{...}/apis/{apiId}` | Get API metadata (description, URL, protocols) |
| `download_api_spec` | `GET /instances/{...}/apis/{apiId}/spec` | Download full OpenAPI spec |
| `list_api_operations` | `GET /instances/{...}/apis/{apiId}/operations` | List all HTTP endpoints in an API |
| `get_api_catalog` | `GET /instances/{...}/catalog` | Full catalog with all APIs and operations |

### 3.3 Natural Language API Query (Chat Agent)

The `/chat` endpoint is a **Copilot for the API Catalogue** — powered by Azure OpenAI (GPT-4o-mini) with function calling. It uses all 7 tools above to answer capability-based questions about your API landscape.

**Discovery Strategy** (built into the agent's system prompt):
1. For broad questions → calls `get_api_catalog` for a full overview
2. For keyword-driven questions → uses `search_apis` as a shortcut
3. For capability questions → surveys the catalog, then drills into operations to confirm the exact endpoint
4. Always verifies at the operation level rather than guessing from API names

**Example queries:**
- *"Which API gives me a customer's transaction history?"*
- *"What payment-related endpoints do we have?"*
- *"Show me all the operations in the Banking API"*
- *"List all APIs in the dev instance"*

Session-based conversation history is maintained via `sessionId` in the request body.

---

## 4. Security Model

### 4.1 Authentication & Authorization

| Layer | Mechanism |
|---|---|
| **APIM Gateway** | Subscription key + Entra ID authentication |
| **Functions App** | `AuthorizationLevel.Anonymous` (auth delegated to APIM) |
| **Azure SDK calls** | User-assigned managed identity + RBAC |
| **Azure OpenAI** | Managed identity with Cognitive Services OpenAI User role |
| **Container Registry** | Managed identity with AcrPull role |

### 4.2 RBAC Roles

| Role | Scope | Purpose |
|---|---|---|
| API Management Service Reader | Target APIM instance | Read API metadata and export specs |
| Cognitive Services OpenAI User | Azure OpenAI account | Chat completions with function calling |
| AcrPull | Azure Container Registry | Pull container images |

### 4.3 No Stored Secrets

- No connection strings, API keys, or passwords in configuration
- `DefaultAzureCredential` handles all authentication
- Managed identity is the sole credential mechanism

---

## 5. Infrastructure (Bicep IaC)

### 5.1 Resource Topology

```
Resource Group
  |
  +-- User-Assigned Managed Identity
  |
  +-- Azure Container Registry (Basic SKU)
  |     +-- AcrPull role -> Managed Identity
  |
  +-- Log Analytics Workspace (30-day retention)
  |
  +-- Container App Environment (Consumption workload)
  |     +-- Container App: apim-mcp-app
  |           - Image: {acr}/apim-mcp:latest
  |           - CPU: 0.5 cores, Memory: 1 Gi
  |           - Scale: 0-3 replicas (HTTP trigger, 50 concurrent)
  |           - Ingress: External HTTPS (port 80)
  |
  +-- APIM Backend: apim-mcp-backend -> Container App FQDN
  |
  +-- APIM API: apim-mcp-agent (path: /mcp-agent)
  |     +-- 9 operations (listInstances, listApis, searchApis, getApiDetails,
  |     |       downloadApiSpec, listApiOperations, getApiCatalog, healthCheck, chat)
  |     +-- Policy: set-backend-service -> apim-mcp-backend
  |
  +-- Role Assignments
        +-- API Management Service Reader -> target APIM
        +-- Cognitive Services OpenAI User -> OpenAI account (optional)
```

### 5.2 Bicep Module Structure

| Module | File | Purpose |
|---|---|---|
| Managed Identity | `managed-identity.bicep` | User-assigned identity |
| Container Registry | `container-registry.bicep` | ACR + AcrPull role |
| Container App Environment | `container-app-environment.bicep` | Log Analytics + ACA environment |
| Container App | `container-app.bicep` | Functions container with env vars and scaling |
| API Management | `api-management.bicep` | Backend, API, and 9 operations |
| Role Assignments | `role-assignments.bicep` | RBAC for APIM reader + OpenAI user |
| Orchestrator | `main.bicep` | Wires all modules together |

### 5.3 Parameters

| Parameter | Description | Example |
|---|---|---|
| `environmentName` | Resource naming prefix | `apim-mcp` |
| `location` | Azure region | `australiaeast` |
| `containerImageTag` | Docker image tag | `latest` |
| `apimServiceName` | Existing APIM service | `aundy-apim` |
| `targetApimInstanceName` | Logical name for target | `dev` |
| `targetApimSubscriptionId` | Subscription of target APIM | `a181ee41-...` |
| `targetApimResourceGroup` | RG of target APIM | `aundydev` |
| `targetApimServiceName` | Target APIM service name | `aundy-apim` |
| `azureOpenAIEndpoint` | OpenAI endpoint URL | `https://...cognitiveservices.azure.com/` |
| `azureOpenAIDeploymentName` | Model deployment name | `gpt-4o-mini` |
| `azureOpenAICognitiveAccountName` | OpenAI account name | `aundy-mm93xpod-westus` |

---

## 6. Application Code

### 6.1 Technology Stack

| Component | Technology |
|---|---|
| Runtime | .NET 10 (dotnet-isolated) |
| Hosting | Azure Functions v4 on Container Apps |
| Container | Docker multi-stage build |
| AI Framework | Microsoft.Extensions.AI + Azure.AI.OpenAI |
| Azure SDK | Azure.ResourceManager.ApiManagement + Azure.Identity |

### 6.2 Project Structure

```
src/AzureApimMcp.Functions/
  Configuration/
    ApimSettings.cs              # Root config: list of APIM instances
    ApimInstanceConfig.cs        # Per-instance: Name, SubId, RG, ServiceName
  Services/
    IApimService.cs              # Interface + data records (ApiSummary, ApiDetails)
    ApimService.cs               # ARM API implementation (ArmClient)
  Functions/
    InstanceFunctions.cs         # GET /instances
    ApiFunctions.cs              # GET /instances/{name}/apis/...
    ChatFunction.cs              # POST /chat (Azure OpenAI + function calling)
    HealthFunction.cs            # GET /health
  Program.cs                     # Host builder, DI registration
  openapi.json                   # OpenAPI spec for this API
  Dockerfile                     # Multi-stage build
```

### 6.3 Key Implementation Details

**API Search Algorithm** (`ApimService.SearchApisAsync`):
- Filters common stop words ("api", "apis", "rest", "the", etc.)
- Case-insensitive substring matching across DisplayName, Description, and Path
- Returns all matches from the target APIM instance

**OpenAPI Spec Download** (`ApimService.DownloadApiSpecAsync`):
- Calls ARM API export endpoint with `?export=true&format=openapi-link`
- Handles both response formats: `{"value": "url"}` and `{"value": {"link": "url"}}`
- Downloads the actual spec from the returned URL using managed identity credentials

**Operation Discovery** (`ApimService.ListApiOperationsAsync`):
- Finds an API by exact name or display name match (same pattern as `GetApiDetailsAsync`)
- Enumerates operations via ARM SDK: `api.GetApiOperations().GetAllAsync()`
- Returns `ApiOperationSummary` with method, URL template, display name, and description

**API Catalog** (`ApimService.GetApiCatalogAsync`):
- Iterates all APIs, and for each, fetches its operations (N+1 ARM calls)
- Returns a full catalog of `ApiCatalogEntry` records with nested operations
- Designed for broad discovery — amortises multiple tool calls into a single request

**Natural Language API Query — Chat Agent** (`ChatFunction`):
- Creates 7 `AIFunction` tools from `IApimService` methods via `AIFunctionFactory.Create`
- Uses `IChatClient` (Azure OpenAI) with `ChatOptions.Tools` for function calling
- Maintains per-session conversation history in `ConcurrentDictionary`
- **Enhanced system prompt** positions the agent as a "Copilot for the API Catalogue":
  - Teaches a discovery strategy: catalog-first for broad questions, keyword search for targeted queries, operation drill-down for confirmation
  - Instructs the agent to always verify at the operation level, not guess from API names
  - Produces concise responses with API name, HTTP method, path, and description

### 6.4 Dependencies

| Package | Version | Purpose |
|---|---|---|
| Azure.AI.OpenAI | 2.1.0 | Azure OpenAI client |
| Azure.Identity | 1.13.0 | DefaultAzureCredential |
| Azure.ResourceManager.ApiManagement | 1.2.0 | ARM API for APIM metadata |
| Microsoft.Azure.Functions.Worker | 2.51.0 | Functions isolated host |
| Microsoft.Extensions.AI | 9.5.0 | AI abstractions (IChatClient, AIFunction) |
| Microsoft.Extensions.AI.OpenAI | 10.3.0 | OpenAI integration for M.E.AI |

---

## 7. Deployment

### 7.1 Prerequisites

- .NET 10 SDK
- Azure Functions Core Tools v4
- Azure CLI (logged in)
- Docker
- Azure subscription with at least one APIM instance
- (Optional) Azure OpenAI resource for chat endpoint

### 7.2 Automated Deployment

```powershell
# Full deployment (build + infra + restart)
./deploy.ps1

# Skip Docker build (infra + restart only)
./deploy.ps1 -SkipBuild

# Skip Bicep (build + restart only)
./deploy.ps1 -SkipInfra
```

**Deployment script steps:**
1. `docker build` - Multi-stage build from Dockerfile
2. `az acr login` + `docker push` - Push image to ACR
3. `az deployment group create` - Deploy Bicep infrastructure
4. `az containerapp update` - Update Container App to pull latest image

### 7.3 Post-Deployment: Enable MCP Server in APIM

1. Navigate to your APIM instance in Azure Portal
2. Go to **APIs > MCP Servers** (preview feature)
3. Add the deployed API (`apim-mcp-agent`) as an MCP server
4. APIM automatically maps REST operations to MCP tools
5. AI agents can now connect to `https://{apim-name}.azure-api.net/mcp-agent` as an MCP endpoint

### 7.4 Local Development

```bash
cd src/AzureApimMcp.Functions
func start   # Runs on http://localhost:7071
```

Configure `local.settings.json` with target APIM instance details and optionally Azure OpenAI credentials.

---

## 8. Configuration

### 8.1 Multi-Instance Support

The app supports querying multiple APIM instances simultaneously. Each instance is configured as an indexed entry:

```
Apim__Instances__0__Name=production
Apim__Instances__0__SubscriptionId=<sub-id>
Apim__Instances__0__ResourceGroup=<rg>
Apim__Instances__0__ServiceName=<apim-name>

Apim__Instances__1__Name=staging
Apim__Instances__1__SubscriptionId=<sub-id>
Apim__Instances__1__ResourceGroup=<rg>
Apim__Instances__1__ServiceName=<apim-name>
```

### 8.2 Azure OpenAI (Optional)

The chat endpoint is conditionally registered. If these values are missing, all other endpoints still function:

```
AzureOpenAI__Endpoint=https://<account>.cognitiveservices.azure.com/
AzureOpenAI__DeploymentName=gpt-4o-mini
```

---

## 9. Observability

- **Structured logging**: All services use `ILogger<T>` with structured log messages
- **Log Analytics**: Container App logs flow to a Log Analytics workspace (30-day retention)
- **APIM diagnostics**: Standard APIM logging and analytics apply
- **Health endpoint**: `/health` returns `{"status":"ok"}` for monitoring and probes

---

## 10. Limitations & Future Considerations

| Area | Current State | Future |
|---|---|---|
| APIM instances | Configured at deploy time | Dynamic instance discovery |
| Authentication | Subscription key + Entra ID | OAuth 2.0 flows for end-users |
| Chat responses | Buffered (full response) | Streaming via SSE (`GetStreamingResponseAsync` + APIM `buffer-response="false"`) |
| Chat sessions | In-memory (ConcurrentDictionary) | Persistent storage (Redis/Cosmos) |
| Search | Substring matching | Semantic search with embeddings |
| Spec caching | No caching | Cache specs to reduce ARM calls |
| Catalog performance | N+1 ARM calls per request | Cache with `IMemoryCache` (5-min TTL) or Azure AI Search index |
| Network isolation | External ingress | VNET integration for private connectivity |
| MCP transport | APIM built-in (HTTP+SSE) | Streamable HTTP transport |
