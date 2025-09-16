using System.CommandLine;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Agent;
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
            var toolConfig = new ToolConfiguration();
            using var loggerFactory = toolConfig.CreateLoggerFactory();

            var services = new ServiceCollection();
            services.AddSingleton(loggerFactory);
            services.AddLogging();
            services.AddGeneratorAgentServices(toolConfig);

            await using var serviceProvider = services.BuildServiceProvider();

            var program = new Program(serviceProvider);
            return await program.RunAsync(args).ConfigureAwait(false);
        }

        internal async Task<int> RunAsync(string[] args)
        {
            var rootCommand = CommandLineConfig.CreateRootCommand(HandleCommandAsync);
            return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
        }

        internal async Task<int> HandleCommandAsync(string? typespecPath, string? commitId, string sdkPath)
        {
            var validationResult = CommandLineConfig.ValidateInput(typespecPath, commitId, sdkPath);
            if (validationResult != ExitCodeSuccess)
            {
                return validationResult;
            }

            var validationContext = ValidationContext.CreateFromValidatedInputs(
                typespecPath!, commitId!, sdkPath);

            using var cancellationTokenSource = new CancellationTokenSource();
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
                Logger.LogInformation("Operation was cancelled");
                return ExitCodeSuccess;
            }
            catch (ArgumentException ex)
            {
                Logger.LogError("Invalid configuration: {Error}", ex.Message);
                return ExitCodeFailure;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError("GitHub API request failed: {Error}", ex.Message);
                return ExitCodeFailure;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("GitHub") || ex.Message.Contains("deserialize"))
            {
                Logger.LogError("GitHub API response error: {Error}", ex.Message);
                return ExitCodeFailure;
            }
            catch (UnauthorizedAccessException)
            {
                Logger.LogError("Authentication failed for Azure AI service");
                return ExitCodeFailure;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status >= 400 && ex.Status < 500)
            {
                Logger.LogError("Azure AI service client error ({StatusCode}): {Error}", ex.Status, ex.Message);
                return ExitCodeFailure;
            }
            catch (Azure.RequestFailedException ex)
            {
                Logger.LogError("Azure AI service error: {Error}", ex.Message);
                return ExitCodeFailure;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error occurred during SDK generation");
                return ExitCodeFailure;
            }
        }

        private async Task<int> ExecuteGenerationAsync(ValidationContext validationContext, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Starting library generation process");

            var appSettings = ServiceProvider.GetRequiredService<AppSettings>();
            var maxIterations = appSettings.MaxIterations;

            // Step 1: Get TypeSpec files (from local or GitHub)
            var fileServiceFactory = ServiceProvider.GetRequiredService<Func<ValidationContext, TypeSpecFileService>>();
            var fileService = fileServiceFactory(validationContext);

            var typeSpecFilesResult = await fileService.GetTypeSpecFilesAsync(cancellationToken).ConfigureAwait(false);
            if (typeSpecFilesResult.IsFailure)
            {
                typeSpecFilesResult.ThrowIfFailure();
            }

            var typeSpecFiles = typeSpecFilesResult.Value!;   

            Logger.LogInformation("Successfully loaded {Count} TypeSpec files", typeSpecFiles.Count);

            // Step 2: Initialize ErrorFixer Agent with files in memory
            var errorFixerAgent = ServiceProvider.GetRequiredService<ErrorFixerAgent>();
            await errorFixerAgent.InitializeAgentEnvironmentAsync(typeSpecFiles, cancellationToken).ConfigureAwait(false);
            Logger.LogInformation("Agent environment initialized successfully");

            //Iteratively: Compile -> generate library -> build library -> capture and fix error
            var currentIteration = 0;

            while (currentIteration < maxIterations)
            {
                // Step 4: Compile Typespec 
                var sdkServiceFactory = ServiceProvider.GetRequiredService<Func<ValidationContext, LocalLibraryGenerationService>>();
                var sdkGenerationService = sdkServiceFactory(validationContext);
                var compileResult = await sdkGenerationService.CompileTypeSpecAsync(cancellationToken).ConfigureAwait(false);
                
                // Step 5: Compile Generated SDK (only if TypeSpec compilation succeeded)
                Result<object>? buildResult = null;
                if (compileResult.IsSuccess)
                {
                    Logger.LogInformation("Step 4: TypeSpec compilation completed successfully. Building Library...");

                    var libraryBuildServiceFactory = ServiceProvider.GetRequiredService<Func<ValidationContext, LibraryBuildService>>();
                    var libraryBuildService = libraryBuildServiceFactory(validationContext);
                    buildResult = await libraryBuildService.BuildSdkAsync(cancellationToken).ConfigureAwait(false);
                    
                    if (buildResult.IsSuccess)
                    {
                        Logger.LogInformation("Build completed successfully");
                    }
                }

                // Step 6: Check if compile and build are successful
                if (compileResult.IsSuccess && (buildResult?.IsSuccess ?? true))
                {
                    Logger.LogInformation("Generation process completed successfully - no errors found");
                    break;
                }

                // Step 7: Analyze errors and get fixes
                var analyzer = ServiceProvider.GetRequiredService<FixGeneratorService>();
                var analysisResult = await analyzer.AnalyzeAndGetFixesAsync(compileResult, buildResult, cancellationToken).ConfigureAwait(false);
                if (analysisResult.IsFailure)
                {
                    Logger.LogError("Failed to analyze errors: {Error}", analysisResult.Exception?.Message);
                    analysisResult.ThrowIfFailure();
                }

                var allFixes = analysisResult.Value!;
                Logger.LogInformation("Error analysis completed successfully - generated {FixCount} fixes", allFixes.Count);

                //No point continuing if no fixes were generated
                if (allFixes.Count == 0)
                {
                    Logger.LogInformation("No fixes generated - compilation errors may not be addressable by this agent");
                    break;
                }

                // Step 8: Apply fixes - handle Result<T>
                var fixResult = await errorFixerAgent.FixCodeAsync(allFixes, cancellationToken).ConfigureAwait(false);
                if (fixResult.IsFailure)
                {
                    Logger.LogError("Agent failed to fix code: {Error}", fixResult.Exception?.Message);
                    fixResult.ThrowIfFailure();
                }
                
                var updatedClientTspContent = fixResult.Value!;
                if (string.IsNullOrWhiteSpace(updatedClientTspContent))
                {
                    throw new InvalidOperationException("Agent returned empty content for client.tsp");
                }

                Logger.LogInformation("Agent successfully generated fixes for {FixCount} issues", allFixes.Count);

                // Step 9: Update client.tsp files
                var updateResult = await fileService.UpdateTypeSpecFileAsync("client.tsp", updatedClientTspContent, cancellationToken).ConfigureAwait(false);
                if (updateResult.IsFailure)
                {
                    Logger.LogError("Failed to update TypeSpec file: {Error}", updateResult.Exception?.Message);
                    updateResult.ThrowIfFailure();
                }

                await errorFixerAgent.UpdateFileAsync("client.tsp", updatedClientTspContent, cancellationToken);
                
                Logger.LogInformation("Successfully updated client.tsp with generated fixes");

                currentIteration++;
            }
                
            Logger.LogInformation("Library generation completed successfully");
            return ExitCodeSuccess;
        }
    } 
}


