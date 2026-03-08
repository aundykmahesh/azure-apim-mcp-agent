using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ApiManagement;
using AzureApimMcp.Functions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzureApimMcp.Functions.Services;

public class ApimService : IApimService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "apis", "rest", "the", "a", "an", "of", "for", "in", "to", "list"
    };

    private readonly ApimSettings _settings;
    private readonly TokenCredential _credential;
    private readonly ArmClient _armClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApimService> _logger;

    public ApimService(
        IOptions<ApimSettings> settings,
        TokenCredential credential,
        ArmClient armClient,
        IHttpClientFactory httpClientFactory,
        ILogger<ApimService> logger)
    {
        _settings = settings.Value;
        _credential = credential;
        _armClient = armClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public IReadOnlyList<string> GetConfiguredInstances()
        => _settings.Instances.Select(i => i.Name).ToList();

    public async Task<IReadOnlyList<ApiSummary>> ListApisAsync(
        string instanceName, CancellationToken ct = default)
    {
        var config = ResolveInstance(instanceName);
        var service = GetService(config);
        var results = new List<ApiSummary>();

        await foreach (var api in service.GetApis().GetAllAsync(cancellationToken: ct))
        {
            results.Add(new ApiSummary(
                api.Data.Name,
                api.Data.DisplayName,
                api.Data.Path,
                api.Data.Description));
        }

        _logger.LogInformation("ListApis | Instance={Instance} Count={Count}",
            instanceName, results.Count);

        return results;
    }

    public async Task<IReadOnlyList<ApiSummary>> SearchApisAsync(
        string instanceName, string keyword, CancellationToken ct = default)
    {
        var words = keyword
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !StopWords.Contains(w))
            .ToArray();

        if (words.Length == 0)
            words = [keyword.Trim()];

        var config = ResolveInstance(instanceName);
        var service = GetService(config);
        var results = new List<ApiSummary>();

        await foreach (var api in service.GetApis().GetAllAsync(cancellationToken: ct))
        {
            bool nameMatch = words.Any(w =>
                api.Data.DisplayName.Contains(w, StringComparison.OrdinalIgnoreCase));
            bool descMatch = words.Any(w =>
                api.Data.Description?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false);
            bool pathMatch = words.Any(w =>
                api.Data.Path?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false);

            if (nameMatch || descMatch || pathMatch)
            {
                results.Add(new ApiSummary(
                    api.Data.Name,
                    api.Data.DisplayName,
                    api.Data.Path,
                    api.Data.Description));
            }
        }

        _logger.LogInformation("SearchApis | Instance={Instance} Keyword={Keyword} Count={Count}",
            instanceName, keyword, results.Count);

        return results;
    }

    public async Task<ApiDetails?> GetApiDetailsAsync(
        string instanceName, string apiNameOrTitle, CancellationToken ct = default)
    {
        var config = ResolveInstance(instanceName);
        var service = GetService(config);

        await foreach (var api in service.GetApis().GetAllAsync(cancellationToken: ct))
        {
            bool exactName = api.Data.Name.Equals(apiNameOrTitle, StringComparison.OrdinalIgnoreCase);
            bool titleMatch = api.Data.DisplayName.Contains(apiNameOrTitle, StringComparison.OrdinalIgnoreCase);

            if (exactName || titleMatch)
            {
                return new ApiDetails(
                    api.Data.Name,
                    api.Data.DisplayName,
                    api.Data.Description,
                    api.Data.Path,
                    api.Data.ServiceLink?.ToString(),
                    api.Data.Protocols?.Select(p => p.ToString()).ToList()
                        ?? (IReadOnlyList<string>)[]);
            }
        }

        return null;
    }

    public async Task<string> DownloadApiSpecAsync(
        string instanceName, string apiNameOrTitle, CancellationToken ct = default)
    {
        var config = ResolveInstance(instanceName);

        // Step 1: Find the API
        var details = await GetApiDetailsAsync(instanceName, apiNameOrTitle, ct);
        if (details is null)
            return $"API '{apiNameOrTitle}' not found in instance '{instanceName}'.";

        // Step 2: Get the export link from ARM
        var exportUrl =
            $"https://management.azure.com/subscriptions/{config.SubscriptionId}" +
            $"/resourceGroups/{config.ResourceGroup}" +
            $"/providers/Microsoft.ApiManagement/service/{config.ServiceName}" +
            $"/apis/{details.Name}" +
            $"?export=true&format=openapi-link&api-version=2022-08-01";

        var token = await _credential.GetTokenAsync(
            new TokenRequestContext(["https://management.azure.com/.default"]), ct);

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);

        var exportResponse = await httpClient.GetAsync(exportUrl, ct);
        var exportBody = await exportResponse.Content.ReadAsStringAsync(ct);

        if (!exportResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get export link | Status={Status} Body={Body}",
                exportResponse.StatusCode, exportBody);
            return $"Failed to get spec export link: {exportResponse.StatusCode}";
        }

        // Step 3: Parse the spec URL from the response
        // ARM returns { "value": { "link": "https://..." } } for openapi-link format
        _logger.LogInformation("DownloadSpec | ExportBody={Body}", exportBody);
        using var doc = JsonDocument.Parse(exportBody);
        string? specUrl = null;
        if (doc.RootElement.TryGetProperty("value", out var valEl))
        {
            if (valEl.ValueKind == JsonValueKind.String)
                specUrl = valEl.GetString();
            else if (valEl.ValueKind == JsonValueKind.Object && valEl.TryGetProperty("link", out var linkEl))
                specUrl = linkEl.GetString();
        }

        if (string.IsNullOrWhiteSpace(specUrl))
            return $"Could not extract spec URL from export response. Body: {exportBody}";

        // Step 4: Download the actual spec content
        _logger.LogInformation("DownloadSpec | API={Api} SpecUrl={Url}", details.DisplayName, specUrl);

        var specClient = _httpClientFactory.CreateClient();
        var specResponse = await specClient.GetAsync(specUrl, ct);
        var specContent = await specResponse.Content.ReadAsStringAsync(ct);

        _logger.LogInformation("DownloadSpec | Status={Status} ContentLength={Length}",
            specResponse.StatusCode, specContent.Length);

        if (string.IsNullOrWhiteSpace(specContent))
            return $"Spec downloaded but content was empty (HTTP {specResponse.StatusCode}).";

        return specContent;
    }

    private ApimInstanceConfig ResolveInstance(string instanceName)
    {
        var config = _settings.Instances.FirstOrDefault(i =>
            i.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase));

        if (config is null)
        {
            var available = string.Join(", ", _settings.Instances.Select(i => i.Name));
            throw new ArgumentException(
                $"APIM instance '{instanceName}' not found. Available: {available}");
        }

        return config;
    }

    private ApiManagementServiceResource GetService(ApimInstanceConfig config)
    {
        var id = ApiManagementServiceResource.CreateResourceIdentifier(
            config.SubscriptionId, config.ResourceGroup, config.ServiceName);
        return _armClient.GetApiManagementServiceResource(id);
    }
}
