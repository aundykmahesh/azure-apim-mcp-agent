using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json.Serialization;
using AzureApimMcp.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AzureApimMcp.Functions.Functions;

public class ChatFunction
{
    private readonly IChatClient _chatClient;
    private readonly IApimService _apimService;
    private readonly ILogger<ChatFunction> _logger;

    private static readonly ConcurrentDictionary<string, List<ChatMessage>> Sessions = new();

    private const string SystemPrompt =
        "You are a Copilot for the API Catalogue — an expert assistant that helps developers, architects, and other business stakeholders" +
        "discover which APIs and specific endpoints exist in Azure API Management, and understand " +
        "what each one does.\n\n" +
        "STRATEGY:\n" +
        "1. When a user asks a vague or broad question (e.g. 'what payment APIs exist?'), " +
        "start with get_api_catalog to see all APIs and their operations in one view.\n" +
        "2. When a user asks about a specific capability (e.g. 'which endpoint gives me a " +
        "customer's transaction history?'), first call get_api_catalog to survey the landscape, " +
        "then use list_api_operations on candidate APIs to confirm the exact endpoint.\n" +
        "3. Keyword search (search_apis) is a good shortcut when the user provides a clear domain " +
        "term. But search only matches API-level metadata (name, description, path), not the " +
        "individual operations inside. If search returns no or ambiguous results, browse the catalog.\n" +
        "4. When you identify a candidate API, always check its operations to confirm it actually " +
        "has the endpoint the user needs. Do not guess from the API name alone.\n" +
        "5. Only call download_api_spec when the user explicitly asks for the full spec or when " +
        "you need precise request/response schema details. Prefer list_api_operations for discovery.\n\n" +
        "RESPONSE STYLE:\n" +
        "- Answer concisely. State the API name, the HTTP method and path of the relevant " +
        "operation, and a one-sentence description.\n" +
        "- When multiple APIs match, list all candidates with their key operations.\n" +
        "- If nothing matches after a thorough search, say so clearly and suggest what the user " +
        "might try next.\n" +
        "- Use markdown tables or bullet lists when comparing multiple APIs or operations.";

    public ChatFunction(IChatClient chatClient, IApimService apimService, ILogger<ChatFunction> logger)
    {
        _chatClient = chatClient;
        _apimService = apimService;
        _logger = logger;
    }

    [Function("Chat")]
    public async Task<IActionResult> Chat(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "chat")] HttpRequest req)
    {
        try
        {
            var body = await req.ReadFromJsonAsync<ChatRequest>();
            if (string.IsNullOrWhiteSpace(body?.Message))
                return new BadRequestObjectResult(new { error = "Message is required." });

            var sessionId = body.SessionId ?? "default";
            _logger.LogDebug("Chat request | Session={SessionId} Message={Message}", sessionId, body.Message);

            var history = Sessions.GetOrAdd(sessionId, _ =>
                [new(ChatRole.System, SystemPrompt)]);

            history.Add(new ChatMessage(ChatRole.User, body.Message));

            var options = new ChatOptions
            {
                Tools = [.. CreateTools()]
            };

            _logger.LogDebug("Calling Azure OpenAI with {ToolCount} tools", options.Tools.Count);
            var response = await _chatClient.GetResponseAsync(history, options);
            history.AddRange(response.Messages);

            _logger.LogDebug("Chat response received | Session={SessionId}", sessionId);
            return new OkObjectResult(new { response = response.Text, sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat endpoint failed");
            return new ObjectResult(new { error = ex.Message, type = ex.GetType().Name })
            {
                StatusCode = 500
            };
        }
    }

    private List<AITool> CreateTools()
    {
        return
        [
            AIFunctionFactory.Create(
                () => _apimService.GetConfiguredInstances(),
                "list_apim_instances",
                "Lists all configured Azure API Management instances available for querying."),

            AIFunctionFactory.Create(
                ([Description("Name of the APIM instance to query (use list_apim_instances to see available names)")] string instanceName) =>
                    _apimService.ListApisAsync(instanceName),
                "list_apis",
                "Lists all APIs registered in a specific APIM instance. Returns name, display name, path, and description."),

            AIFunctionFactory.Create(
                ([Description("Search keyword (e.g., 'payments', 'users')")] string keyword,
                 [Description("Name of the APIM instance to search")] string instanceName) =>
                    _apimService.SearchApisAsync(instanceName, keyword),
                "search_apis",
                "Searches APIs by keyword matching against name, description, and URL path."),

            AIFunctionFactory.Create(
                ([Description("The API display name or internal name to look up")] string apiNameOrTitle,
                 [Description("Name of the APIM instance containing the API")] string instanceName) =>
                    _apimService.GetApiDetailsAsync(instanceName, apiNameOrTitle),
                "get_api_details",
                "Gets detailed metadata for a specific API including description, service URL, and supported protocols."),

            AIFunctionFactory.Create(
                ([Description("The API display name or internal name")] string apiNameOrTitle,
                 [Description("Name of the APIM instance containing the API")] string instanceName) =>
                    _apimService.DownloadApiSpecAsync(instanceName, apiNameOrTitle),
                "download_api_spec",
                "Downloads and returns the full OpenAPI specification content for a specific API."),

            AIFunctionFactory.Create(
                ([Description("The API display name or internal name to list operations for")] string apiNameOrTitle,
                 [Description("Name of the APIM instance containing the API")] string instanceName) =>
                    _apimService.ListApiOperationsAsync(instanceName, apiNameOrTitle),
                "list_api_operations",
                "Lists all HTTP operations (endpoints) for a specific API. Returns the HTTP method, " +
                "URL template, display name, and description for each operation. Use this to drill into " +
                "a specific API and find which endpoint handles a particular capability."),

            AIFunctionFactory.Create(
                ([Description("Name of the APIM instance to get the full catalog for")] string instanceName) =>
                    _apimService.GetApiCatalogAsync(instanceName),
                "get_api_catalog",
                "Returns all APIs in an APIM instance together with their operations in a single call. " +
                "Use this for broad discovery questions or when you need to survey all available endpoints. " +
                "Preferred over making multiple separate list_apis + list_api_operations calls.")
        ];
    }
}

public class ChatRequest
{
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
