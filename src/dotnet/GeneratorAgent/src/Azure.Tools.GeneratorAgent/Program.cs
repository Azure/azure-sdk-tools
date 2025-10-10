using System.CommandLine;
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

            // Get services from DI container
            var appSettings = ServiceProvider.GetRequiredService<AppSettings>();
            var typeSpecFileService = ServiceProvider.GetRequiredService<TypeSpecFileService>();
            var toolBasedAgent = ServiceProvider.GetRequiredService<ToolBasedAgent>();
            var libraryBuildService = ServiceProvider.GetRequiredService<LibraryBuildService>();            
            var sdkGenerationService = ServiceProvider.GetRequiredService<LocalLibraryGenerationService>();

            // Download TypeSpec files if from GitHub (commitId provided)
            if (!string.IsNullOrWhiteSpace(validationContext.ValidatedCommitId))
            { 
                await typeSpecFileService.DownloadGitHubTypeSpecFilesAsync(validationContext, cancellationToken).ConfigureAwait(false);
                Logger.LogInformation("GitHub TypeSpec files downloaded successfully");
            }

            // Initialize Tool-Based Agent with proper disposal   
            await using (toolBasedAgent.ConfigureAwait(false))
            {
                // Set the validation context for tool execution
                toolBasedAgent.SetValidationContext(validationContext);
                
                await toolBasedAgent.InitializeAsync(cancellationToken).ConfigureAwait(false);          

                // Setup local machine for iterative generation
                await sdkGenerationService.InstallTypeSpecDependencies(cancellationToken).ConfigureAwait(false);
            
                // Iteratively: Compile -> generate library -> build library -> capture and fix error
                var currentIteration = 0;
                var maxIterations = appSettings.MaxIterations;

                while (currentIteration < maxIterations)
                {
                    // Compile TypeSpec              
                    var compileResult = await sdkGenerationService.CompileTypeSpecAsync(validationContext, cancellationToken).ConfigureAwait(false);

                    // Compile Generated SDK (only if TypeSpec compilation succeeded)
                    Result<object>? buildResult = null;
                    if (compileResult.IsSuccess)
                    {
                        Logger.LogInformation("TypeSpec compilation completed successfully. Building Library...\n");

                        buildResult = await libraryBuildService.BuildSdkAsync(validationContext.ValidatedSdkDir, cancellationToken).ConfigureAwait(false);
                    }

                    // Check if compile and build are successful
                    if (compileResult.IsSuccess && (buildResult?.IsSuccess ?? true))
                    {
                        Logger.LogInformation("Generation process completed successfully - no errors found");
                        break;
                    }

                    // Analyze errors and get fixes - use the already initialized agent
                    var errorAnalyzer = ServiceProvider.GetRequiredService<ErrorAnalysisService>();
                    var analysisResult = await errorAnalyzer.GenerateFixesFromResultsAsync(compileResult, buildResult, cancellationToken).ConfigureAwait(false);
                    if (analysisResult.IsFailure)
                    {
                        Logger.LogError("Failed to analyze errors: {Error}", analysisResult.Exception?.Message);
                        analysisResult.ThrowIfFailure();
                    }

                    var allFixes = analysisResult.Value!;
                    Logger.LogInformation("Error analysis completed successfully - generated {FixCount} fixes\n", allFixes.Count);

                    // No point continuing if no fixes were generated
                    if (allFixes.Count == 0)
                    {
                        Logger.LogInformation("No fixes generated - compilation errors may not be addressable by this agent");
                        break;
                    }

                    // Get patch proposal from agent
                    Logger.LogInformation("Processing fixes with Agent");
                    var patchResult = await toolBasedAgent.FixCodeAsync(allFixes, cancellationToken).ConfigureAwait(false);
                    
                    if (!patchResult.IsSuccess)
                    {
                        Logger.LogError("Failed to get patch from agent: {Error}", 
                            patchResult.ProcessException?.Message ?? patchResult.Exception?.Message ?? "Unknown error");
                        break;
                    }
                    
                    Logger.LogInformation("The patch result is {patchResult}", patchResult.Value);

                    // Apply patch and continue iteration
                    var patchApplicator = ServiceProvider.GetRequiredService<TypeSpecPatchApplicator>();
                    bool success = await patchApplicator.ApplyPatchAsync(patchResult.Value!, validationContext.ValidatedTypeSpecDir, cancellationToken);

                    if (success) {
                        Logger.LogInformation("Patch applied, proceeding to next iteration");
                        // Continue to next compile/build cycle
                    } else {
                        Logger.LogError("Patch failed, stopping iterations");
                        break;
                    }
                    
                    currentIteration += 1;
                }
            }

            Logger.LogInformation("Library generation completed successfully");
            return ExitCodeSuccess;
        }
    } 
}


