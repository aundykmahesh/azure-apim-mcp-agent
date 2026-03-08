using AzureApimMcp.Functions.Functions;
using AzureApimMcp.Functions.Services;
using AzureApimMcp.Functions.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AzureApimMcp.Functions.Tests.Functions;

public class ApiFunctionsTests
{
    private readonly Mock<IApimService> _apimServiceMock = new();
    private readonly ILogger<ApiFunctions> _logger = NullLogger<ApiFunctions>.Instance;
    private readonly ApiFunctions _sut;

    public ApiFunctionsTests()
    {
        _sut = new ApiFunctions(_apimServiceMock.Object, _logger);
    }

    // ──────────────────────────────────────
    // ListApis
    // ──────────────────────────────────────

    [Fact]
    public async Task ListApis_ReturnsOk_WithApiList()
    {
        var apis = new List<ApiSummary>
        {
            new("payment-api", "Payment API", "/payments", "Handles payments"),
            new("user-api", "User API", "/users", null)
        };
        _apimServiceMock
            .Setup(s => s.ListApisAsync("dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(apis);

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.ListApis(request, "dev");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<ApiSummary>>(okResult.Value);
        Assert.Equal(2, returned.Count);
        Assert.Equal("Payment API", returned[0].DisplayName);
    }

    [Fact]
    public async Task ListApis_ReturnsNotFound_WhenInstanceNotFound()
    {
        _apimServiceMock
            .Setup(s => s.ListApisAsync("unknown", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("APIM instance 'unknown' not found."));

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.ListApis(request, "unknown");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(notFound.Value);
        Assert.Contains("unknown", json);
    }

    // ──────────────────────────────────────
    // SearchApis
    // ──────────────────────────────────────

    [Fact]
    public async Task SearchApis_ReturnsOk_WithMatchingApis()
    {
        var apis = new List<ApiSummary>
        {
            new("payment-api", "Payment API", "/payments", "Handles payments")
        };
        _apimServiceMock
            .Setup(s => s.SearchApisAsync("dev", "payment", It.IsAny<CancellationToken>()))
            .ReturnsAsync(apis);

        var request = TestHttpRequestHelper.CreateGet(new() { ["keyword"] = "payment" });
        var result = await _sut.SearchApis(request, "dev");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<ApiSummary>>(okResult.Value);
        Assert.Single(returned);
    }

    [Fact]
    public async Task SearchApis_ReturnsBadRequest_WhenKeywordMissing()
    {
        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.SearchApis(request, "dev");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("keyword", json);
    }

    [Fact]
    public async Task SearchApis_ReturnsBadRequest_WhenKeywordEmpty()
    {
        var request = TestHttpRequestHelper.CreateGet(new() { ["keyword"] = "" });
        var result = await _sut.SearchApis(request, "dev");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SearchApis_ReturnsNotFound_WhenInstanceNotFound()
    {
        _apimServiceMock
            .Setup(s => s.SearchApisAsync("unknown", "test", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("APIM instance 'unknown' not found."));

        var request = TestHttpRequestHelper.CreateGet(new() { ["keyword"] = "test" });
        var result = await _sut.SearchApis(request, "unknown");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ──────────────────────────────────────
    // GetApiDetails
    // ──────────────────────────────────────

    [Fact]
    public async Task GetApiDetails_ReturnsOk_WithDetails()
    {
        var details = new ApiDetails(
            "payment-api", "Payment API", "Handles payments",
            "/payments", "https://backend.com", new[] { "https" });
        _apimServiceMock
            .Setup(s => s.GetApiDetailsAsync("dev", "payment-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.GetApiDetails(request, "dev", "payment-api");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<ApiDetails>(okResult.Value);
        Assert.Equal("Payment API", returned.DisplayName);
        Assert.Equal("https://backend.com", returned.ServiceUrl);
    }

    [Fact]
    public async Task GetApiDetails_ReturnsNotFound_WhenApiNotFound()
    {
        _apimServiceMock
            .Setup(s => s.GetApiDetailsAsync("dev", "nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiDetails?)null);

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.GetApiDetails(request, "dev", "nonexistent");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(notFound.Value);
        Assert.Contains("nonexistent", json);
    }

    [Fact]
    public async Task GetApiDetails_ReturnsNotFound_WhenInstanceNotFound()
    {
        _apimServiceMock
            .Setup(s => s.GetApiDetailsAsync("unknown", "any", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("APIM instance 'unknown' not found."));

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.GetApiDetails(request, "unknown", "any");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ──────────────────────────────────────
    // DownloadApiSpec
    // ──────────────────────────────────────

    [Fact]
    public async Task DownloadApiSpec_ReturnsContentResult_WithSpec()
    {
        var spec = """{"openapi":"3.0.1","info":{"title":"Test"}}""";
        _apimServiceMock
            .Setup(s => s.DownloadApiSpecAsync("dev", "test-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(spec);

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.DownloadApiSpec(request, "dev", "test-api");

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(200, content.StatusCode);
        Assert.Equal("application/json", content.ContentType);
        Assert.Contains("openapi", content.Content);
    }

    [Fact]
    public async Task DownloadApiSpec_ReturnsNotFound_WhenServiceReturnsApiNotFoundMessage()
    {
        _apimServiceMock
            .Setup(s => s.DownloadApiSpecAsync("dev", "missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync("API 'missing' not found in instance 'dev'.");

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.DownloadApiSpec(request, "dev", "missing");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DownloadApiSpec_ReturnsNotFound_WhenServiceReturnsFailedMessage()
    {
        _apimServiceMock
            .Setup(s => s.DownloadApiSpecAsync("dev", "broken", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Failed to get spec export link: NotFound");

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.DownloadApiSpec(request, "dev", "broken");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DownloadApiSpec_ReturnsNotFound_WhenServiceReturnsCouldNotMessage()
    {
        _apimServiceMock
            .Setup(s => s.DownloadApiSpecAsync("dev", "broken", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Could not extract spec URL from export response.");

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.DownloadApiSpec(request, "dev", "broken");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ──────────────────────────────────────
    // ListApiOperations
    // ──────────────────────────────────────

    [Fact]
    public async Task ListApiOperations_ReturnsOk_WithOperations()
    {
        var operations = new List<ApiOperationSummary>
        {
            new("get-users", "GET", "/users", "Get Users", "Returns all users"),
            new("create-user", "POST", "/users", "Create User", "Creates a new user")
        };
        _apimServiceMock
            .Setup(s => s.ListApiOperationsAsync("dev", "user-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(operations);

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.ListApiOperations(request, "dev", "user-api");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<ApiOperationSummary>>(okResult.Value);
        Assert.Equal(2, returned.Count);
        Assert.Equal("GET", returned[0].Method);
        Assert.Equal("/users", returned[0].UriTemplate);
    }

    [Fact]
    public async Task ListApiOperations_ReturnsNotFound_WhenApiNotFound()
    {
        _apimServiceMock
            .Setup(s => s.ListApiOperationsAsync("dev", "nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiOperationSummary>());

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.ListApiOperations(request, "dev", "nonexistent");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(notFound.Value);
        Assert.Contains("nonexistent", json);
    }

    [Fact]
    public async Task ListApiOperations_ReturnsNotFound_WhenInstanceNotFound()
    {
        _apimServiceMock
            .Setup(s => s.ListApiOperationsAsync("unknown", "any", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("APIM instance 'unknown' not found."));

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.ListApiOperations(request, "unknown", "any");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ──────────────────────────────────────
    // GetApiCatalog
    // ──────────────────────────────────────

    [Fact]
    public async Task GetApiCatalog_ReturnsOk_WithCatalog()
    {
        var catalog = new List<ApiCatalogEntry>
        {
            new("user-api", "User API", "/users", "User management", new List<ApiOperationSummary>
            {
                new("get-users", "GET", "/users", "Get Users", "Returns all users"),
                new("create-user", "POST", "/users", "Create User", null)
            }),
            new("payment-api", "Payment API", "/payments", null, new List<ApiOperationSummary>())
        };
        _apimServiceMock
            .Setup(s => s.GetApiCatalogAsync("dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalog);

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.GetApiCatalog(request, "dev");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<ApiCatalogEntry>>(okResult.Value);
        Assert.Equal(2, returned.Count);
        Assert.Equal(2, returned[0].Operations.Count);
        Assert.Empty(returned[1].Operations);
    }

    [Fact]
    public async Task GetApiCatalog_ReturnsOk_WhenEmpty()
    {
        _apimServiceMock
            .Setup(s => s.GetApiCatalogAsync("dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiCatalogEntry>());

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.GetApiCatalog(request, "dev");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<ApiCatalogEntry>>(okResult.Value);
        Assert.Empty(returned);
    }

    [Fact]
    public async Task GetApiCatalog_ReturnsNotFound_WhenInstanceNotFound()
    {
        _apimServiceMock
            .Setup(s => s.GetApiCatalogAsync("unknown", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("APIM instance 'unknown' not found."));

        var request = TestHttpRequestHelper.CreateGet();
        var result = await _sut.GetApiCatalog(request, "unknown");

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
