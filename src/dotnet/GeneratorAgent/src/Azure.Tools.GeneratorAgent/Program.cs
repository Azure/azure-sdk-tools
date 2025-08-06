using System.CommandLine;
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Core;
using Azure.Identity;
using Azure.Tools.GeneratorAgent.Authentication;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Security;
using Azure.Tools.ErrorAnalyzers;
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

        private async Task<int> ExecuteGenerationAsync(string? typespecdir, string? commitId, string sdkdir, CancellationToken cancellationToken)
        {
            try
            {
                // Step 1: Create AppSettings
                ILogger<AppSettings> appSettingsLogger = LoggerFactory.CreateLogger<AppSettings>();
                AppSettings appSettings = ToolConfig.CreateAppSettings(appSettingsLogger);


                // Step 2: Create SDK Generation Service and ErrorFixer Agent
                ProcessExecutor processExecutor = new ProcessExecutor(LoggerFactory.CreateLogger<ProcessExecutor>());
                ISdkGenerationService sdkGenerationService = SdkGenerationServiceFactory.CreateSdkGenerationService(
                    typespecdir,
                    commitId,
                    sdkdir,
                    appSettings,
                    LoggerFactory,
                    processExecutor);

                ErrorFixerAgent agent = CreateErrorFixerAgent(appSettings);

                // Step 3: Initialize Agent Environment with TypeSpec files
                string threadId = await agent.InitializeAgentEnvironmentAsync(typespecdir!, cancellationToken).ConfigureAwait(false);

                // Step 4: Compile Typespec 
                await sdkGenerationService.CompileTypeSpecAsync(cancellationToken).ConfigureAwait(false);

                // Step 5: Compile Generated SDK
                BuildErrorAnalyzer errorAnalyzer = new BuildErrorAnalyzer(LoggerFactory.CreateLogger<BuildErrorAnalyzer>());
                SdkBuildService sdkBuildService = new SdkBuildService(
                    LoggerFactory.CreateLogger<SdkBuildService>(), 
                    processExecutor, 
                    errorAnalyzer, 
                    sdkdir);
                SdkBuildResult buildResult = await sdkBuildService.BuildSdkAsync(cancellationToken).ConfigureAwait(false);

                // Step 6: Parse build errors and get fixes
                List<RuleError> errors = errorAnalyzer.ParseBuildOutput(buildResult.Output);                
                if (errors.Count > 0)
                {
                    IEnumerable<Fix> fixes = errorAnalyzer.GetFixes(errors);
                }

                // Step 7: Ask Agent to Fix Errors
                await using (agent)
                {
                    await agent.FixCodeAsync(cancellationToken).ConfigureAwait(false);
                }
                
                return ExitCodeSuccess;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("SDK generation was cancelled");
                return ExitCodeSuccess;
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogError("Operation failed: {Error}", ex.Message);
                return ExitCodeFailure;
            }
            catch (ArgumentException ex)
            {
                Logger.LogError("Invalid configuration: {Error}", ex.Message);
                return ExitCodeFailure;
            }
            catch (RequestFailedException ex) when (ex.Status == 401)
            {
                Logger.LogError("Authentication failed for Azure AI service. Please check your credentials.");
                return ExitCodeFailure;
            }
            catch (RequestFailedException ex) when (ex.Status >= 500)
            {
                Logger.LogError("Azure AI service is temporarily unavailable: {Error}", ex.Message);
                return ExitCodeFailure;
            }
            catch (RequestFailedException ex)
            {
                Logger.LogError("Azure AI service error: {Error}", ex.Message);
                return ExitCodeFailure;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occurred during SDK generation");
                return ExitCodeFailure;
            }
        }

        private ErrorFixerAgent CreateErrorFixerAgent(AppSettings appSettings)
        {
            RuntimeEnvironment environment = DetermineRuntimeEnvironment();
            TokenCredentialOptions? credentialOptions = CreateCredentialOptions();

            CredentialFactory credentialFactory = new CredentialFactory(LoggerFactory.CreateLogger<CredentialFactory>());
            TokenCredential credential = credentialFactory.CreateCredential(environment, credentialOptions);

            PersistentAgentsClient client = new PersistentAgentsClient(
                appSettings.ProjectEndpoint,
                credential);

            return new ErrorFixerAgent(appSettings, LoggerFactory.CreateLogger<ErrorFixerAgent>(), client);
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
