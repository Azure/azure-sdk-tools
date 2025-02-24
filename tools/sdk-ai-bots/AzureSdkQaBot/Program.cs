using AzureSdkQaBot;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.TeamsAI;
using Microsoft.TeamsAI.AI.Planner;
using Microsoft.TeamsAI.AI.Prompt;
using Microsoft.TeamsAI.AI;
using Microsoft.TeamsAI.AI.Moderator;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch;
using AzureSdkQaBot.Model;
using Octokit;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient("WebClient", client => client.Timeout = TimeSpan.FromSeconds(600));
builder.Services.AddHttpContextAccessor();

// Prepare Configuration for ConfigurationBotFrameworkAuthentication
var config = builder.Configuration.Get<ConfigOptions>()!;


// Access key vault
if (string.IsNullOrEmpty(config.KeyVaultUrl))
{
    throw new Exception("KeyVaultUrl is not set in the configuration.");
}

System.Security.Cryptography.X509Certificates.X509Certificate2? certificate = null;
try
{
    CertificateClient client = new(vaultUri: new Uri(config.KeyVaultUrl), credential: new DefaultAzureCredential());
    if (client == null)
    {
        throw new Exception($"Failed to create KeyVault client for {config.KeyVaultUrl}");
    }


    //Get certificate in X509Certificate format
    if (string.IsNullOrEmpty(config.CertificateName))
    {
        throw new Exception("CertificateName is not set in the configuration.");
    }
    string certificateName = config.CertificateName;
    certificate = client.DownloadCertificate(certificateName).Value;

    if (certificate == null)
    {
        throw new Exception($"Certificate {certificateName} not found in KeyVault {config.KeyVaultUrl}");
    }
}
catch (Exception ex)
{
    throw new Exception($"Failed to get certificate {config.CertificateName} from KeyVault {config.KeyVaultUrl}", ex);
}

// Create the ClientCredentialsFactory to user certificate authentication
builder.Services.AddSingleton<ServiceClientCredentialsFactory>((e) => new CertificateServiceClientCredentialsFactory(certificate, config.BOT_ID, "72f988bf-86f1-41af-91ab-2d7cd011db47"));

// Create the Bot Framework Authentication to be used with the Bot Adapter.
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

// Create the Cloud Adapter with error handling enabled.
// Note: some classes expect a BotAdapter and some expect a BotFrameworkHttpAdapter, so
// register the same adapter instance for all types.
builder.Services.AddSingleton<CloudAdapter, AdapterWithErrorHandler>();
builder.Services.AddSingleton<IBotFrameworkHttpAdapter>(sp => sp.GetService<CloudAdapter>()!);
builder.Services.AddSingleton<BotAdapter>(sp => sp.GetService<CloudAdapter>()!);

builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add OpenTelemetry and configure it to use Azure Monitor.
builder.Services.AddOpenTelemetry().UseAzureMonitor();

#region Use Azure OpenAI and Azure Content Safety

if (config.Azure == null
    || string.IsNullOrEmpty(config.Azure.OpenAIApiKey) 
    || string.IsNullOrEmpty(config.Azure.OpenAIEndpoint)
    || string.IsNullOrEmpty(config.Azure.ContentSafetyApiKey)
    || string.IsNullOrEmpty(config.Azure.ContentSafetyEndpoint))
{
    throw new Exception("Missing Azure configuration.");
}

if (config.Search == null
    || string.IsNullOrEmpty(config.Search.SearchServiceApiKey)
    || string.IsNullOrEmpty(config.Search.SearchServiceUrl))
{
    throw new Exception("Missing Cognitive Search configuration.");
}

builder.Services.AddSingleton(_ => new AzureOpenAIPlannerOptions(config.Azure.OpenAIApiKey, "text-davinci-003", config.Azure.OpenAIEndpoint));
//builder.Services.AddSingleton(_ => new AzureContentSafetyModeratorOptions(config.Azure.ContentSafetyApiKey, config.Azure.ContentSafetyEndpoint, ModerationType.Both));

// build semantic kernel
var kernelBuilder = new KernelBuilder();
kernelBuilder.WithAzureTextEmbeddingGenerationService(deploymentName: config.Azure.EmbeddingModelDeploymentName, endpoint: config.Azure.OpenAIEndpoint, apiKey: config.Azure.OpenAIApiKey);
kernelBuilder.WithAzureChatCompletionService(config.Azure.ChatModelDeploymentName, config.Azure.OpenAIEndpoint, config.Azure.OpenAIApiKey);
kernelBuilder.WithMemoryStorage(new AzureCognitiveSearchMemoryStore(config.Search.SearchServiceUrl, config.Search.SearchServiceApiKey));
var semanticKernel = kernelBuilder.Build();
builder.Services.AddSingleton(semanticKernel);

// Build github client
var githubClient = new GitHubClient(new ProductHeaderValue("AzureSdkQaBot"))
{
    Credentials = new Credentials(config.GITHUB_TOKEN)
};
builder.Services.AddSingleton(githubClient);

// Create the Application.
builder.Services.AddTransient<IBot, AzureSdkQaBotApplication>(sp =>
{
    ILoggerFactory loggerFactory = sp.GetService<ILoggerFactory>()!;

    IPromptManager<AppState> promptManager = new PromptManager<AppState>("./Prompts");

    IPlanner<AppState> planner = new AzureOpenAIPlanner<AppState>(sp.GetService<AzureOpenAIPlannerOptions>(), loggerFactory.CreateLogger<AzureOpenAIPlanner<AppState>>());
    //IModerator<AppState> moderator = new AzureContentSafetyModerator<AppState>(sp.GetService<AzureContentSafetyModeratorOptions>(), loggerFactory.CreateLogger<AzureContentSafetyModerator<AppState>>());

    ApplicationOptions<AppState, AppStateManager> applicationOptions = new()
    {
        AI = new AIOptions<AppState>(planner, promptManager)
        {
            //Moderator = moderator,
            Prompt = "Planner",
            History = new AIHistoryOptions()
            {
                TrackHistory = true,
                AssistantHistoryType = AssistantHistoryType.Text,
            },
        },
        Storage = sp.GetService<IStorage>(),
        StartTypingTimer = true
    };

    return new AzureSdkQaBotApplication(applicationOptions, semanticKernel, githubClient, loggerFactory.CreateLogger("SdkQaBot"));
});

#endregion

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

app.Run();
