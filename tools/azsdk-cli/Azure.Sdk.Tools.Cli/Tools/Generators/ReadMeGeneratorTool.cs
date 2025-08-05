// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Services;
using OpenAI.Chat;

namespace Azure.Sdk.Tools.Cli.Tools.Generators
{
    // Will add after we decide which tools are exported via the MCP.
    //[McpServerToolType, Description("Generates a README file, using service documentation")]
    public partial class ReadMeGeneratorTool : MCPTool
    {
        private readonly ILogger<ReadMeGeneratorTool> logger;
        private readonly IOutputService output;
        private readonly AzureOpenAIClient openAiClient;

        private Option<string> packagePathOption = new(
                name: "--package-path",
                getDefaultValue: () => ".",
                description: "Path to a module, underneath the 'sdk' folder for your repository (ex: /yoursource/azure-sdk-for-go/sdk/messaging/azservicebus)")
        {
            IsRequired = true,
        };

        private Option<string> outputPathOption = new(
                name: "--output-path",
                getDefaultValue: () => "README.output.md",
                description: "Path to write the generated README contents")
        {
            IsRequired = true,
        };

        private Option<string> templatePathOption = new("--template-path", "Path to the README template file (ie: Templates/ReadMeGenerator/README-template.go.md)")
        {
            IsRequired = true,
        };

        private Option<string> serviceDocumentationOption = new(
                "--service-url", "URL to the service documentation (ex: https://learn.microsoft.com/azure/service-bus-messaging)")
        {
            IsRequired = true
        };

        private Option<string> modelOption = new(
            name: "--model", 
            getDefaultValue: () => "gpt-4.1", 
            description: "The OpenAI model to use when generating the readme. Note, this will match the name of your Azure OpenAI model deployment.")
        {
            IsRequired = true,
        };

        public ReadMeGeneratorTool(ILogger<ReadMeGeneratorTool> logger, IOutputService output, AzureOpenAIClient openAiClient)
        {
            this.logger = logger;
            this.output = output;
            this.openAiClient = openAiClient;

            this.CommandHierarchy = [SharedCommandGroups.Generators];
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
            var pr = ctx.ParseResult;

            var templatePath = pr.GetValueForOption(templatePathOption);
            var serviceDocumentation = pr.GetValueForOption(serviceDocumentationOption);
            var outputPath = pr.GetValueForOption(outputPathOption);
            var model = pr.GetValueForOption(modelOption);

            var chatClient = openAiClient.GetChatClient(model);

            var (repoPath, subPackagePath) = GetPackageInfoFromPath(pr.GetValueForOption(packagePathOption));

            var validReadme = await Generate(
                chatClient: chatClient,
                repoPath: repoPath,
                templatePath: templatePath,
                serviceDocumentation: new Uri(serviceDocumentation),
                packagePath: subPackagePath,
                outputPath: outputPath,
                ct: ct);

            if (!validReadme)
            {
                output.OutputError("The generated readme did not pass validation checks. Please check the output for details.");
                ctx.ExitCode = 1;
            }
        }

        /// <summary>
        /// Generates a readme.md file.
        /// </summary>
        /// <summary>
        /// Generates a README.md file using a template, service documentation, and package information.
        /// </summary>
        /// <param name="chatClient">The OpenAI chat client used to generate README content.</param>
        /// <param name="templatePath">Path to the README template file.</param>
        /// <param name="serviceDocumentation">URL to the service documentation for the package.</param>
        /// <param name="packagePath">The package path, under 'sdk', that houses your package.</param>
        /// <param name="outputPath">Path to write the generated README contents.</param>
        /// <param name="repoPath">The root path of the repository containing the package.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>
        /// True if a valid README was generated and passed validation checks; 
        /// false if the README did not pass validation (e.g., due to dead or invalid links).
        /// </returns>
        /// <returns>true if a valid readme was generated, false is the readme did not pass validation checks. For instance, dead/invalid links.</returns>
        async Task<bool> Generate(ChatClient chatClient, string templatePath, Uri serviceDocumentation, string packagePath, string outputPath, string repoPath, CancellationToken ct)
        {
            ArgumentException.ThrowIfNullOrEmpty(templatePath);
            ArgumentNullException.ThrowIfNull(serviceDocumentation);
            ArgumentException.ThrowIfNullOrEmpty(outputPath);
            ArgumentException.ThrowIfNullOrEmpty(repoPath);

            var readmeText = await File.ReadAllTextAsync(templatePath, ct);

            var messages = new ChatMessage[]{
                new SystemChatMessage(
                    """
                    We're going to create some READMEs.
                    The parameters are:
                    * A URL that contains service documentation, to be used when creating key concepts, an introduction blurb and any other places where conceptual docs are needed
                    * A package path that we can use to generate documentation links
                    Here are some more rules to follow:
                    - Do not touch the following sections, or its subsections: Contributing.
                    - Do not generate sample code.
                    - Rules for proper readmes can be found here: https://github.com/Azure/azure-sdk/blob/main/docs/policies/README-TEMPLATE.md
                    """
                ),
                new SystemChatMessage($"The readme template is this: {readmeText} which we fill in with the user parameters, which follow:"),
                new UserChatMessage($"Service URL: {serviceDocumentation}"),
                new UserChatMessage($"Package path: {packagePath}")
            };

            var response = await chatClient.CompleteChatAsync(messages);
            var generatedReadmeText = Customize(response.Value.Content[0].Text);

            await File.WriteAllTextAsync(outputPath, generatedReadmeText, ct);
            output.Output($"Readme written to {outputPath}");

            return await IsValid(repoPath: repoPath, readmePath: outputPath, ct: ct);
        }

        private async Task<bool> IsValid(string repoPath, string readmePath, CancellationToken ct)
        {
            var valid = true;

            try
            {
                var result = await VerifyLinks(repoPath, readmePath, ct);

                if (result != null)
                {
                    output.OutputError($"[FAIL] Verify-Links.ps1 failed: {result}");
                    valid = false;
                }
                else
                {
                    output.Output($"[PASS] Verify-Links.ps1 passed");
                }
            }
            catch (Exception ex)
            {
                output.OutputError($"[ERROR] Verify-Links.ps1 threw an exception: {ex.Message}");
                valid = false;
            }

            return valid;
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
            var verifyLinksPs1 = Path.Join(repoPath, "eng", "common", "scripts", "Verify-Links.ps1");
            var errors = new List<string>();

            output.Output($"Running {verifyLinksPs1} {readmePath}");

            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pwsh.exe" : "pwsh",
                ArgumentList = { verifyLinksPs1, readmePath },
                RedirectStandardError = true,
                RedirectStandardOutput = true
            });

            using var verifyLinksCt = CancellationTokenSource.CreateLinkedTokenSource(ct);
            verifyLinksCt.CancelAfter(TimeSpan.FromSeconds(30)); // Set your time limit here

            await process!.WaitForExitAsync(verifyLinksCt.Token);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(ct);
                var stdout = await process.StandardOutput.ReadToEndAsync(ct);

                return $"Verify-Links.ps1 check did not pass.\nStdout: {stdout.Replace("\n", "\n  ")}\nStderr: {stderr.Replace("\n", "\n  ")}";
            }

            return null;
        }

        /// <summary>
        /// Check the readme, make sure that none of our rules have been violated.
        /// </summary>
        /// <returns></returns>
        private static string Customize(string generatedReadmeText)
        {
            if (TemplatePackagePlaceholderRegex().IsMatch(generatedReadmeText))
            {
                // we failed to get rid of the template package placeholder
                throw new Exception("Template package placeholders were not all removed");
            }

            // check any links within the package, and fix any en-us ones.
            generatedReadmeText = generatedReadmeText.Replace("/en-us/", "/");

            return generatedReadmeText;
        }

        private static (string RepoPath, string SubPath) GetPackageInfoFromPath(string path)
        {
            var origPath = Path.GetFullPath(path);

            var pieces = origPath.Split($"{Path.DirectorySeparatorChar}sdk{Path.DirectorySeparatorChar}");

            if (pieces.Length != 2)
            {
                throw new ArgumentException("Path was not under a language repo with an 'sdk' subfolder", nameof(path));
            }

            return (pieces[0], pieces[1]);
        }

        [GeneratedRegex(@"Template Package|<package path>", RegexOptions.IgnoreCase, "")]
        private static partial Regex TemplatePackagePlaceholderRegex();
    }
}

