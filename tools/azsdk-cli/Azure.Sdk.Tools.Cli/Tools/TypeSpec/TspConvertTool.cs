// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec
{
    /// <summary>
    /// This tool provides functionality for converting existing Azure service swagger definitions to TypeSpec projects.
    /// Use this tool to convert existing services to TypeSpec.
    /// </summary>
    [McpServerToolType, Description("Tools for converting existing Azure service swagger definitions to TypeSpec projects.")]
    public class TypeSpecConvertTool(
        ILogger<TypeSpecConvertTool> logger,
        ITspClientHelper tspClientHelper
    ) : MCPTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec];

        // commands
        private const string ConvertCommandName = "convert";

        // command options
        private readonly Option<string> outputDirectoryArg = new("--output-directory")
        {
            Description = "The output directory for the generated TypeSpec project. This directory must already exist and be empty.",
            Required = true,
        };

        private readonly Option<string> swaggerReadmeArg = new("--swagger-readme")
        {
            Description = "The path or URL to an Azure swagger README file.",
            Required = true,
        };

        private readonly Option<bool> isArmOption = new("--arm")
        {
            Description = "Whether the generated TypeSpec project is for an Azure Resource Management (ARM) API. This should be true if the swagger's path contains 'resource-manager'.",
            Required = false,
        };

        private readonly Option<bool> fullyCompatibleOption = new("--fully-compatible")
        {
            Description = "Whether to generate a TypeSpec project that is fully compatible with the swagger. It is recommended not to set this to true so that the converted TypeSpec project leverages TypeSpec built-in libraries with standard patterns and templates.",
            Required = false,
        };

        protected override Command GetCommand() =>
            new(ConvertCommandName, "Convert an existing Azure service swagger definition to a TypeSpec project")
            {
                swaggerReadmeArg, outputDirectoryArg, isArmOption, fullyCompatibleOption,
            };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var swaggerReadme = parseResult.GetValue(swaggerReadmeArg);
            var outputDirectory = parseResult.GetValue(outputDirectoryArg);
            var isArm = parseResult.GetValue(isArmOption);
            var fullyCompatible = parseResult.GetValue(fullyCompatibleOption);

            return await ConvertSwaggerAsync(swaggerReadme, outputDirectory, isArm, fullyCompatible, true, ct);
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
            [Description("Indicates whether the swagger is for an Azure Resource Management (ARM) API. " +
                         "Should be true if the swagger's path contains `resource-manager`.")
            ]
            bool isAzureResourceManagement,
            [Description("Indicates whether the generated TypeSpec project should be fully compatible with the swagger. " +
                         "It is recommended to set this to `false` so that the generated project leverages " +
                         "TypeSpec built-in libraries with standard patterns and templates.")
            ]
            bool fullyCompatible,
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
                    return new TspToolResponse
                    {
                        ResponseError = $"Failed: Invalid --output-directory, {validationResult}"
                    };
                }

                var fullOutputDir = Path.GetFullPath(outputDirectory.Trim());
                return await tspClientHelper.ConvertSwaggerAsync(fullPathToSwaggerReadme, fullOutputDir, isAzureResourceManagement, fullyCompatible, isCli, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while converting swagger to TypeSpec: {pathToSwaggerReadme}, {outputDirectory}", pathToSwaggerReadme, outputDirectory);
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

    }
}
