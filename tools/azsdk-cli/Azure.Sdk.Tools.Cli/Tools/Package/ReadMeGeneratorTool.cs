// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    // Will add after we decide which tools are exported via the MCP.
    //[McpServerToolType, Description("Generates a README file, using service documentation")]
    public class ReadMeGeneratorTool(
        ILogger<ReadMeGeneratorTool> logger,
        IMicroagentHostService microAgentHostService
    ) : MCPTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Generators];

        private readonly Option<string> packagePathOption = new("--package-path")
        {
            Description = "Path to a module, underneath the 'sdk' folder for your repository (ex: /yoursource/azure-sdk-for-go/sdk/messaging/azservicebus)",
            Required = true,
            DefaultValueFactory = _ => ".",
        };

        private readonly Option<string> outputPathOption = new("--output-path")
        {
            Description = "Path to write the generated README contents",
            Required = true,
            DefaultValueFactory = _ => "README.output.md",
        };

        private readonly Option<string> templatePathOption = new("--template-path")
        {
            Description = "Path to the README template file (ie: Templates/ReadMeGenerator/README-template.go.md)",
            Required = true,
        };

        private readonly Option<string> serviceDocumentationOption = new("--service-url")
        {
            Description = "URL to the service documentation (ex: https://learn.microsoft.com/azure/service-bus-messaging)",
            Required = true,
        };

        private readonly Option<string> modelOption = new("--model")
        {
            Description = "The OpenAI model to use when generating the readme. Note, this will match the name of your Azure OpenAI model deployment.",
            Required = true,
            DefaultValueFactory = _ => "gpt-4.1",
        };

        protected override Command GetCommand() => new("readme", "README generator tool")
        {
            modelOption,
            outputPathOption,
            packagePathOption,
            serviceDocumentationOption,
            templatePathOption,
        };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            try
            {
                var templatePath = parseResult.GetValue(templatePathOption);
                var serviceDocumentation = parseResult.GetValue(serviceDocumentationOption);
                var outputPath = parseResult.GetValue(outputPathOption);
                var model = parseResult.GetValue(modelOption);

                var generator = new ReadmeGenerator(
                    logger: logger,
                    microAgentHostService: microAgentHostService,
                    templatePath: templatePath,
                    serviceDocumentation: new Uri(serviceDocumentation),
                    packagePath: parseResult.GetValue(packagePathOption),
                    outputPath: outputPath,
                    model: model);

                await generator.Generate(ct);
                return new DefaultCommandResponse() { Message = $"Readme written to {outputPath}" };
            }
            catch (ReadmeValidationException ex)
            {
                logger.LogError(ex, "ReadmeGeneratorTool failed");
                return new DefaultCommandResponse() { ResponseError = $"ReadmeGenerator failed with validation errors: {ex.Message}" };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ReadmeGeneratorTool failed");
                return new DefaultCommandResponse() { ResponseError = $"ReadmeGenerator threw an exception: {ex.Message}" };
            }
        }
    }

    public partial class ReadmeGenerator
    {
        private readonly ILogger<ReadMeGeneratorTool> logger;
        private readonly IMicroagentHostService microAgentHostService;
        private readonly string templatePath;
        private readonly Uri serviceDocumentation;
        private readonly string subPackagePath;
        private readonly string outputPath;
        private readonly string repoPath;
        private readonly string model;

        public ReadmeGenerator(
            ILogger<ReadMeGeneratorTool> logger, IMicroagentHostService microAgentHostService,
            string templatePath, Uri serviceDocumentation, string packagePath, string outputPath,
            string model)
        {
            this.logger = logger;
            this.microAgentHostService = microAgentHostService;

            this.templatePath = templatePath;
            this.serviceDocumentation = serviceDocumentation;

            var (repoPath, subPackagePath) = GetPackageInfoFromPath(packagePath);
            this.subPackagePath = subPackagePath;
            this.repoPath = repoPath;

            this.outputPath = outputPath;

            this.model = model;
        }

        /// <summary>
        /// Generates a README.md file using a template, service documentation, and package information.
        /// </summary>
        /// <param name="ct">Cancellation token for the operation.</param>
        public async Task Generate(CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrEmpty(templatePath);
            ArgumentNullException.ThrowIfNull(serviceDocumentation);
            ArgumentException.ThrowIfNullOrEmpty(outputPath);
            ArgumentException.ThrowIfNullOrEmpty(repoPath);

            var readmeText = await File.ReadAllTextAsync(templatePath, ct);

            // Use the standardized template system with built-in safety measures
            var template = new ReadMeGenerationTemplate(
                templateContent: readmeText,
                serviceDocumentation: serviceDocumentation.ToString(),
                packagePath: subPackagePath);
            var prompt = template.BuildPrompt();

            var result = await this.microAgentHostService.RunAgentToCompletion(new Microagent<ReadmeContents>()
            {
                Instructions = prompt,
                MaxToolCalls = 100,
                Model = this.model,
                Tools =
                [
                    AgentTool<ReadmeContents, CheckReadmeResult>.FromFunc("check_readme_tool", "Checks a readme to make sure that all the required values have been replaced", CheckReadme),
                ]
            }, ct);

            // to guard against hallucinations, make sure this readme _truly_ is correct.
            var checkReadmeResult = await CheckReadme(result, ct);

            if (checkReadmeResult.Suggestions.Any())
            {
                throw new ReadmeValidationException(string.Join(Environment.NewLine, checkReadmeResult.Suggestions));
            }

            await File.WriteAllTextAsync(outputPath, result.Contents, ct);
        }

        public record ReadmeContents(string Contents);

        record CheckReadmeResult(IEnumerable<string> Suggestions);

        private async Task<CheckReadmeResult> CheckReadme(ReadmeContents parameters, CancellationToken ct)
        {
            var suggestions = new List<string>();

            var re = placeholderRegex();

            var placeholderMatches = re.Matches(parameters.Contents);

            if (placeholderMatches != null && placeholderMatches.Count() > 0)
            {
                var placeholders = placeholderMatches.Select(m => m.Groups[1].Value).Distinct();
                suggestions.Add($"The readme contains placeholders ({string.Join(',', placeholders)}) that should be removed and replaced with a proper package name");
            }

            var localeMatches = localeRegex().Matches(parameters.Contents);

            if (localeMatches != null && localeMatches.Count() > 0)
            {
                var locales = localeMatches.Select(m => m.Groups[1].Value);
                suggestions.Add($"The readme contains links with locales. Keep the link, but remove these locales from links: ({string.Join(',', locales)}).");
            }

            var tempFile = Path.GetTempFileName();

            try
            {
                await File.WriteAllTextAsync(tempFile, parameters.Contents, ct);
                var output = await VerifyLinks(repoPath, tempFile, ct);

                if (output != null)
                {
                    suggestions.Add($"Some links were broken and should be replaced: {output}");
                }
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }

            return new CheckReadmeResult(suggestions);
        }

        /// <summary>
        /// Run the Verify-Links.ps1 script, which checks that all links in the readme are valid.
        /// </summary>
        /// <param name="repoPath">Path to an azure-sdk-for-<language> repository</language></param>
        /// <param name="readmePath">Path to README.md to verify</param>
        /// <param name="ct">Cancellation token for method</param>
        /// <returns>A string, if the verification did not pass, or null if it did.</returns>
        /// <exception cref="Exception"></exception>
        private async Task<string> VerifyLinks(string repoPath, string readmePath, CancellationToken ct)
        {
            var verifyLinksPs1 = Path.Join(repoPath, Constants.ENG_COMMON_SCRIPTS_PATH, "Verify-Links.ps1");
            var errors = new List<string>();

            logger.LogInformation("Running {VerifyLinksPs1} {ReadmePath}", verifyLinksPs1, readmePath);

            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pwsh.exe" : "pwsh",
                ArgumentList = { verifyLinksPs1, readmePath },
                RedirectStandardError = true,
                RedirectStandardOutput = true
            });

            using var verifyLinksCt = CancellationTokenSource.CreateLinkedTokenSource(ct);
            verifyLinksCt.CancelAfter(TimeSpan.FromSeconds(60)); // Set your time limit here

            await process!.WaitForExitAsync(verifyLinksCt.Token);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(ct);
                var stdout = await process.StandardOutput.ReadToEndAsync(ct);

                return $"Verify-Links.ps1 check did not pass.\nStdout: {stdout.Replace("\n", "\n  ")}\nStderr: {stderr.Replace("\n", "\n  ")}";
            }

            return null;
        }

        public static (string RepoPath, string SubPath) GetPackageInfoFromPath(string path)
        {
            var origPath = Path.GetFullPath(path);

            var pieces = origPath.Split($"{Path.DirectorySeparatorChar}sdk{Path.DirectorySeparatorChar}");

            if (pieces.Length != 2)
            {
                throw new ArgumentException("Path was not under a language repo with an 'sdk' subfolder", nameof(path));
            }

            return (pieces[0], pieces[1]);
        }

        // Example: 'en-us' from http://hello/en-us/blah
        [GeneratedRegex(@"https?://[^\s/]+/([a-z]{2}-[a-z]{2})/")]
        private static partial Regex localeRegex();

        // Example matches:
        // - '# Azure Template Package client library for Go'
        // - 'Use the (package) client module `github.com/Azure/azure-sdk-for-go/sdk/(package path)` in your application to:'
        [GeneratedRegex(@"(Azure Template|\(package path\)|aztemplate)")]
        private static partial Regex placeholderRegex();
    }

    /// <summary>
    /// Exception thrown when README validation fails.
    /// </summary>
    public class ReadmeValidationException : Exception
    {
        public ReadmeValidationException() { }

        public ReadmeValidationException(string message) : base(message) { }
    }
}
