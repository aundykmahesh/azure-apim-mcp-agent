# AI-Powered API Discovery with Azure APIM MCP Agent

## Stakeholder Overview

---

## The Problem

As organisations scale their API landscape across Azure API Management, developers and teams face a growing challenge: **finding the right API for the job**.

Today, discovering APIs typically means:

- Manually browsing the APIM Developer Portal
- Searching through Confluence or SharePoint for outdated documentation
- Asking colleagues in Slack/Teams *"Is there an API for X?"*
- Reading through lengthy OpenAPI specs to understand what an API does

This friction slows down development, leads to duplicate API creation, and reduces the return on investment in our API platform.

---

## The Solution: AI Agents That Understand Our APIs

The **Azure APIM MCP Agent** enables AI assistants — such as Claude, Microsoft 365 Copilot, and VS Code extensions — to **discover, search, and understand our APIs using natural language**.

Instead of browsing a portal, a developer simply asks:

> *"Are there any APIs related to payments?"*

And the AI agent responds with a list of matching APIs, their descriptions, endpoints, and even their full OpenAPI specifications — all retrieved live from Azure API Management.

---

## How It Works

### For the Developer (End User)

```
Developer:  "Which API gives me a customer's transaction history?"

AI Agent:   I surveyed the API catalog and found a match:

            **Banking API** (path: /banking)
            - GET /customers/{customerId}/transactions
              "Retrieves the transaction history for a given customer"

            Would you like to see all operations in the Banking API,
            or download the full OpenAPI spec?

Developer:  "What other endpoints does the Banking API have?"

AI Agent:   The Banking API has 6 operations:
            | Method | Path                              | Description              |
            |--------|-----------------------------------|--------------------------|
            | GET    | /customers/{id}/transactions      | Transaction history      |
            | GET    | /customers/{id}/balance           | Current balance          |
            | POST   | /transfers                        | Initiate a transfer      |
            | ...    | ...                               | ...                      |
```

The developer never leaves their IDE or chat tool. The AI agent handles the discovery.

### Under The Hood

```
   Developer's AI Tool             Azure Cloud
   (Claude / Copilot)         ________________________
         |                   |                        |
         |  MCP Protocol     |  Azure API Management  |
         +------------------>|  (MCP Server)          |
                             |         |               |
                             |    REST API calls       |
                             |         |               |
                             |  Azure Container Apps   |
                             |  (Functions backend)    |
                             |         |               |
                             |  Azure ARM APIs         |
                             |  (API metadata)         |
                             |________________________|
```

- **Azure API Management** acts as the MCP Server (a standard protocol for AI tool integration)
- A lightweight **Azure Functions** backend queries APIM for API metadata
- The AI agent uses this to answer developer questions about available APIs
- Everything runs on **managed identity** — no secrets, no credentials to manage

---

## What is MCP (Model Context Protocol)?

MCP is an open protocol (created by Anthropic, adopted by Microsoft) that allows AI agents to discover and use external tools. Think of it as a **universal plugin system for AI**:

- **MCP Client**: The AI agent (Claude, Copilot, VS Code)
- **MCP Server**: Exposes tools that the AI can call (in our case, Azure APIM)
- **Tools**: Actions the AI can perform (list APIs, search APIs, download specs)

Azure API Management has **built-in MCP Server support**, meaning we don't need to build the protocol layer ourselves. We only need to provide the REST endpoints that return API metadata.

---

## Capabilities

| Capability | Description |
|---|---|
| **List APIs** | Show all APIs registered in any APIM instance |
| **Search APIs** | Find APIs by keyword across names, descriptions, and paths |
| **API Details** | Get metadata including description, service URL, and protocols |
| **Operation Discovery** | Drill into any API to see all its HTTP endpoints (method, path, description) |
| **API Catalog** | Get a complete overview of all APIs with their operations in a single call |
| **Natural Language Query** | Ask *"which API gives me X?"* and the AI finds the exact endpoint |
| **OpenAPI Specs** | Download the full OpenAPI specification for any API |
| **Multi-Instance** | Query across multiple APIM instances (dev, staging, production) |

---

## Value Proposition

### For Developers
- **Faster discovery**: Find the right API in seconds, not hours
- **Always current**: Queries live APIM metadata, never stale documentation
- **Context-aware**: AI understands the full OpenAPI spec and can answer detailed questions
- **IDE-integrated**: Works directly in VS Code, Claude, or Copilot — no portal switching

### For API Platform Teams
- **Increased API adoption**: Easier discovery means more reuse, fewer duplicates
- **Reduced support burden**: Developers self-serve instead of asking *"where's the API for X?"*
- **Governance visibility**: See which APIs are discoverable and well-documented
- **Living documentation**: API metadata in APIM becomes the single source of truth

### For the Organisation
- **Accelerated delivery**: Developers spend less time searching, more time building
- **Better ROI on API investments**: APIs that are easily found get used
- **AI-ready infrastructure**: Establishes the pattern for AI-augmented developer workflows
- **Low cost**: Container Apps scale to zero when idle; minimal ongoing Azure spend

---

## Security & Governance

| Concern | How It's Addressed |
|---|---|
| **Authentication** | APIM subscription key + Entra ID — existing APIM auth model |
| **Authorization** | Managed identity with RBAC; least-privilege (Reader role only) |
| **No stored secrets** | Zero credentials in code or config; `DefaultAzureCredential` throughout |
| **Data exposure** | Only reads API metadata — no access to actual API data or payloads |
| **Audit trail** | All requests flow through APIM, fully logged and traceable |
| **Network** | Can be deployed with VNET integration for private connectivity |

---

## Cost Profile

| Resource | Cost Model |
|---|---|
| Azure Container Apps | **Consumption**: scale-to-zero, pay only for active requests |
| Azure API Management | Uses existing APIM instance — **no additional APIM cost** |
| Azure OpenAI (optional) | Pay-per-token for chat endpoint only |
| Container Registry | Basic SKU (~$5/month) |
| Log Analytics | 30-day retention, minimal ingestion |

**Estimated cost**: Minimal. The solution piggybacks on existing APIM infrastructure. Container Apps cost near-zero at low usage.

---

## Onboarding for Other Teams

### What's Needed to Add Your APIs

If your APIs are already in Azure API Management, **they are automatically discoverable** — no changes needed. The agent reads metadata directly from APIM.

To improve discoverability:
1. Ensure your APIs have **clear display names** and **descriptions** in APIM
2. Upload **OpenAPI specifications** to APIM for each API
3. Use **meaningful tags** and consistent naming conventions

### Adding Additional APIM Instances

To add a new APIM instance (e.g., a different team's or environment's instance):
1. Add the instance configuration to the deployment parameters
2. Assign the managed identity the **API Management Service Reader** role on the target instance
3. Re-deploy — the new instance is immediately queryable

---

## Roadmap & Future Possibilities

| Phase | Enhancement |
|---|---|
| **Current** | API discovery, search, spec download, AI chat |
| **Near-term** | Semantic search with embeddings for smarter matching |
| **Near-term** | Spec caching for faster responses |
| **Medium-term** | API usage analytics — *"Which APIs are most queried by AI agents?"* |
| **Medium-term** | Proactive recommendations — *"Based on your project, you might need these APIs"* |
| **Long-term** | Code generation — *"Generate a C# client for the Customer API"* |
| **Long-term** | Cross-platform — extend to non-APIM API gateways |

---

## Technology Summary

| Component | Technology |
|---|---|
| Backend | .NET 10, Azure Functions v4 (isolated) |
| Hosting | Azure Container Apps (scale-to-zero) |
| AI | Azure OpenAI (GPT-4o-mini) with function calling |
| Protocol | Model Context Protocol (MCP) via APIM built-in support |
| Infrastructure | Bicep (Infrastructure-as-Code), fully automated |
| Auth | Managed Identity + RBAC (zero secrets) |
| Deployment | PowerShell script, Docker, Azure CLI |

---

## Demo / How to Try It

### Quick Test via curl

```bash
# Health check
curl https://aundy-apim.azure-api.net/mcp-agent/health \
  -H "Ocp-Apim-Subscription-Key: <your-key>"

# Full API catalog (all APIs with their operations)
curl https://aundy-apim.azure-api.net/mcp-agent/instances/dev/catalog \
  -H "Ocp-Apim-Subscription-Key: <your-key>"

# List operations for a specific API
curl https://aundy-apim.azure-api.net/mcp-agent/instances/dev/apis/banking-api/operations \
  -H "Ocp-Apim-Subscription-Key: <your-key>"

# Search for APIs by keyword
curl "https://aundy-apim.azure-api.net/mcp-agent/instances/dev/apis/search?keyword=payment" \
  -H "Ocp-Apim-Subscription-Key: <your-key>"

# Natural language query — ask about capabilities
curl -X POST https://aundy-apim.azure-api.net/mcp-agent/chat \
  -H "Ocp-Apim-Subscription-Key: <your-key>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Which API gives me a customer transaction history?", "sessionId": "demo"}'
```

### Connect from Claude / Copilot

Once the MCP Server is enabled in APIM, AI agents can connect directly using the MCP endpoint URL — enabling the natural language API discovery experience described above.

---

## Summary

The Azure APIM MCP Agent transforms API discovery from a manual, portal-driven process into a **conversational, AI-powered experience**. By leveraging Azure API Management's built-in MCP Server support, we enable AI agents to understand and surface our API landscape — making APIs easier to find, easier to understand, and more likely to be reused.

**This is not about replacing documentation.** It's about making our existing API investments more accessible through the tools developers already use.

| What | Detail |
|---|---|
| **Status** | Working reference implementation, deployed to Azure |
| **Effort** | Lightweight — single Functions app + Bicep infrastructure |
| **Cost** | Minimal — leverages existing APIM, scale-to-zero compute |
| **Risk** | Low — read-only access to API metadata, no data plane exposure |
| **Impact** | High — unlocks AI-powered API discovery for all teams |
