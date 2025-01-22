using AzureSdkQaBot;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Teams.AI;
using Microsoft.Teams.AI.AI;
using Microsoft.Teams.AI.AI.Clients;
using Microsoft.Teams.AI.AI.Models;
using Microsoft.Teams.AI.AI.Planners;
using Microsoft.Teams.AI.AI.Prompts;
using Microsoft.Teams.AI.State;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch;
using AzureSdkQaBot.Model;
using Octokit;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Identity.Client;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient("WebClient", client => client.Timeout = TimeSpan.FromSeconds(600));
builder.Services.AddHttpContextAccessor();

// Prepare Configuration for ConfigurationBotFrameworkAuthentication
ConfigOptions config = builder.Configuration.Get<ConfigOptions>()!;


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
//builder.Configuration["MicrosoftAppType"] = "MultiTenant";
//builder.Configuration["MicrosoftAppId"] = config.BOT_ID;
// MSAL certificate auth.
//builder.Services.AddSingleton(
  //  serviceProvider => ConfidentialClientApplicationBuilder.Create(config.BOT_ID)
    //    .WithCertificate(certificate, true)
      //  .Build()); 

// MSAL credential factory: regardless of secret, cert or custom auth, need to add the line below to enable MSAL.
//builder.Services.AddSingleton<ServiceClientCredentialsFactory, CertificateServiceClientCredentialsFactory>();

// Create the ClientCredentialsFactory to user certificate authentication
builder.Services.AddSingleton<ServiceClientCredentialsFactory>((e) => new CertificateServiceClientCredentialsFactory(certificate, config.BOT_ID, null, null, null, true));

// Create the Bot Framework Authentication to be used with the Bot Adapter.
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

// Create the Cloud Adapter with error handling enabled.
// Note: some classes expect a BotAdapter and some expect a BotFrameworkHttpAdapter, so
// register the same adapter instance for all types.
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
//builder.Services.AddSingleton<IBotFrameworkHttpAdapter>(sp => sp.GetService<TeamsAdapter>()!);
//builder.Services.AddSingleton<BotAdapter>(sp => sp.GetService<TeamsAdapter>()!);

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

// Create AI model
builder.Services.AddSingleton<OpenAIModel>(sp => new(
    new AzureOpenAIModelOptions(
        config.Azure.OpenAIApiKey,
        "gpt-35-turbo",
        config.Azure.OpenAIEndpoint
    )
    {
        LogRequests = true
    },
    sp.GetService<ILoggerFactory>()
));

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

    // Create Prompt Manager
    PromptManager promptManager = new(new()
    {
        PromptFolder = "./Prompts"
    });

    // Adds functions to be referenced in the prompt template
    promptManager.AddFunction("getCitations", (context, memory, functions, tokenizer, args) =>
    {
        IEnumerable<string> citations = ((IList<DocumentCitation>)memory.GetValue("conversation." + Constants.AppState_Conversation_CitationKey)!).Select((citation, index) =>
        {
            return $"<Citation {index + 1}>{Environment.NewLine}Source:{Environment.NewLine}{citation.Source}{Environment.NewLine}Content:{Environment.NewLine}{citation.Content}";
        });
        string citationStrings = string.Join(Environment.NewLine + Environment.NewLine, citations);
        return Task.FromResult<dynamic>(citationStrings);
    });
    promptManager.AddFunction("getInput", (context, memory, functions, tokenizer, args) =>
    {
        string input = GitHubPrActions.GetUserQueryFromContext(context);
        return Task.FromResult<dynamic>(input);
    });

    LLMClient<string> llmClient = new(
        new(sp.GetService<OpenAIModel>()!, promptManager.GetPrompt("QA")),
        loggerFactory
    );

    // Create OpenAIPlanner
    ActionPlanner<AppState> planner = new(
        new(
            sp.GetService<OpenAIModel>()!,
            promptManager,
            async (context, state, planner) =>
            {
                return await Task.FromResult(promptManager.GetPrompt("Planner"));
            }
        ),
        loggerFactory
    );

    ApplicationOptions<AppState> applicationOptions = new()
    {
        AI = new AIOptions<AppState>(planner),
        Storage = sp.GetService<IStorage>(),
        StartTypingTimer = true
    };

    return new AzureSdkQaBotApplication(applicationOptions, llmClient, promptManager, semanticKernel, githubClient, loggerFactory.CreateLogger("SdkQaBot"));
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
