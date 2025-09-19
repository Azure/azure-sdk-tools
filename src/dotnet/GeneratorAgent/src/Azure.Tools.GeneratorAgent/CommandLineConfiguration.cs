using System.CommandLine;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Configures the command line interface for the Azure SDK Generator Agent with comprehensive security validation.
    /// </summary>
    internal sealed class CommandLineConfiguration
    {
        private const int ExitCodeSuccess = 0;
        private const int ExitCodeFailure = 1;    
        private readonly ILogger<CommandLineConfiguration> Logger;

        public CommandLineConfiguration(ILogger<CommandLineConfiguration> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            Logger = logger;
        }

        /// <summary>
        /// Creates and configures the root command for the application.
        /// </summary>
        /// <param name="handler">The handler function to execute when the command is invoked.</param>
        /// <returns>The configured root command.</returns>
        public RootCommand CreateRootCommand(Func<string?, string?, string, Task<int>> handler)
        {
            var rootCommand = new RootCommand("Azure SDK Generator Agent");

            var typespecDirOption = new Option<string?>(
                new[] { "--typespec-dir", "-t" },
                "Path to the local TypeSpec project directory or TypeSpec specification directory (e.g., specification/testservice/TestService)")
            {
                IsRequired = true
            };

            var commitIdOption = new Option<string?>(
                new[] { "--commit-id", "-c" },
                "GitHub commit ID to generate SDK from (optional, used with --typespec-dir for GitHub generation)");

            var sdkDirOption = new Option<string>(
                new[] { "--output-dir", "-o" },
                "Output directory for generated SDK files")
            {
                IsRequired = true
            };

            rootCommand.AddOption(typespecDirOption);
            rootCommand.AddOption(commitIdOption);
            rootCommand.AddOption(sdkDirOption);

            rootCommand.SetHandler(handler,
                typespecDirOption,
                commitIdOption,
                sdkDirOption);

            return rootCommand;
        }

        /// <summary>
        /// Validates input and logs errors. Returns 0 for success, 1 for failure.
        /// Single responsibility - validate and log once at boundary.
        /// </summary>
        internal int ValidateInput(string? typespecDir, string? commitId, string sdkDir)
        {
            var result = ValidationContext.TryValidateAndCreate(typespecDir, commitId, sdkDir);
            
            if (result.IsFailure)
            {
                Logger.LogError("Input validation failed: {Error}", result.Exception?.Message ?? "Unknown validation error");
                return ExitCodeFailure;
            }
            
            Logger.LogDebug("Input validation completed successfully");
            return ExitCodeSuccess;
        } 
    }
}
