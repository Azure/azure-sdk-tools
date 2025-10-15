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
    }
}
