// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models.Responses;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Helpers.Process;

namespace Azure.Sdk.Tools.Cli.Tools.TspTool
{
    /// <summary>
    /// This tool provides functionality for initializing TypeSpec projects and converting existing Azure service swagger definitions to TypeSpec projects.
    /// Use this tool to onboard to TypeSpec for new services or convert existing services.
    /// </summary>
    [McpServerToolType, Description("Tools for initializing TypeSpec projects and converting existing Azure service swagger definitions to TypeSpec projects.")]
    public class TypeSpecTool(INpxHelper npxHelper, ILogger<TypeSpecTool> logger, IOutputService output) : MCPTool
    {

        // commands
        private const string convertSwaggerCommandName = "convert-swagger";

        private readonly Option<string> outputDirectoryArg = new("--output-directory", "The output directory for the generated TypeSpec project. This directory must already exist and be empty.");

        private readonly Option<string> swaggerReadmeArg = new("--swagger-readme", "The path or URL to an Azure swagger README file.");
        private readonly Option<bool> isArmOption = new("--arm", "Whether the generated TypeSpec project is for an Azure Resource Management (ARM) API. This should be true if the swagger's path contains 'resource-manager'.");
        private readonly Option<bool> fullyCompatibleOption = new("--fully-compatible", "Whether to generate a TypeSpec project that is fully compatible with the swagger. It is recommended not to set this to true so that the converted TypeSpec project leverages TypeSpec built-in libraries with standard patterns and templates.");

        public override Command GetCommand()
        {
            var tspCommand = new Command("tsp", "Tools for initializing TypeSpec projects and converting existing Azure service swagger definitions to TypeSpec projects");

            var subCommands = new[]
            {
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
                case convertSwaggerCommandName:
                    await HandleConvertCommand(ctx, ct);
                    return 0;
                default:
                    logger.LogError($"Unknown command: {command}");
                    return 1;
            }
        }

        private Task HandleConvertCommand(InvocationContext ctx, CancellationToken ct)
        {
            var swaggerReadme = ctx.ParseResult.GetValueForOption(swaggerReadmeArg);
            var outputDirectory = ctx.ParseResult.GetValueForOption(outputDirectoryArg);
            var isArm = ctx.ParseResult.GetValueForOption(isArmOption);
            var fullyCompatible = ctx.ParseResult.GetValueForOption(fullyCompatibleOption);

            TspToolResponse result = ConvertSwagger(swaggerReadme, outputDirectory, isArm, fullyCompatible);
            ctx.ExitCode = ExitCode;
            output.Output(result.ToString());
            return Task.CompletedTask;
        }

        [McpServerTool(Name = "ConvertSwaggerToTypeSpec"), Description(@"**Call this tool when trying to convert an existing Azure service to TypeSpec.**
        This command should only be ran once to get started working on a TypeSpec project.
        Verify whether the source swagger describes an Azure Resource Management (ARM) API or a data plane API if unsure.
        Pass in the `pathToSwaggerReadme` which is the path to the swagger README file.
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
                        ErrorMessage = "Failed: pathToSwaggerReadme must be a valid path to a swagger README.md file."
                    };
                }

                var fullPathToSwaggerReadme = Path.GetFullPath(pathToSwaggerReadme.Trim());
                if (!File.Exists(fullPathToSwaggerReadme))
                {
                    SetFailure(1);
                    return new TspToolResponse
                    {
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

        private TspToolResponse RunTspClient(string pathToSwaggerReadme, string outputDirectory, bool isAzureResourceManagement, bool fullyCompatible)
        {
            var argsList = new List<string>
            {
                "--package=@azure-tools/typespec-client-generator-cli",
                "--yes",
                "--",
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

            var result = npxHelper.RunNpx(argsList, Environment.CurrentDirectory);

            if (result.ExitCode != 0)
            {
                SetFailure();
                return new TspToolResponse
                {
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
