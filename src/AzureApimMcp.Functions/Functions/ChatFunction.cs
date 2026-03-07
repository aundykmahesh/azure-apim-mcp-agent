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
        "You are an AI assistant that helps users discover and explore APIs " +
        "in Azure API Management. You can list APIM instances, search for APIs, " +
        "get API details, and download OpenAPI specifications. " +
        "When a user asks about APIs, use the available tools to find the information. " +
        "Always be helpful and concise.";

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
            _logger.LogInformation("Chat request | Session={SessionId} Message={Message}", sessionId, body.Message);

            var history = Sessions.GetOrAdd(sessionId, _ =>
                [new(ChatRole.System, SystemPrompt)]);

            history.Add(new ChatMessage(ChatRole.User, body.Message));

            var options = new ChatOptions
            {
                Tools = [.. CreateTools()]
            };

            _logger.LogInformation("Calling Azure OpenAI with {ToolCount} tools", options.Tools.Count);
            var response = await _chatClient.GetResponseAsync(history, options);
            history.AddRange(response.Messages);

            _logger.LogInformation("Chat response received | Session={SessionId}", sessionId);
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
                "Downloads and returns the full OpenAPI specification content for a specific API.")
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
