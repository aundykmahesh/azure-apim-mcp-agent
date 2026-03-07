using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using AzureApimMcp.Functions.Configuration;
using AzureApimMcp.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // Configuration
        services.Configure<ApimSettings>(
            context.Configuration.GetSection(ApimSettings.SectionName));

        // Azure SDK
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        services.AddSingleton(sp => new ArmClient(sp.GetRequiredService<TokenCredential>()));

        // HTTP client
        services.AddHttpClient();

        // Application services
        services.AddSingleton<IApimService, ApimService>();

        // Azure OpenAI chat client (for /chat endpoint)
        var aiEndpoint = context.Configuration["AzureOpenAI:Endpoint"];
        var aiDeployment = context.Configuration["AzureOpenAI:DeploymentName"];
        if (!string.IsNullOrEmpty(aiEndpoint) && !string.IsNullOrEmpty(aiDeployment))
        {
            services.AddSingleton<IChatClient>(sp =>
            {
                var credential = sp.GetRequiredService<TokenCredential>();
                var azureClient = new AzureOpenAIClient(new Uri(aiEndpoint), credential);
                return azureClient
                    .GetChatClient(aiDeployment)
                    .AsIChatClient()
                    .AsBuilder()
                    .UseFunctionInvocation()
                    .Build();
            });
        }
    })
    .Build();

host.Run();
