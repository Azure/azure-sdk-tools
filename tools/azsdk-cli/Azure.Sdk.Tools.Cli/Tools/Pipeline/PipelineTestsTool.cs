// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Core;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tools.Pipeline;

[McpServerToolType, Description("Fetches test data from Azure Pipelines")]
public class PipelineTestsTool(
    IAzureService azureService,
    IDevOpsService devopsService,
    ILogger<PipelineTestsTool> logger
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.AzurePipelines];

    private readonly Argument<int> buildIdArg = new("Pipeline/Build ID");

    protected override Command GetCommand() =>
        new("test-results", "Get test results for a pipeline run") { buildIdArg };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        Initialize();
        var buildId = parseResult.GetValue(buildIdArg);

        logger.LogInformation("Getting test results for pipeline {buildId}...", buildId);
        return await GetPipelineLlmArtifacts(buildId);
    }

    private BuildHttpClient buildClient;
    private readonly bool initialized = false;

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
