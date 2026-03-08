using AzureApimMcp.Functions.Functions;
using AzureApimMcp.Functions.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace AzureApimMcp.Functions.Tests.Functions;

public class HealthFunctionTests
{
    private readonly HealthFunction _sut = new();

    [Fact]
    public void Health_ReturnsOk_WithStatusOk()
    {
        var request = TestHttpRequestHelper.CreateGet();

        var result = _sut.Health(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // The anonymous object { status = "ok" } should serialize correctly
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("\"status\"", json);
        Assert.Contains("ok", json);
    }

    [Fact]
    public void Health_Returns200StatusCode()
    {
        var request = TestHttpRequestHelper.CreateGet();

        var result = _sut.Health(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }
}
