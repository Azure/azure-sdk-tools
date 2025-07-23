using System.CommandLine;
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Core;
using Azure.Identity;
using Azure.Tools.GeneratorAgent.Authentication;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    public class Program
    {
        private const int ExitCodeSuccess = 0;
        private const int ExitCodeFailure = 1;

        private readonly ToolConfiguration ToolConfig;
        private readonly ILoggerFactory LoggerFactory;
        private readonly ILogger<Program> Logger;
        private readonly CommandLineConfiguration CommandLineConfig;

        internal Program(ToolConfiguration toolConfig, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(toolConfig);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            
            ToolConfig = toolConfig;
            LoggerFactory = loggerFactory;
            Logger = LoggerFactory.CreateLogger<Program>();
            CommandLineConfig = new CommandLineConfiguration(LoggerFactory.CreateLogger<CommandLineConfiguration>());
            
            InputValidator.SetLogger(LoggerFactory.CreateLogger("Azure.Tools.GeneratorAgent.Security.InputValidator"));
        }

        public static async Task<int> Main(string[] args)
        {
            ToolConfiguration toolConfig = new ToolConfiguration();
            using ILoggerFactory loggerFactory = toolConfig.CreateLoggerFactory();

            Program program = new Program(toolConfig, loggerFactory);
            return await program.RunAsync(args).ConfigureAwait(false);
        }

        internal async Task<int> RunAsync(string[] args)
        {
            RootCommand rootCommand = CommandLineConfig.CreateRootCommand(HandleCommandAsync);
            return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
        }

        internal async Task<int> HandleCommandAsync(string? typespecPath, string? commitId, string sdkPath)
        {
            int validationResult = CommandLineConfig.ValidateInput(typespecPath, commitId, sdkPath);
            if (validationResult != ExitCodeSuccess)
            {
                return validationResult;
            }

            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                Logger.LogInformation("Cancellation requested by user");
                cancellationTokenSource.Cancel();
                eventArgs.Cancel = true;
            };

            try
            {
                return await ExecuteGenerationAsync(typespecPath, commitId, sdkPath, cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("Operation was cancelled. Shutting down gracefully");
                return ExitCodeSuccess;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error occurred during command execution");
                return ExitCodeFailure;
            }
        }

        private async Task<int> ExecuteGenerationAsync(
            string? typespecdir,
            string? commitId,
            string sdkdir,
            CancellationToken cancellationToken)
        {
            try
            {
                Logger.LogInformation("Starting SDK generation process");

                AppSettings appSettings;
                try
                {
                    var appSettingsLogger = LoggerFactory.CreateLogger<AppSettings>();
                    appSettings = ToolConfig.CreateAppSettings(appSettingsLogger);
                }
                catch (InvalidOperationException ex)
                {
                    Logger.LogError("Configuration validation failed: {Error}", ex.Message);
                    Logger.LogError("Please check your configuration settings and try again.");
                    return ExitCodeFailure;
                }

                RuntimeEnvironment environment = DetermineRuntimeEnvironment();
                TokenCredentialOptions? credentialOptions = CreateCredentialOptions();

                CredentialFactory credentialFactory = new(LoggerFactory.CreateLogger<CredentialFactory>());
                TokenCredential credential = credentialFactory.CreateCredential(environment, credentialOptions);

                ProcessExecutor processExecutor = new(LoggerFactory.CreateLogger<ProcessExecutor>());

                PersistentAgentsAdministrationClient adminClient = new(
                    new Uri(appSettings.ProjectEndpoint),
                    credential);

                ISdkGenerationService sdkGenerationService;
                try
                {
                    if (string.IsNullOrWhiteSpace(commitId))
                    {
                        Logger.LogInformation("Using local TypeSpec SDK generation service");
                        sdkGenerationService = SdkGenerationServiceFactory.CreateForLocalPath(typespecdir!, sdkdir, appSettings, LoggerFactory, processExecutor);
                    }
                    else
                    {
                        Logger.LogInformation("Using GitHub TypeSpec SDK generation service");
                        sdkGenerationService = SdkGenerationServiceFactory.CreateForGitHubCommit(commitId, typespecdir!, sdkdir, appSettings, LoggerFactory, processExecutor);
                    }
                }
                catch (ArgumentException ex)
                {
                    Logger.LogError("Invalid configuration for SDK generation: {Error}", ex.Message);
                    Logger.LogError("Please verify your input parameters and try again.");
                    return ExitCodeFailure;
                }
                catch (InvalidOperationException ex)
                {
                    Logger.LogError("Configuration validation failed: {Error}", ex.Message);
                    Logger.LogError("Please check your configuration settings and try again.");
                    return ExitCodeFailure;
                }

                Logger.LogInformation("Initializing error fixing agent");
                await using ErrorFixerAgent agent = new(
                    appSettings,
                    LoggerFactory.CreateLogger<ErrorFixerAgent>(),
                    adminClient);

                try
                {
                    await agent.FixCodeAsync(cancellationToken).ConfigureAwait(false);
                    Logger.LogInformation("Error fixing agent completed successfully");
                }
                catch (InvalidOperationException ex)
                {
                    Logger.LogError("Error fixing agent failed to initialize: {Error}", ex.Message);
                    Logger.LogError("Please check your Azure AI service configuration and try again.");
                    return ExitCodeFailure;
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 401)
                {
                    Logger.LogError("Authentication failed for Azure AI service. Please check your credentials.");
                    return ExitCodeFailure;
                }
                catch (Azure.RequestFailedException ex) when (ex.Status >= 500)
                {
                    Logger.LogError("Azure AI service is temporarily unavailable: {Error}", ex.Message);
                    Logger.LogError("Please try again later.");
                    return ExitCodeFailure;
                }
                catch (Azure.RequestFailedException ex)
                {
                    Logger.LogError("Azure AI service error: {Error}", ex.Message);
                    return ExitCodeFailure;
                }

                Logger.LogInformation("Starting TypeSpec compilation");
                try
                {
                    await sdkGenerationService.CompileTypeSpecAsync(cancellationToken).ConfigureAwait(false);
                    Logger.LogInformation("SDK generation completed successfully");
                    return ExitCodeSuccess;
                }
                catch (InvalidOperationException ex)
                {
                    Logger.LogError("SDK generation failed: {Error}", ex.Message);
                    return ExitCodeFailure;
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("SDK generation was cancelled");
                return ExitCodeSuccess;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occurred during SDK generation");
                return ExitCodeFailure;
            }
        }

        private static RuntimeEnvironment DetermineRuntimeEnvironment()
        {
            bool isGitHubActions = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariables.GitHubActions)) ||
                                 !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariables.GitHubWorkflow));

            if (isGitHubActions)
            {
                return RuntimeEnvironment.DevOpsPipeline;
            }

            return RuntimeEnvironment.LocalDevelopment;
        }

        private static TokenCredentialOptions? CreateCredentialOptions()
        {
            string? tenantId = Environment.GetEnvironmentVariable(EnvironmentVariables.AzureTenantId);
            Uri? authorityHost = null;

            string? authority = Environment.GetEnvironmentVariable(EnvironmentVariables.AzureAuthorityHost);
            if (!string.IsNullOrEmpty(authority) && Uri.TryCreate(authority, UriKind.Absolute, out Uri? parsedAuthority))
            {
                authorityHost = parsedAuthority;
            }

            if (tenantId == null && authorityHost == null)
            {
                return null;
            }

            TokenCredentialOptions options = new TokenCredentialOptions();

            if (authorityHost != null)
            {
                options.AuthorityHost = authorityHost;
            }

            return options;
        }
    }
}
