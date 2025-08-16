using System.CommandLine;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    public class Program
    {
        private const int ExitCodeSuccess = 0;
        private const int ExitCodeFailure = 1;

        private readonly IServiceProvider ServiceProvider;
        private readonly ILogger<Program> Logger;
        private readonly CommandLineConfiguration CommandLineConfig;

        internal Program(IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            
            ServiceProvider = serviceProvider;
            Logger = ServiceProvider.GetRequiredService<ILogger<Program>>();
            CommandLineConfig = new CommandLineConfiguration(ServiceProvider.GetRequiredService<ILogger<CommandLineConfiguration>>());
        }

        public static async Task<int> Main(string[] args)
        {
            ToolConfiguration toolConfig = new ToolConfiguration();
            using ILoggerFactory loggerFactory = toolConfig.CreateLoggerFactory();

            var services = new ServiceCollection();
            services.AddSingleton(loggerFactory);
            services.AddLogging();
            services.AddGeneratorAgentServices(toolConfig);

            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            
            Program program = new Program(serviceProvider);
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

            ValidationContext validationContext = ValidationContext.CreateFromValidatedInputs(
                typespecPath!, commitId!, sdkPath);

            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                Logger.LogInformation("Cancellation requested by user");
                cancellationTokenSource.Cancel();
                eventArgs.Cancel = true;
            };

            try
            {
                return await ExecuteGenerationAsync(validationContext, cancellationTokenSource.Token).ConfigureAwait(false);
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

        private async Task<int> ExecuteGenerationAsync(ValidationContext validationContext, CancellationToken cancellationToken)
        {
            try
            {
                // Step 1: Get services from DI container
                AppSettings appSettings = ServiceProvider.GetRequiredService<AppSettings>();
                
                // Step 2: Get TypeSpec files (from local or GitHub)
                var fileServiceFactory = ServiceProvider.GetRequiredService<Func<ValidationContext, TypeSpecFileService>>();
                TypeSpecFileService fileService = fileServiceFactory(validationContext);

                Dictionary<string, string> typeSpecFiles = await fileService.GetTypeSpecFilesAsync(cancellationToken);

                // Step 3: Initialize ErrorFixer Agent with files in memory (singleton)
                ErrorFixerAgent agent = ServiceProvider.GetRequiredService<ErrorFixerAgent>();
                string threadId = await agent.InitializeAgentEnvironmentAsync(typeSpecFiles, cancellationToken).ConfigureAwait(false);

                // Step 4: Compile Typespec 
                var sdkServiceFactory = ServiceProvider.GetRequiredService<Func<ValidationContext, ISdkGenerationService>>();
                ISdkGenerationService sdkGenerationService = sdkServiceFactory(validationContext);
                Result<object> compileResult = await sdkGenerationService.CompileTypeSpecAsync(cancellationToken).ConfigureAwait(false);

                // Step 5: Compile Generated SDK
                var sdkBuildServiceFactory = ServiceProvider.GetRequiredService<Func<ValidationContext, SdkBuildService>>();
                SdkBuildService sdkBuildService = sdkBuildServiceFactory(validationContext);
                Result<object> buildResult = await sdkBuildService.BuildSdkAsync(cancellationToken).ConfigureAwait(false);

                // Step 6: Analyze all errors and get fixes (singleton)
                BuildErrorAnalyzer analyzer = ServiceProvider.GetRequiredService<BuildErrorAnalyzer>();
                List<Fix> allFixes = analyzer.AnalyzeAndGetFixes(compileResult, buildResult);

                // Step 7:                     
                // TODO: Send fixes to ErrorFixerAgent if List<Fix> is not empty
                // await agent.ProcessFixesAsync(allFixes, threadId, cancellationToken);

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
    }
}
