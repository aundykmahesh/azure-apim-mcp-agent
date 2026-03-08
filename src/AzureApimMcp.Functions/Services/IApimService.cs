namespace AzureApimMcp.Functions.Services;

public record ApiSummary(
    string Name,
    string DisplayName,
    string? Path,
    string? Description);

public record ApiDetails(
    string Name,
    string DisplayName,
    string? Description,
    string? Path,
    string? ServiceUrl,
    IReadOnlyList<string> Protocols);

public record ApiOperationSummary(
    string Name,
    string Method,
    string UriTemplate,
    string? DisplayName,
    string? Description);

public record ApiCatalogEntry(
    string Name,
    string DisplayName,
    string? Path,
    string? Description,
    IReadOnlyList<ApiOperationSummary> Operations);

public interface IApimService
{
    IReadOnlyList<string> GetConfiguredInstances();

    Task<IReadOnlyList<ApiSummary>> ListApisAsync(
        string instanceName, CancellationToken ct = default);

    Task<IReadOnlyList<ApiSummary>> SearchApisAsync(
        string instanceName, string keyword, CancellationToken ct = default);

    Task<ApiDetails?> GetApiDetailsAsync(
        string instanceName, string apiNameOrTitle, CancellationToken ct = default);

    Task<string> DownloadApiSpecAsync(
        string instanceName, string apiNameOrTitle, CancellationToken ct = default);

    Task<IReadOnlyList<ApiOperationSummary>> ListApiOperationsAsync(
        string instanceName, string apiNameOrTitle, CancellationToken ct = default);

    Task<IReadOnlyList<ApiCatalogEntry>> GetApiCatalogAsync(
        string instanceName, CancellationToken ct = default);
}
