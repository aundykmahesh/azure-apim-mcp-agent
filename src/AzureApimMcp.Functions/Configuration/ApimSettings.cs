namespace AzureApimMcp.Functions.Configuration;

public class ApimSettings
{
    public const string SectionName = "Apim";

    public List<ApimInstanceConfig> Instances { get; set; } = [];
}
