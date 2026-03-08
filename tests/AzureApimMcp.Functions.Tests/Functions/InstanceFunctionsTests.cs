using AzureApimMcp.Functions.Functions;
using AzureApimMcp.Functions.Services;
using AzureApimMcp.Functions.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AzureApimMcp.Functions.Tests.Functions;

public class InstanceFunctionsTests
{
    private readonly Mock<IApimService> _apimServiceMock = new();
    private readonly InstanceFunctions _sut;

    public InstanceFunctionsTests()
    {
        _sut = new InstanceFunctions(_apimServiceMock.Object);
    }

    [Fact]
    public void ListInstances_ReturnsOk_WithInstanceNames()
    {
        _apimServiceMock
            .Setup(s => s.GetConfiguredInstances())
            .Returns(new List<string> { "dev", "production" });

        var request = TestHttpRequestHelper.CreateGet();

        var result = _sut.ListInstances(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("dev", json);
        Assert.Contains("production", json);
    }

    [Fact]
    public void ListInstances_ReturnsOk_WhenNoInstancesConfigured()
    {
        _apimServiceMock
            .Setup(s => s.GetConfiguredInstances())
            .Returns(new List<string>());

        var request = TestHttpRequestHelper.CreateGet();

        var result = _sut.ListInstances(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public void ListInstances_CallsServiceOnce()
    {
        _apimServiceMock
            .Setup(s => s.GetConfiguredInstances())
            .Returns(new List<string> { "dev" });

        var request = TestHttpRequestHelper.CreateGet();

        _sut.ListInstances(request);

        _apimServiceMock.Verify(s => s.GetConfiguredInstances(), Times.Once);
    }
}
