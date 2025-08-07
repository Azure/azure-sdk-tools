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

namespace Azure.Sdk.Tools.Cli.Tools
{
    /// <summary>
    /// This tool provides functionality for initializing TypeSpec projects and converting existing Azure service swagger definitions to TypeSpec projects.
    /// Use this tool to onboard to TypeSpec for new services or convert existing services.
    /// </summary>
    [McpServerToolType, Description("Tools for initializing TypeSpec projects and converting existing Azure service swagger definitions to TypeSpec projects.")]
    public class TypeSpecTool(INpxHelper npxHelper, ILogger<TypeSpecTool> logger, IOutputService output) : MCPTool
    {

        // commands
        private const string ConvertSwaggerCommandName = "convert-swagger";

        private readonly Option<string> outputDirectoryArg = new("--output-directory", "The output directory for the generated TypeSpec project. This directory must already exist and be empty.") { IsRequired = true };

        private readonly Option<string> swaggerReadmeArg = new("--swagger-readme", "The path or URL to an Azure swagger README file.") { IsRequired = true };
        private readonly Option<bool> isArmOption = new("--arm", "Whether the generated TypeSpec project is for an Azure Resource Management (ARM) API. This should be true if the swagger's path contains 'resource-manager'.");
        private readonly Option<bool> fullyCompatibleOption = new("--fully-compatible", "Whether to generate a TypeSpec project that is fully compatible with the swagger. It is recommended not to set this to true so that the converted TypeSpec project leverages TypeSpec built-in libraries with standard patterns and templates.");

        public override Command GetCommand()
        {
            var tspCommand = new Command("tsp", "Tools for initializing TypeSpec projects and converting existing Azure service swagger definitions to TypeSpec projects");

            var subCommands = new[]
            {
                new Command(ConvertSwaggerCommandName, "Convert an existing Azure service swagger definition to a TypeSpec project") {
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
                case ConvertSwaggerCommandName:
                    HandleConvertCommand(ctx, ct);
                    return ctx.ExitCode;
                default:
                    logger.LogError($"Unknown command: {command}");
                    return 1;
            }
        }

        private void HandleConvertCommand(InvocationContext ctx, CancellationToken ct)
        {
            var swaggerReadme = ctx.ParseResult.GetValueForOption(swaggerReadmeArg);
            var outputDirectory = ctx.ParseResult.GetValueForOption(outputDirectoryArg);
            var isArm = ctx.ParseResult.GetValueForOption(isArmOption);
            var fullyCompatible = ctx.ParseResult.GetValueForOption(fullyCompatibleOption);

            TspToolResponse result = ConvertSwagger(swaggerReadme, outputDirectory, isArm, fullyCompatible);
            ctx.ExitCode = ExitCode;
            output.Output(result);
        }

        [McpServerTool(Name = "convert_swagger_to_typespec"), Description(@"Converts an existing Azure service swagger definition to a TypeSpec project.
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
                string? readmeValidationResult = ValidateSwaggerReadme(pathToSwaggerReadme);
                if (readmeValidationResult != null)
                {
                    SetFailure();
                    return new TspToolResponse
                    {
                        ResponseError = readmeValidationResult
                    };
                }
                // fullPathToSwaggerReadme is not null or empty at this point - already validated
                var fullPathToSwaggerReadme = Path.GetFullPath(pathToSwaggerReadme.Trim());

                // validate outputDirectory using FileHelper
                var validationResult = FileHelper.ValidateEmptyDirectory(outputDirectory);
                if (validationResult != null)
                {
                    SetFailure();
                    return new TspToolResponse
                    {
                        ResponseError = $"Failed: Invalid --output-dir, {validationResult}"
                    };
                }

                var fullOutputDir = Path.GetFullPath(outputDirectory.Trim());
                return RunTspClient(fullPathToSwaggerReadme, fullOutputDir, isAzureResourceManagement ?? false, fullyCompatible ?? false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while converting swagger to TypeSpec: {pathToSwaggerReadme}, {outputDirectory}", pathToSwaggerReadme, outputDirectory);
                SetFailure();
                return new TspToolResponse
                {
                    ResponseError = $"Failed: An error occurred trying to convert '{pathToSwaggerReadme}': {ex.Message}"
                };
            }
        }

        private static string? ValidateSwaggerReadme(string pathToSwaggerReadme)
        {
            if (string.IsNullOrWhiteSpace(pathToSwaggerReadme) || !pathToSwaggerReadme.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                return $"Failed: Invalid pathToSwaggerReadme '{pathToSwaggerReadme}' - must be a non-empty path to a swagger README.md file";
            }

            var fullPath = Path.GetFullPath(pathToSwaggerReadme.Trim());
            if (!File.Exists(fullPath))
            {
                return $"Failed: pathToSwaggerReadme '{fullPath}' does not exist.";
            }

            var fullPathToSwaggerReadme = Path.GetFullPath(pathToSwaggerReadme.Trim());
            if (!File.Exists(fullPathToSwaggerReadme))
            {

                return $"Failed: pathToSwaggerReadme '{fullPathToSwaggerReadme}' does not exist.";
            }

            return null; // Validation passed
        }

        private TspToolResponse RunTspClient(string pathToSwaggerReadme, string outputDirectory, bool isAzureResourceManagement, bool fullyCompatible)
        {
            var cmd = npxHelper.CreateCommand();
            cmd.Package = "@azure-tools/typespec-client-generator-cli";
            cmd.Cwd = Environment.CurrentDirectory;
            cmd.AddArgs("tsp-client", "convert", "--swagger-readme", pathToSwaggerReadme, "--output-dir", outputDirectory);

            if (isAzureResourceManagement)
            {
                cmd.AddArgs("--arm");
            }

            if (fullyCompatible)
            {
                cmd.AddArgs("--fully-compatible");
            }

            var result = cmd.Run();
            if (result.ExitCode != 0)
            {
                SetFailure();
                return new TspToolResponse
                {
                    ResponseError = $"Failed to convert swagger to TypeSpec project: {result.Output}"
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
