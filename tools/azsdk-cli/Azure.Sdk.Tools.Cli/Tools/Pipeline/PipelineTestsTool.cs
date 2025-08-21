// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Core;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tools.Pipeline;

[McpServerToolType, Description("Fetches test data from Azure Pipelines")]
public class PipelineTestsTool : MCPTool
{
    private BuildHttpClient buildClient;
    private readonly bool initialized = false;

    private IAzureService azureService;
    private IDevOpsService devopsService;
    private IOutputHelper output;
    private ILogger<PipelineTestsTool> logger;

    private readonly Argument<int> buildIdArg = new("Pipeline/Build ID");

    public PipelineTestsTool(
        IAzureService azureService,
        IDevOpsService devopsService,
        IOutputHelper output,
        ILogger<PipelineTestsTool> logger
    ) : base()
    {
        this.azureService = azureService;
        this.devopsService = devopsService;
        this.output = output;
        this.logger = logger;

        CommandHierarchy =
        [
            SharedCommandGroups.AzurePipelines // azsdk azp
        ];
    }

    public override Command GetCommand()
    {
        var testResultsCommand = new Command("test-results", "Get test results for a pipeline run") { buildIdArg };
        testResultsCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });

        return testResultsCommand;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        Initialize();
        var buildId = ctx.ParseResult.GetValueForArgument(buildIdArg);

        logger.LogInformation("Getting test results for pipeline {buildId}...", buildId);
        var result = await GetPipelineLlmArtifacts(buildId);
        ctx.ExitCode = ExitCode;
        output.Output(result);
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }
        var tokenScope = new[] { Constants.AZURE_DEVOPS_TOKEN_SCOPE };  // Azure DevOps scope
        var token = azureService.GetCredential().GetToken(new TokenRequestContext(tokenScope), CancellationToken.None);
        var tokenCredential = new VssOAuthAccessTokenCredential(token.Token);
        var connection = new VssConnection(new Uri(Constants.AZURE_SDK_DEVOPS_BASE_URL), tokenCredential);
        buildClient = connection.GetClient<BuildHttpClient>();
    }

    [McpServerTool(Name = "azsdk_get_pipeline_llm_artifacts"), Description("Downloads artifacts intended for LLM analysis from a pipeline run")]
    public async Task<ObjectCommandResponse> GetPipelineLlmArtifacts(int buildId)
    {
        string project = "";
        try
        {
            var build = await GetPipelineRun(buildId);
            project = build.Project.Name;
            logger.LogInformation("Fetching artifacts for build {buildId} in project {project}", buildId, project);
            var result = await devopsService.GetPipelineLlmArtifacts(project, buildId);
            return new ObjectCommandResponse { Result = result };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get pipeline artifacts for build {buildId} in project {project}", buildId, project);
            SetFailure();
            return new ObjectCommandResponse
            {
                ResponseError = $"Failed to get pipeline artifacts for build {buildId} in project {project}",
            };
        }
    }

    private async Task<Build> GetPipelineRun(int buildId, string? project = null)
    {
        if (!string.IsNullOrEmpty(project))
        {
            return await buildClient.GetBuildAsync(project, buildId);
        }
        try
        {
            return await buildClient.GetBuildAsync(Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT, buildId);
        }
        catch (Exception)
        {
            return await buildClient.GetBuildAsync(Constants.AZURE_SDK_DEVOPS_INTERNAL_PROJECT, buildId);
        }
    }
}
