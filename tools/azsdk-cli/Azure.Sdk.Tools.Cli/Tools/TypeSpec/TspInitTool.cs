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
    /// This tool provides functionality for initializing TypeSpec projects.
    /// Use this tool to onboard new services to TypeSpec.
    /// </summary>
    [McpServerToolType, Description("Tools for initializing TypeSpec projects.")]
    public class TypeSpecInitTool : MCPTool
    {
        private readonly INpxHelper npxHelper;
        private readonly ILogger<TypeSpecInitTool> logger;
        private readonly IOutputHelper output;
        private readonly ITypeSpecDocsService docsService;

        public TypeSpecInitTool(INpxHelper npxHelper, ILogger<TypeSpecInitTool> logger, IOutputHelper output, ITypeSpecDocsService docsService)
        {
            this.npxHelper = npxHelper;
            this.logger = logger;
            this.output = output;
            this.docsService = docsService;
            CommandHierarchy = [SharedCommandGroups.TypeSpec];
        }

        // This is the template registry URL used by the TypeSpec compiler's init command.
        private const string AzureTemplatesUrl = "https://aka.ms/typespec/azure-init";

        // command
        private const string InitCommandName = "init";

        // command options
        private readonly Option<string> outputDirectoryArg = new("--output-directory", "The output directory for the generated TypeSpec project. This directory must already exist and be empty.") { IsRequired = true };
        private readonly Option<string> templateArg = new("--template", "The template to use for the TypeSpec project. Use azure-arm for resource management services, or azure-core for data plane services.") { IsRequired = true };
        private readonly Option<string> serviceNamespaceArg = new("--service-namespace", "The namespace of the service you are creating. This should be in Pascal case and represent the service's namespace.") { IsRequired = true };

        private enum ServiceType
        {
            ARM,
            DataPlane
        }

        private static readonly Dictionary<string, ServiceType> templateMap = new()
        {
            { "azure-arm", ServiceType.ARM },
            { "azure-core", ServiceType.DataPlane }
        };

        public override Command GetCommand()
        {
            // Add validator to serviceNamespaceArg
            serviceNamespaceArg.AddValidator(result =>
            {
                var value = result.GetValueOrDefault<string>();
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.ErrorMessage = "The service namespace cannot be empty or whitespace.";
                }
            });
            Command command = new(InitCommandName, "Initialize a new TypeSpec project") {
                outputDirectoryArg,
                templateArg,
                serviceNamespaceArg
            };
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            await HandleInitCommandAsync(ctx, ct);
        }

        private async Task HandleInitCommandAsync(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var outputDirectory = ctx.ParseResult.GetValueForOption(outputDirectoryArg);
                var template = ctx.ParseResult.GetValueForOption(templateArg);
                var serviceNamespace = ctx.ParseResult.GetValueForOption(serviceNamespaceArg);

                TspToolResponse result = await InitTypeSpecProjectAsync(outputDirectory: outputDirectory, template: template, serviceNamespace: serviceNamespace, isCli: true, ct);
                ctx.ExitCode = ExitCode;
                output.Output(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error initializing TypeSpec project");
                SetFailure();
                ctx.ExitCode = ExitCode;
            }
        }

        [McpServerTool(Name = "azsdk_init_typespec_project"), Description("Use this tool to initialize a new TypeSpec project. Returns the path to the created project.")]
        public async Task<TspToolResponse> InitTypeSpecProjectAsync(
            [Description("Pass in the output directory where the project should be created. Must be an existing empty directory.")]
            string outputDirectory,
            [Description("`azure-core` for data-plane services, or `azure-arm` for resource-manager services.")]
            string template,
            [Description("The namespace of the service you are creating. Should be Pascal case. Exclude the 'Microsoft.' prefix for ARM services.")]
            string serviceNamespace,
            bool isCli,
            CancellationToken ct = default
        )
        {
            try
            {
                logger.LogInformation("Initializing TypeSpec project: {outputDirectory}, template: {template}, serviceNamespace: {serviceNamespace}",
                    outputDirectory, template, serviceNamespace);



                // Validate template
                if (string.IsNullOrWhiteSpace(template) || !templateMap.ContainsKey(template))
                {
                    SetFailure();
                    return new TspToolResponse
                    {
                        ResponseError = $"Failed: Invalid --template, '{template}'. Must be one of: {string.Join(", ", templateMap.Keys)}."
                    };
                }

                string normalizedServiceName = templateMap[template] switch
                {
                    ServiceType.ARM => serviceNamespace.StartsWith("Microsoft.") ? serviceNamespace["Microsoft.".Length..] : serviceNamespace,
                    ServiceType.DataPlane => serviceNamespace,
                    _ => serviceNamespace
                };

                // Validate service namespace
                if (string.IsNullOrWhiteSpace(serviceNamespace))
                {
                    SetFailure();
                    return new TspToolResponse
                    {
                        ResponseError = $"Failed: Invalid --service-namespace, '{serviceNamespace}'."
                    };
                }

                // Validate outputDirectory using FileHelper
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
                return await RunTspInitAsync(outputDirectory: fullOutputDir, template: template, serviceNamespace: normalizedServiceName, isCli, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while initializing TypeSpec project: {outputDirectory}, {template}, {serviceNamespace}", outputDirectory, template, serviceNamespace);
                SetFailure();
                return new TspToolResponse
                {
                    ResponseError = $"Failed: An error occurred trying to initialize TypeSpec project in '{outputDirectory}': {ex.Message}"
                };
            }
        }

        private async Task<TspToolResponse> RunTspInitAsync(string outputDirectory, string template, string serviceNamespace, bool isCli, CancellationToken ct)
        {
            var npxOptions = new NpxOptions(
                "@typespec/compiler",
                ["tsp", "init", "--no-prompt"],
                logOutputStream: true
            );
            npxOptions.AddArgs("--project-name", serviceNamespace, "--args", $"ServiceNamespace={serviceNamespace}");
            npxOptions.AddArgs("--output-dir", outputDirectory);
            npxOptions.AddArgs("--template", template, AzureTemplatesUrl);

            var tspInitCt = CancellationTokenSource.CreateLinkedTokenSource(ct);
            tspInitCt.CancelAfter(TimeSpan.FromMinutes(2)); // Set a timeout for the initialization

            var result = await npxHelper.Run(npxOptions, tspInitCt.Token);
            if (result.ExitCode != 0)
            {
                SetFailure();
                if (isCli)
                {
                    return new TspToolResponse
                    {
                        ResponseError = $"Failed to initialize TypeSpec project, see details in the above logs."
                    };
                }
                return new TspToolResponse
                {
                    ResponseError = $"Failed to initialize TypeSpec project, see generator output below" +
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

        [McpServerTool(Name = "azsdk_list_typespec_topics"),
         Description("Use this tool to list all available TypeSpec and TypeSpec Azure documentation topics with their descriptions.")]
        public async Task<ListTypeSpecTopicsResponse> ListTypeSpecTopicsAsync(
            CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Fetching TypeSpec documentation topics");
                var response = await docsService.GetTopicsAsync(ct);

                if (!response.IsSuccessful)
                {
                    SetFailure();
                }

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching TypeSpec topics");
                SetFailure();
                return new ListTypeSpecTopicsResponse
                {
                    IsSuccessful = false,
                    ResponseError = $"Failed to fetch TypeSpec topics: {ex.Message}"
                };
            }
        }

        [McpServerTool(Name = "azsdk_get_typespec_topics_docs"),
         Description("Use this tool to fetch the documentation content for specified TypeSpec and TypeSpec Azure topics. Call azsdk_list_typespec_topics first to see available topics.")]
        public async Task<GetTypeSpecTopicsDocsResponse> GetTypeSpecTopicsDocsAsync(
            [Description("List of topic names to fetch documentation for. Use azsdk_list_typespec_topics to see available topics.")]
            List<string> topics,
            CancellationToken ct = default)
        {
            try
            {
                if (topics == null || !topics.Any())
                {
                    SetFailure();
                    return new GetTypeSpecTopicsDocsResponse
                    {
                        IsSuccessful = false,
                        ResponseError = "Topics list is required and cannot be empty"
                    };
                }

                logger.LogInformation("Fetching documentation for {Count} TypeSpec topics", topics.Count);

                var response = await docsService.GetTopicDocsAsync(topics, ct);

                if (!response.IsSuccessful)
                {
                    SetFailure();
                }

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching TypeSpec documentation");
                SetFailure();
                return new GetTypeSpecTopicsDocsResponse
                {
                    IsSuccessful = false,
                    ResponseError = $"Failed to fetch TypeSpec documentation: {ex.Message}"
                };
            }
        }
    }
}
