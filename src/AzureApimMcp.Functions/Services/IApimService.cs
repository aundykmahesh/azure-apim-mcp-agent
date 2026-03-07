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
}
