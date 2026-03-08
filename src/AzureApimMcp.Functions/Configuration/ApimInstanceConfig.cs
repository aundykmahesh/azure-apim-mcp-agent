namespace AzureApimMcp.Functions.Configuration;

public class ApimInstanceConfig
{
    public required string Name { get; set; }
    public required string SubscriptionId { get; set; }
    public required string ResourceGroup { get; set; }
    public required string ServiceName { get; set; }
}
