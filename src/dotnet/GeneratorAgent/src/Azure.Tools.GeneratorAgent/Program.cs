using System.CommandLine;
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
            CommandLineConfig = new CommandLineConfiguration();
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
            var validationContext = ValidationContext.ValidateAndCreate(typespecPath, commitId, sdkPath);

            using var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                Logger.LogInformation("Cancellation requested by user");
                cancellationTokenSource.Cancel();
                eventArgs.Cancel = true;
            };

            try
            {
                var orchestrator = ServiceProvider.GetRequiredService<GenerationOrchestrator>();
                await orchestrator.ExecuteAsync(validationContext, cancellationTokenSource.Token).ConfigureAwait(false);
                return ExitCodeSuccess;
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
    } 
}


