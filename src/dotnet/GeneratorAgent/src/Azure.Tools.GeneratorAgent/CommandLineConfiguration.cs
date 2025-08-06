using System.CommandLine;
using Microsoft.Extensions.Logging;
using Azure.Tools.GeneratorAgent.Security;
using Azure.Tools.GeneratorAgent.Configuration;

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
            RootCommand rootCommand = new("Azure SDK Generator Agent");

            Option<string?> typespecDirOption = new(
                new[] { "--typespec-dir", "-t" },
                "Path to the local TypeSpec project directory or TypeSpec specification directory (e.g., specification/testservice/TestService)")
            {
                IsRequired = true
            };

            Option<string?> commitIdOption = new(
                new[] { "--commit-id", "-c" },
                "GitHub commit ID to generate SDK from (optional, used with --typespec-dir for GitHub generation)");

            Option<string> sdkDirOption = new(
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

        internal int ValidateInput(string? typespecDir, string? commitId, string sdkDir)
        {
            Result<ValidationContext> result = ValidationContext.TryValidateAndCreate(typespecDir, commitId, sdkDir, Logger);
            
            if (result.IsFailure)
            {
                Logger.LogError("Input validation failed: {Error}", result.Error);
                return ExitCodeFailure;
            }
            
            return ExitCodeSuccess;
        } 
    }
}
