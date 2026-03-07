using AzureApimMcp.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureApimMcp.Functions.Functions;

public class ApiFunctions
{
    private readonly IApimService _apimService;
    private readonly ILogger<ApiFunctions> _logger;

    public ApiFunctions(IApimService apimService, ILogger<ApiFunctions> logger)
    {
        _apimService = apimService;
        _logger = logger;
    }

    [Function("ListApis")]
    public async Task<IActionResult> ListApis(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instances/{instanceName}/apis")]
        HttpRequest req,
        string instanceName)
    {
        try
        {
            var apis = await _apimService.ListApisAsync(instanceName, req.HttpContext.RequestAborted);
            return new OkObjectResult(apis);
        }
        catch (ArgumentException ex)
        {
            return new NotFoundObjectResult(new { error = ex.Message });
        }
    }

    [Function("SearchApis")]
    public async Task<IActionResult> SearchApis(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instances/{instanceName}/apis/search")]
        HttpRequest req,
        string instanceName)
    {
        var keyword = req.Query["keyword"].ToString();
        if (string.IsNullOrWhiteSpace(keyword))
            return new BadRequestObjectResult(new { error = "Query parameter 'keyword' is required." });

        try
        {
            var results = await _apimService.SearchApisAsync(
                instanceName, keyword, req.HttpContext.RequestAborted);
            return new OkObjectResult(results);
        }
        catch (ArgumentException ex)
        {
            return new NotFoundObjectResult(new { error = ex.Message });
        }
    }

    [Function("GetApiDetails")]
    public async Task<IActionResult> GetApiDetails(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instances/{instanceName}/apis/{apiId}")]
        HttpRequest req,
        string instanceName,
        string apiId)
    {
        try
        {
            var details = await _apimService.GetApiDetailsAsync(
                instanceName, apiId, req.HttpContext.RequestAborted);

            if (details is null)
                return new NotFoundObjectResult(new { error = $"API '{apiId}' not found." });

            return new OkObjectResult(details);
        }
        catch (ArgumentException ex)
        {
            return new NotFoundObjectResult(new { error = ex.Message });
        }
    }

    [Function("DownloadApiSpec")]
    public async Task<IActionResult> DownloadApiSpec(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instances/{instanceName}/apis/{apiId}/spec")]
        HttpRequest req,
        string instanceName,
        string apiId)
    {
        try
        {
            var spec = await _apimService.DownloadApiSpecAsync(
                instanceName, apiId, req.HttpContext.RequestAborted);

            if (spec.StartsWith("API '") || spec.StartsWith("Failed ") || spec.StartsWith("Could not"))
            {
                _logger.LogWarning("DownloadApiSpec returned error message: {Message}", spec);
                return new NotFoundObjectResult(new { error = spec });
            }

            return new ContentResult
            {
                Content = spec,
                ContentType = "application/json",
                StatusCode = 200
            };
        }
        catch (ArgumentException ex)
        {
            return new NotFoundObjectResult(new { error = ex.Message });
        }
    }
}
