// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models.Responses;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tools.TspTool
{
    /// <summary>
    /// This tool provides functionality for initializing TypeSpec projects and converting existing Azure service swagger definitions to TypeSpec projects.
    /// Use this tool to onboard to TypeSpec for new services or convert existing services.
    /// </summary>
    [McpServerToolType, Description("Tools for initializing TypeSpec projects and converting existing Azure service swagger definitions to TypeSpec projects.")]
    public class TypeSpecTool(ILogger<TypeSpecTool> logger, IOutputService output) : MCPTool
    {

        // commands
        private const string initCommandName = "init";
        private const string convertSwaggerCommandName = "convert-swagger";

        private readonly Argument<string> templateArg = new("--template", "The template to use for the TypeSpec project. Valid values are: azure-core (for data-plane services), azure-arm (for resource-manager services)");
        private readonly Argument<string> serviceNamespaceArg = new("--service-namespace", "The namespace of the service you are creating. This should be in Pascal case and represent the service's namespace.");
        private readonly Argument<string> outputDirectoryArg = new("--output-directory", "The output directory for the generated TypeSpec project. This directory must already exist and be empty.");

        private readonly Argument<string> swaggerReadmeArg = new("--swagger-readme", "The path or URL to an Azure swagger README file.");
        private readonly Option<bool> isArmOption = new("--arm", "Whether the generated TypeSpec project is for an Azure Resource Management (ARM) API. This should be true if the swagger's path contains 'resource-manager'.");
        private readonly Option<bool> fullyCompatibleOption = new("--fully-compatible", "Whether to generate a TypeSpec project that is fully compatible with the swagger. It is recommended not to set this to true so that the converted TypeSpec project leverages TypeSpec built-in libraries with standard patterns and templates.");

        public override Command GetCommand()
        {
            var tspCommand = new Command("tsp", "Tools for initializing TypeSpec projects and converting existing Azure service swagger definitions to TypeSpec projects");

            var subCommands = new[]
            {
                new Command(initCommandName, "Initialize a new TypeSpec project") {
                    templateArg,
                    serviceNamespaceArg,
                    outputDirectoryArg
                },
                new Command(convertSwaggerCommandName, "Convert an existing Azure service swagger definition to a TypeSpec project") {
                    swaggerReadmeArg,
                    outputDirectoryArg,
                    isArmOption,
                    fullyCompatibleOption
                }
            };

            foreach (var subCommand in subCommands)
            {
                subCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
                tspCommand.AddCommand(subCommand);
            }

            return tspCommand;
        }

        public override async Task<int> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var command = ctx.ParseResult.CommandResult.Command.Name;

            switch (command)
            {
                case initCommandName:
                    await HandleInitCommand(ctx, ct);
                    return 0;
                case convertSwaggerCommandName:
                    await HandleConvertCommand(ctx, ct);
                    return 0;
                default:
                    logger.LogError($"Unknown command: {command}");
                    return 1;
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task HandleInitCommand(InvocationContext ctx, CancellationToken ct)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var template = ctx.ParseResult.GetValueForArgument(templateArg);
            var serviceNamespace = ctx.ParseResult.GetValueForArgument(serviceNamespaceArg);
            var outputDirectory = ctx.ParseResult.GetValueForArgument(outputDirectoryArg);

            var result = Init(template, serviceNamespace, outputDirectory);
            ctx.ExitCode = ExitCode;
            output.Output(result.ToString());
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task HandleConvertCommand(InvocationContext ctx, CancellationToken ct)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var swaggerReadme = ctx.ParseResult.GetValueForArgument(swaggerReadmeArg);
            var outputDirectory = ctx.ParseResult.GetValueForArgument(outputDirectoryArg);
            var isArm = ctx.ParseResult.GetValueForOption(isArmOption);
            var fullyCompatible = ctx.ParseResult.GetValueForOption(fullyCompatibleOption);

            var result = ConvertSwagger(swaggerReadme, outputDirectory, isArm, fullyCompatible);
            ctx.ExitCode = ExitCode;
            output.Output(result.ToString());
        }

        [McpServerTool(Name = "InitializeAzureTypeSpecProject"), Description(@"Initialize a new TypeSpec project
        **Call this tool when starting a new TypeSpec project.**
        Pass in the `template` to use: `azure-core` for data-plane services, or `azure-arm` for resource-manager services.
        Pass in the `serviceNamespace` to use, which is the namespace of the service you are creating. Should be Pascal case. Exclude the 'Microsoft.' prefix for ARM services.
        Pass in the `outputDirectory` where the project should be created. This must be an existing empty directory.
        Returns the path to the created project.")]
        public TspToolResponse Init(string template, string serviceNamespace, string outputDirectory)
        {
            try
            {
                logger.LogInformation("Initializing TypeSpec project with template: {template}, namespace: {serviceNamespace}, output: {outputDirectory}", template, serviceNamespace, outputDirectory);

                // Validate template
                var validTemplates = new[] { "azure-core", "azure-arm" };
                if (string.IsNullOrWhiteSpace(template) || !validTemplates.Contains(template.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    SetFailure(1);
                    return new TspToolResponse
                    {
                        IsSuccessful = false,
                        ErrorMessage = $"Failed: template must be one of: {string.Join(", ", validTemplates)} but was '{template}'"
                    };
                }

                // Validate serviceNamespace
                if (string.IsNullOrWhiteSpace(serviceNamespace))
                {
                    SetFailure(1);
                    return new TspToolResponse
                    {
                        IsSuccessful = false,
                        ErrorMessage = "Failed: serviceNamespace must be provided and cannot be empty."
                    };
                }

                // Validate outputDir
                var validationResult = ValidateOutputDirectory(outputDirectory);
                if (validationResult != null)
                {
                    SetFailure(1);
                    return new TspToolResponse
                    {
                        IsSuccessful = false,
                        ErrorMessage = validationResult
                    };
                }

                var fullOutputDir = Path.GetFullPath(outputDirectory.Trim());
                return RunTspInit(template, serviceNamespace, fullOutputDir);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while initializing TypeSpec project: {template}, {serviceNamespace}, {outputDirectory}", template, serviceNamespace, outputDirectory);
                SetFailure(1);
                return new TspToolResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = $"Failed: An error occurred trying to initialize TypeSpec project: {ex.Message}"
                };
            }
        }

        [McpServerTool(Name = "ConvertSwaggerToTypeSpec"), Description(@"**Call this tool when trying to convert an existing Azure service to TypeSpec.**
        This command should only be ran once to get started working on a TypeSpec project.
        Verify whether the source swagger describes an Azure Resource Management (ARM) API
        or a data plane API if unsure.
        Pass in the `pathToSwaggerReadme` which is the path or URL to the swagger README file.
        Pass in the `outputDirectory` where the TypeSpec project should be created. This must be an existing empty directory.
        Pass in `isAzureResourceManagement` to indicate whether the swagger is for an Azure Resource Management (ARM) API.
        This should be true if the swagger's path contains `resource-manager`.
        Pass in `fullyCompatible` to indicate whether the generated TypeSpec project should be fully compatible with the swagger.
        It is recommended not to set this to `true` so that the converted TypeSpec project
        leverages TypeSpec built-in libraries with standard patterns and templates.
        Returns path to the created project.")]
        public TspToolResponse ConvertSwagger(string pathToSwaggerReadme, string outputDirectory, bool? isAzureResourceManagement = null, bool? fullyCompatible = null)
        {
            try
            {
                logger.LogInformation("Converting swagger to TypeSpec: {pathToSwaggerReadme}, output: {outputDirectory}, isArm: {isArm}, fullyCompatible: {fullyCompatible}",
                    pathToSwaggerReadme, outputDirectory, isAzureResourceManagement, fullyCompatible);

                // Validate pathToSwaggerReadme
                if (string.IsNullOrWhiteSpace(pathToSwaggerReadme) || !pathToSwaggerReadme.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    SetFailure(1);
                    return new TspToolResponse
                    {
                        IsSuccessful = false,
                        ErrorMessage = "Failed: pathToSwaggerReadme must be a valid Markdown file."
                    };
                }

                var fullPathToSwaggerReadme = Path.GetFullPath(pathToSwaggerReadme.Trim());
                if (!File.Exists(fullPathToSwaggerReadme))
                {
                    SetFailure(1);
                    return new TspToolResponse
                    {
                        IsSuccessful = false,
                        ErrorMessage = $"Failed: pathToSwaggerReadme '{fullPathToSwaggerReadme}' does not exist."
                    };
                }

                // validate outputDirectory using the extracted method
                var validationResult = ValidateOutputDirectory(outputDirectory);
                if (validationResult != null)
                {
                    SetFailure(1);
                    return new TspToolResponse
                    {
                        IsSuccessful = false,
                        ErrorMessage = validationResult
                    };
                }

                var fullOutputDir = Path.GetFullPath(outputDirectory.Trim());
                return RunTspClient(fullPathToSwaggerReadme, fullOutputDir, isAzureResourceManagement ?? false, fullyCompatible ?? false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while converting swagger to TypeSpec: {pathToSwaggerReadme}, {outputDirectory}", pathToSwaggerReadme, outputDirectory);
                SetFailure(1);
                return new TspToolResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = $"Failed: An error occurred trying to convert '{pathToSwaggerReadme}': {ex.Message}"
                };
            }
        }

        private static string? ValidateOutputDirectory(string outputDir)
        {
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                return "Failed: outputDirectory must be provided";
            }

            var fullOutputDir = Path.GetFullPath(outputDir.Trim());
            if (string.IsNullOrEmpty(fullOutputDir))
            {
                return $"Failed: outputDirectory '{outputDir}' could not be resolved to a full path.";
            }

            if (!Directory.Exists(fullOutputDir))
            {
                return $"Failed: Full output directory '{fullOutputDir}' does not exist.";
            }

            if (Directory.GetFileSystemEntries(fullOutputDir).Length != 0)
            {
                return $"Failed: The full output directory '{fullOutputDir}' points to a non-empty directory.";
            }

            return null; // Validation passed
        }

        private TspToolResponse RunTspInit(string template, string serviceNamespace, string outputDirectory)
        {
            const string AZURE_TEMPLATES_URL = "https://aka.ms/typespec/azure-init";

            var argsList = new List<string>
            {
                "tsp",
                "init",
                "--template",
                template,
                "--project-name",
                serviceNamespace,
                "--args",
                $"ServiceNamespace={serviceNamespace}",
                "--output-dir",
                outputDirectory,
                "--no-prompt",
                AZURE_TEMPLATES_URL
            };

            var result = ProcessHelper.RunNpx(argsList, Environment.CurrentDirectory);
            if (result.ExitCode != 0)
            {
                SetFailure();
                return new TspToolResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = $"Failed to initialize TypeSpec project: {result.Output}"
                };
            }

            return new TspToolResponse
            {
                IsSuccessful = true,
                TypeSpecProjectPath = outputDirectory
            };
        }

        private TspToolResponse RunTspClient(string pathToSwaggerReadme, string outputDirectory, bool isAzureResourceManagement, bool fullyCompatible)
        {
            var argsList = new List<string>
            {
                "tsp-client",
                "convert",
                "--swagger-readme",
                pathToSwaggerReadme,
                "--output-dir",
                outputDirectory
            };

            if (isAzureResourceManagement)
            {
                argsList.Add("--arm");
            }

            if (fullyCompatible)
            {
                argsList.Add("--fully-compatible");
            }

            var result = ProcessHelper.RunNpx(argsList, Environment.CurrentDirectory);

            if (result.ExitCode != 0)
            {
                SetFailure();
                return new TspToolResponse
                {
                    IsSuccessful = false,
                    ErrorMessage = $"Failed to convert swagger to TypeSpec project: {result.Output}"
                };
            }

            return new TspToolResponse
            {
                IsSuccessful = true,
                TypeSpecProjectPath = outputDirectory
            };
        }
    }
}
