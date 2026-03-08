using Azure.Core;
using Azure.ResourceManager;
using AzureApimMcp.Functions.Configuration;
using AzureApimMcp.Functions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AzureApimMcp.Functions.Tests.Services;

public class ApimServiceTests
{
    private readonly ILogger<ApimService> _logger = NullLogger<ApimService>.Instance;

    private ApimService CreateService(ApimSettings settings)
    {
        var options = Options.Create(settings);
        var credential = new Mock<TokenCredential>();
        var armClient = new ArmClient(credential.Object);
        var httpClientFactory = new Mock<IHttpClientFactory>();

        return new ApimService(options, credential.Object, armClient, httpClientFactory.Object, _logger);
    }

    // ──────────────────────────────────────
    // GetConfiguredInstances
    // ──────────────────────────────────────

    [Fact]
    public void GetConfiguredInstances_ReturnsInstanceNames()
    {
        var settings = new ApimSettings
        {
            Instances =
            [
                new() { Name = "dev", SubscriptionId = "sub1", ResourceGroup = "rg1", ServiceName = "svc1" },
                new() { Name = "prod", SubscriptionId = "sub2", ResourceGroup = "rg2", ServiceName = "svc2" }
            ]
        };

        var service = CreateService(settings);
        var result = service.GetConfiguredInstances();

        Assert.Equal(2, result.Count);
        Assert.Equal("dev", result[0]);
        Assert.Equal("prod", result[1]);
    }

    [Fact]
    public void GetConfiguredInstances_ReturnsEmpty_WhenNoInstances()
    {
        var settings = new ApimSettings { Instances = [] };

        var service = CreateService(settings);
        var result = service.GetConfiguredInstances();

        Assert.Empty(result);
    }

    [Fact]
    public void GetConfiguredInstances_ReturnsSingleInstance()
    {
        var settings = new ApimSettings
        {
            Instances =
            [
                new() { Name = "staging", SubscriptionId = "sub", ResourceGroup = "rg", ServiceName = "svc" }
            ]
        };

        var service = CreateService(settings);
        var result = service.GetConfiguredInstances();

        Assert.Single(result);
        Assert.Equal("staging", result[0]);
    }

    // ──────────────────────────────────────
    // ResolveInstance (tested via public methods throwing)
    // ──────────────────────────────────────

    [Fact]
    public async Task ListApis_ThrowsArgumentException_WhenInstanceNotFound()
    {
        var settings = new ApimSettings
        {
            Instances =
            [
                new() { Name = "dev", SubscriptionId = "sub", ResourceGroup = "rg", ServiceName = "svc" }
            ]
        };

        var service = CreateService(settings);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.ListApisAsync("nonexistent"));

        Assert.Contains("nonexistent", ex.Message);
        Assert.Contains("dev", ex.Message); // lists available instances
    }

    [Fact]
    public async Task SearchApis_ThrowsArgumentException_WhenInstanceNotFound()
    {
        var settings = new ApimSettings { Instances = [] };
        var service = CreateService(settings);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.SearchApisAsync("any", "keyword"));

        Assert.Contains("any", ex.Message);
    }

    [Fact]
    public async Task GetApiDetails_ThrowsArgumentException_WhenInstanceNotFound()
    {
        var settings = new ApimSettings { Instances = [] };
        var service = CreateService(settings);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetApiDetailsAsync("missing", "api"));
    }

    [Fact]
    public async Task DownloadApiSpec_ThrowsArgumentException_WhenInstanceNotFound()
    {
        var settings = new ApimSettings { Instances = [] };
        var service = CreateService(settings);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.DownloadApiSpecAsync("missing", "api"));
    }

    [Fact]
    public async Task ListApiOperations_ThrowsArgumentException_WhenInstanceNotFound()
    {
        var settings = new ApimSettings { Instances = [] };
        var service = CreateService(settings);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ListApiOperationsAsync("missing", "api"));
    }

    [Fact]
    public async Task GetApiCatalog_ThrowsArgumentException_WhenInstanceNotFound()
    {
        var settings = new ApimSettings { Instances = [] };
        var service = CreateService(settings);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetApiCatalogAsync("missing"));
    }

    [Fact]
    public async Task ResolveInstance_IsCaseInsensitive()
    {
        var settings = new ApimSettings
        {
            Instances =
            [
                new() { Name = "Dev", SubscriptionId = "sub", ResourceGroup = "rg", ServiceName = "svc" }
            ]
        };

        var service = CreateService(settings);

        // Should NOT throw — "dev" should match "Dev" case-insensitively
        // (It will throw a different exception when trying to call ARM, but not ArgumentException)
        // We verify ResolveInstance doesn't throw by catching the ARM exception instead
        try
        {
            await service.ListApisAsync("dev");
        }
        catch (ArgumentException)
        {
            Assert.Fail("Should have resolved instance 'dev' matching 'Dev' case-insensitively");
        }
        catch
        {
            // Any other exception is fine — it means ResolveInstance succeeded
            // but the ARM call failed (expected in unit tests without real Azure)
        }
    }

    [Fact]
    public async Task ResolveInstance_ListsAvailableInstances_InErrorMessage()
    {
        var settings = new ApimSettings
        {
            Instances =
            [
                new() { Name = "dev", SubscriptionId = "s1", ResourceGroup = "r1", ServiceName = "sv1" },
                new() { Name = "staging", SubscriptionId = "s2", ResourceGroup = "r2", ServiceName = "sv2" },
                new() { Name = "production", SubscriptionId = "s3", ResourceGroup = "r3", ServiceName = "sv3" }
            ]
        };

        var service = CreateService(settings);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.ListApisAsync("unknown"));

        Assert.Contains("dev", ex.Message);
        Assert.Contains("staging", ex.Message);
        Assert.Contains("production", ex.Message);
    }
}
