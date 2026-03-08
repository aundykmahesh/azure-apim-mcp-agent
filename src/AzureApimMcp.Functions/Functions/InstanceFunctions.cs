using AzureApimMcp.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace AzureApimMcp.Functions.Functions;

public class InstanceFunctions
{
    private readonly IApimService _apimService;

    public InstanceFunctions(IApimService apimService)
    {
        _apimService = apimService;
    }

    [Function("ListInstances")]
    public IActionResult ListInstances(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instances")] HttpRequest req)
    {
        var instances = _apimService.GetConfiguredInstances();
        return new OkObjectResult(instances.Select(name => new { name }));
    }
}
