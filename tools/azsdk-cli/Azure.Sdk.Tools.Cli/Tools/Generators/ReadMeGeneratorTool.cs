// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using OpenAI.Chat;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Tools.Generators
{
    // Will add after we decide which tools are exported via the MCP.
    //[McpServerToolType, Description("Generates a README file, using service documentation")]
    public partial class ReadMeGeneratorTool : MCPTool
    {
        private readonly ILogger<ReadMeGeneratorTool> logger;
        private readonly IOutputService output;
        private readonly IAzureOpenAIClient openAiClient;
        private Option<string> packagePathOption;
        private Option<string> engPathOption;
        private Option<string> outputPathOption;
        private Option<string> templatePathOption;
        private Option<string> serviceDocumentationOption;
        private Option<string> modelOption;

        public ReadMeGeneratorTool(ILogger<FileValidationTool> logger, IOutputService output, IAzureOpenAIClient openAiClient)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(output);


            this.logger = logger;
            this.output = output;
            this.openAiClient = openAiClient;

            this.CommandHierarchy = [SharedCommandGroups.Generators];
        }

        public override Command GetCommand()
        {
            templatePathOption = new Option<string>("--template-path")
            {
                IsRequired = true,
                Description = "Path to the README template file (ie: Templates/README-template.go.md)",
            };

            templatePathOption.SetDefaultValue("Templates/ReadMeGenerator/README-template.go.md");

            serviceDocumentationOption = new Option<string>(
                "--service-documentation")
            {
                Description = "URL to the service documentation",
                IsRequired = true
            };

            serviceDocumentationOption.SetDefaultValue("https://learn.microsoft.com/en-us/azure/service-bus-messaging/");

            packagePathOption = new Option<string>(
                "--package-path")
            {
                Description = "Path, underneath the 'sdk' folder (ex: devcenter/devbox)",
                IsRequired = true,
            };

            engPathOption = new Option<string>(
                "--eng-path")
            {
                Description = "Path to an 'eng' folder within an SDK repo (ex: /home/user/src/azure-sdk-for-go/eng)",
                IsRequired = true,
            };

            outputPathOption = new Option<string>(
                "--output-path")
            {
                Description = "Path to write the generated README contents",
                IsRequired = true,
            };

            outputPathOption.SetDefaultValue("README.output.md");

            modelOption = new Option<string>("--model")
            {
                Description = "The OpenAI model to use when generating the readme. Note, this will match the name of your Azure OpenAI model deployment.",
                IsRequired = true,
            };

            modelOption.SetDefaultValue("gpt-4.1");

            var command = new Command("readme", "README generator tool") {
                engPathOption,
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
            var packagePath = pr.GetValueForOption(packagePathOption);
            var outputPath = pr.GetValueForOption(outputPathOption);
            var engPath = pr.GetValueForOption(engPathOption);
            var model = pr.GetValueForOption(modelOption);

            var chatClient = openAiClient.GetChatClient(model);

            await Generate(
                chatClient: chatClient,
                engPath: engPath,
                templatePath: templatePath,
                serviceDocumentation: new Uri(serviceDocumentation),
                packagePath: packagePath,
                outputPath: outputPath);
        }

        async Task Generate(ChatClient chatClient, string templatePath, Uri serviceDocumentation, string packagePath, string outputPath, string engPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(templatePath);
            ArgumentNullException.ThrowIfNull(serviceDocumentation);
            ArgumentException.ThrowIfNullOrEmpty(outputPath);
            ArgumentException.ThrowIfNullOrEmpty(engPath);

            var readmeText = await File.ReadAllTextAsync(templatePath);

            var messages = new ChatMessage[]{
                new SystemChatMessage(
                    string.Join("\n",
                        "We're going to create some READMEs.",
                        "The parameters are:",
                        "* A URL that contains service documentation, to be used when creating key concepts, an introduction blurb and any other places where conceptual docs are needed",
                        "* A package path that we can use to generate documentation links",
                        "Here are some more rules to follow:",
                        "- Do not touch the following sections, or its subsections: Contributing.",
                        "- Do not generate sample code.",
                        "- Rules for proper readmes can be found here: https://github.com/Azure/azure-sdk/blob/main/docs/policies/README-TEMPLATE.md"
                    )
                ),
                new SystemChatMessage("The readme template is this: \"" + readmeText, "\" which we fill in with the user parameters, which follow:"),
                new UserChatMessage($"Service URL: {serviceDocumentation}"),
                new UserChatMessage($"Package path: {packagePath}")
            };

            var response = await chatClient.CompleteChatAsync(messages);
            var generatedReadmeText = Customize(response.Value.Content[0].Text);

            await File.WriteAllTextAsync(outputPath, generatedReadmeText);
            await Validate(engPath: engPath, readmePath: outputPath);

            output.Output($"Readme written to {outputPath}");
        }

        private async Task Validate(string engPath, string readmePath)
        {
            output.Output($"Running {engPath}/common/scripts/Verify-Links.ps1 \"{readmePath}\"");

            // TODO: I think chradek has a replacement for this.
            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "powershell.exe" : "pwsh",
                Arguments = $"\"{Path.Join(engPath, "common", "scripts", "Verify-Links.ps1")}\" \"{readmePath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true
            });

            ArgumentNullException.ThrowIfNull(process);

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                //var stderr = await process.StandardError.ReadToEndAsync();
                var stdout = await process.StandardOutput.ReadToEndAsync();
                var tabified = string.Join("\n  ", stdout.Split("\n"));

                output.OutputError($"[FAIL]: Verify-Links.ps1 check did not pass.\nStdout: {tabified}");
                return;
            }

            output.OutputError($"[PASS] Verify_links.ps1 passed");
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

        [GeneratedRegex(@"Template Package|<package path>", RegexOptions.IgnoreCase, "")]
        private static partial Regex TemplatePackagePlaceholderRegex();
    }
}

