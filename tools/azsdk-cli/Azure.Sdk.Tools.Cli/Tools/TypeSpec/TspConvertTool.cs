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
using Azure.Sdk.Tools.Cli.Commands;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec
{
    /// <summary>
    /// This tool provides functionality for converting existing Azure service swagger definitions to TypeSpec projects.
    /// Use this tool to convert existing services to TypeSpec.
    /// </summary>
    [McpServerToolType, Description("Tools for converting existing Azure service swagger definitions to TypeSpec projects.")]
    public class TypeSpecConvertTool : MCPTool
    {
        private readonly INpxHelper npxHelper;
        private readonly ILogger<TypeSpecConvertTool> logger;
        private readonly IOutputHelper output;

        public TypeSpecConvertTool(INpxHelper npxHelper, ILogger<TypeSpecConvertTool> logger, IOutputHelper output)
        {
            this.npxHelper = npxHelper;
            this.logger = logger;
            this.output = output;
            CommandHierarchy = [SharedCommandGroups.TypeSpec];
        }

        // commands
        private const string ConvertSwaggerCommandName = "convert-swagger";

        // command options
        private readonly Option<string> outputDirectoryArg = new("--output-directory", "The output directory for the generated TypeSpec project. This directory must already exist and be empty.") { IsRequired = true };
        private readonly Option<string> swaggerReadmeArg = new("--swagger-readme", "The path or URL to an Azure swagger README file.") { IsRequired = true };
        private readonly Option<bool> isArmOption = new("--arm", "Whether the generated TypeSpec project is for an Azure Resource Management (ARM) API. This should be true if the swagger's path contains 'resource-manager'.");
        private readonly Option<bool> fullyCompatibleOption = new("--fully-compatible", "Whether to generate a TypeSpec project that is fully compatible with the swagger. It is recommended not to set this to true so that the converted TypeSpec project leverages TypeSpec built-in libraries with standard patterns and templates.");

        public override Command GetCommand()
        {

            Command command = new(ConvertSwaggerCommandName, "Convert an existing Azure service swagger definition to a TypeSpec project") {
                swaggerReadmeArg,
                outputDirectoryArg,
                isArmOption,
                fullyCompatibleOption
            };
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            await HandleConvertCommandAsync(ctx, ct);
        }

        private async Task HandleConvertCommandAsync(InvocationContext ctx, CancellationToken ct)
        {
            var swaggerReadme = ctx.ParseResult.GetValueForOption(swaggerReadmeArg);
            var outputDirectory = ctx.ParseResult.GetValueForOption(outputDirectoryArg);
            var isArm = ctx.ParseResult.GetValueForOption(isArmOption);
            var fullyCompatible = ctx.ParseResult.GetValueForOption(fullyCompatibleOption);

            TspToolResponse result = await ConvertSwaggerAsync(swaggerReadme, outputDirectory, isArm, fullyCompatible, true, ct);
            ctx.ExitCode = ExitCode;
            output.Output(result);
        }

        [
            McpServerTool(Name = "azsdk_convert_swagger_to_typespec"),
            Description("Converts an existing Azure service swagger definition to a TypeSpec project. Returns path to the created project.")
        ]
        public async Task<TspToolResponse> ConvertSwaggerAsync(
            [Description("Path to the swagger README file.")]
            string pathToSwaggerReadme,
            [Description("The output directory for the generated TypeSpec project. This must be an existing empty directory.")]
            string outputDirectory,
            [Description(@"
                Indicates whether the swagger is for an Azure Resource Management (ARM) API.
                Should be true if the swagger's path contains `resource-manager`.
                ")
            ]
            bool? isAzureResourceManagement,
            [Description(@"
                Indicates whether the generated TypeSpec project should be fully compatible with the swagger.
                It is recommended to set this to `false` so that the generated project leverages TypeSpec built-in libraries with standard patterns and templates.
                ")
            ]
            bool? fullyCompatible,
            bool isCli,
            CancellationToken ct
        )
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
                        ResponseError = $"Failed: Invalid --output-directory, {validationResult}"
                    };
                }

                var fullOutputDir = Path.GetFullPath(outputDirectory.Trim());
                return await RunTspClientAsync(fullPathToSwaggerReadme, fullOutputDir, isAzureResourceManagement ?? false, fullyCompatible ?? false, isCli, ct);
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

        private async Task<TspToolResponse> RunTspClientAsync(
            string pathToSwaggerReadme,
            string outputDirectory,
            bool isAzureResourceManagement,
            bool fullyCompatible,
            bool isCli,
            CancellationToken ct
        )
        {
            var npxOptions = new NpxOptions(
                "@azure-tools/typespec-client-generator-cli",
                ["tsp-client", "convert", "--swagger-readme", pathToSwaggerReadme, "--output-dir", outputDirectory],
                logOutputStream: true
            );

            if (isAzureResourceManagement)
            {
                npxOptions.AddArgs("--arm");
            }

            if (fullyCompatible)
            {
                npxOptions.AddArgs("--fully-compatible");
            }

            var result = await npxHelper.Run(npxOptions, ct);
            if (result.ExitCode != 0)
            {
                SetFailure();
                // Omit printing details in CLI mode since we already stream the generator cli output
                if (isCli)
                {
                    return new TspToolResponse
                    {
                        ResponseError = $"Failed to convert swagger to TypeSpec project, see details in the above logs."
                    };
                }
                return new TspToolResponse
                {
                    ResponseError = $"Failed to convert swagger to TypeSpec project, see generator output below" +
                                    Environment.NewLine +
                                    result.Output
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
