// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    // Will add after we decide which tools are exported via the MCP.
    //[McpServerToolType, Description("Generates a README file, using service documentation")]
    public class ReadMeGeneratorTool : MCPTool
    {
        private readonly ILogger<ReadMeGeneratorTool> logger;
        private readonly IOutputHelper output;
        private readonly IMicroagentHostService microAgentHostService;

        private readonly Option<string> packagePathOption = new(
            name: "--package-path",
            getDefaultValue: () => ".",
            description: "Path to a module, underneath the 'sdk' folder for your repository (ex: /yoursource/azure-sdk-for-go/sdk/messaging/azservicebus)")
        {
            IsRequired = true,
        };

        private readonly Option<string> outputPathOption = new(
                name: "--output-path",
                getDefaultValue: () => "README.output.md",
                description: "Path to write the generated README contents")
        {
            IsRequired = true,
        };

        private readonly Option<string> templatePathOption = new("--template-path", "Path to the README template file (ie: Templates/ReadMeGenerator/README-template.go.md)")
        {
            IsRequired = true,
        };

        private readonly Option<string> serviceDocumentationOption = new(
                "--service-url", "URL to the service documentation (ex: https://learn.microsoft.com/azure/service-bus-messaging)")
        {
            IsRequired = true
        };

        private readonly Option<string> modelOption = new(
            name: "--model",
            getDefaultValue: () => "gpt-4.1",
            description: "The OpenAI model to use when generating the readme. Note, this will match the name of your Azure OpenAI model deployment.")
        {
            IsRequired = true,
        };

        public ReadMeGeneratorTool(ILogger<ReadMeGeneratorTool> logger, IOutputHelper output, IMicroagentHostService microAgentHostService)
        {
            this.CommandHierarchy = [SharedCommandGroups.Generators];

            this.logger = logger;
            this.output = output;
            this.microAgentHostService = microAgentHostService;
        }

        public override Command GetCommand()
        {
            var command = new Command("readme", "README generator tool") {
                modelOption,
                outputPathOption,
                packagePathOption,
                serviceDocumentationOption,
                templatePathOption,
            };

            command.SetHandler(async (ic) => await HandleCommand(ic, ic.GetCancellationToken()));

            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var pr = ctx.ParseResult;

                var templatePath = pr.GetValueForOption(templatePathOption);
                var serviceDocumentation = pr.GetValueForOption(serviceDocumentationOption);
                var outputPath = pr.GetValueForOption(outputPathOption);
                var model = pr.GetValueForOption(modelOption);

                var generator = new ReadmeGenerator(
                    logger: logger,
                    output: output,
                    microAgentHostService: microAgentHostService,
                    templatePath: templatePath,
                    serviceDocumentation: new Uri(serviceDocumentation),
                    packagePath: pr.GetValueForOption(packagePathOption),
                    outputPath: outputPath,
                    model: model);

                await generator.Generate(ct);
                output.Output($"Readme written to {outputPath}");
            }
            catch (ReadmeValidationException ex)
            {
                logger.LogError(ex, "ReadmeGeneratorTool failed");
                output.OutputError($"ReadmeGenerator failed with validation errors: {ex.Message}");
                ctx.ExitCode = 1;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ReadmeGeneratorTool failed");
                output.OutputError($"ReadmeGenerator threw an exception: {ex.Message}");
                ctx.ExitCode = 1;
            }
        }
    }

    public partial class ReadmeGenerator
    {
        private readonly ILogger<ReadMeGeneratorTool> logger;
        private readonly IOutputHelper output;
        private readonly IMicroagentHostService microAgentHostService;
        private readonly string templatePath;
        private readonly Uri serviceDocumentation;
        private readonly string subPackagePath;
        private readonly string outputPath;
        private readonly string repoPath;
        private readonly string model;

        public ReadmeGenerator(
            ILogger<ReadMeGeneratorTool> logger, IOutputHelper output, IMicroagentHostService microAgentHostService,
            string templatePath, Uri serviceDocumentation, string packagePath, string outputPath,
            string model)
        {
            this.logger = logger;
            this.output = output;
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

            var prompt = $"""
                We're going to create some READMEs.
                    
                The parameters are:
                * A URL that contains service documentation, to be used when creating key concepts, an introduction blurb and any other places where conceptual docs are needed
                * A package path that we can use to generate documentation links
                    
                Here are some more rules to follow:
                - Do not touch the following sections, or its subsections: Contributing.
                - Do not generate sample code.
                - Rules for proper readmes can be found here: https://github.com/Azure/azure-sdk/blob/main/docs/policies/README-TEMPLATE.md

                The readme template is this: {readmeText} which we fill in with the user parameters, which follow:
                Service URL: {serviceDocumentation}
                Package path: {subPackagePath}

                Call the check_readme_tool with the readme contents, and follow any returned suggestions.
                When there are no further suggestions, give me the readme contents.
                """;

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

