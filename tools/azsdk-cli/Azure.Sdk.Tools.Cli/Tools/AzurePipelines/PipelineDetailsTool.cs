// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Fetches data from Azure Pipelines")]
public class PipelineDetailsTool : MCPTool
{
    private BuildHttpClient buildClient;
    private readonly bool initialized = false;

    private IAzureService azureService;
    private IOutputService output;
    private ILogger<PipelineDetailsTool> logger;

    // Options
    private readonly Option<int> buildIdOpt = new(["--build-id", "-b"], "Pipeline/Build ID") { IsRequired = true };
    private readonly Option<string> projectOpt = new(["--project", "-p"], "Pipeline project name");

    public PipelineDetailsTool(
        IAzureService azureService,
        IOutputService output,
        ILogger<PipelineDetailsTool> logger
    ) : base()
    {
        this.azureService = azureService;
        this.output = output;
        this.logger = logger;

        CommandHierarchy =
        [
            SharedCommandGroups.AzurePipelines
        ];
    }

    public override Command GetCommand()
    {
        var pipelineRunCommand = new Command("pipeline", "Get details for a pipeline run") { buildIdOpt, projectOpt };
        pipelineRunCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        return pipelineRunCommand;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        Initialize();

        var cmd = ctx.ParseResult.CommandResult.Command.Name;
        var buildId = ctx.ParseResult.GetValueForOption(buildIdOpt);
        var project = ctx.ParseResult.GetValueForOption(projectOpt);

        logger.LogInformation("Getting pipeline run {buildId}...", buildId);
        var result = await GetPipelineRun(project, buildId);
        ctx.ExitCode = ExitCode;
        output.Output(result);
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }
        var tokenScope = new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" };  // Azure DevOps scope
        var token = azureService.GetCredential().GetToken(new TokenRequestContext(tokenScope));
        var tokenCredential = new VssOAuthAccessTokenCredential(token.Token);
        var connection = new VssConnection(new Uri($"https://dev.azure.com/azure-sdk"), tokenCredential);
        buildClient = connection.GetClient<BuildHttpClient>();
    }

    [McpServerTool, Description("Gets details for a pipeline run")]
    public async Task<Build> GetPipelineRun(string? project, int buildId)
    {
        try
        {
            logger.LogDebug("Getting pipeline run for {project} {buildId}", project, buildId);
            var build = await buildClient.GetBuildAsync(project ?? "public", buildId);
            return build;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(project))
            {
                throw new Exception($"Failed to find build {buildId} in project 'public': {ex.Message}");
            }
            // If project is not specified, try both azure sdk public and internal devops projects
            return await GetPipelineRun("internal", buildId);
        }
    }
}
