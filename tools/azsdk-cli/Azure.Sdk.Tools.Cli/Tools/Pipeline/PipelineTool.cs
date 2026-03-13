// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Pipeline;

[Description("This type contains the MCP tool to get pipeline status.")]
[McpServerToolType]
public class PipelineTool(
    IPipelineIdentifierHelper pipelineHelper,
    IHttpClientFactory httpClientFactory,
    IAzureService azureService,
    ILogger<PipelineTool> logger
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.AzurePipelines];

    private const string getPipelineStatusCommandName = "status";
    private const string GetPipelineStatusToolName = "azsdk_get_pipeline_status";

    private readonly Option<string> projectOpt = new("--project", "-p")
    {
        Description = "Pipeline project name",
        Required = false,
    };

    protected override Command GetCommand() =>
        new McpCommand(getPipelineStatusCommandName, "Get pipeline run status", GetPipelineStatusToolName) { SharedOptions.PipelineLocator, projectOpt };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var pipelineIdentifier = parseResult.GetValue(SharedOptions.PipelineLocator);
        var project = parseResult.GetValue(projectOpt);

        return await GetPipelineRunStatus(pipelineIdentifier, project);
    }

    [McpServerTool(Name = GetPipelineStatusToolName), Description("Get pipeline status for a given Azure Pipeline link, Build ID, GitHub Pull Request link, or PR number")]
    public async Task<ObjectCommandResponse> GetPipelineRunStatus(
        [Description("Azure Pipeline link, Build ID, GitHub Pull Request link, or PR number")] string pipelineIdentifier,
        [Description("Pipeline project name (optional)")] string? project = null)
    {
        try
        {
            var builds = await pipelineHelper.ResolveBuildsAsync(pipelineIdentifier, project);

            if (builds.Count == 0)
            {
                return new ObjectCommandResponse
                {
                    ResponseError = $"No failed Azure Pipeline builds found for {pipelineIdentifier}"
                };
            }

            var statuses = new List<BuildStatusResult>();
            foreach (var build in builds)
            {
                var status = await GetSingleBuildStatus(build);
                statuses.Add(status);
            }

            return new ObjectCommandResponse { Result = statuses };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get pipeline run for {pipelineIdentifier}", pipelineIdentifier);
            return new ObjectCommandResponse
            {
                ResponseError = $"Failed to get pipeline run for {pipelineIdentifier}. Error: {ex.Message}"
            };
        }
    }

    private async Task<BuildStatusResult> GetSingleBuildStatus(ResolvedBuild build)
    {
        var buildProject = build.Project;
        if (string.IsNullOrEmpty(buildProject))
        {
            buildProject = await pipelineHelper.GetPipelineProjectAsync(build.BuildId);
        }

        var httpClient = httpClientFactory.CreateClient();

        if (buildProject != Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT)
        {
            var tokenScope = new[] { Constants.AZURE_DEVOPS_TOKEN_SCOPE };
            var token = azureService.GetCredential(Constants.MICROSOFT_CORP_TENANT).GetToken(new TokenRequestContext(tokenScope), CancellationToken.None);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }

        var buildUrl = $"{Constants.AZURE_SDK_DEVOPS_BASE_URL}/{buildProject}/_apis/build/builds/{build.BuildId}?api-version=7.1";
        logger.LogDebug("Getting build status from {url}", buildUrl);
        var response = await httpClient.GetAsync(buildUrl);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var result = root.TryGetProperty("result", out var resultProp) ? resultProp.GetString() : null;
        var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

        return new BuildStatusResult
        {
            BuildId = build.BuildId,
            Status = result ?? status ?? "Not available",
            PipelineUrl = build.PipelineUrl ?? pipelineHelper.GetPipelineUrl(buildProject, build.BuildId),
        };
    }
}

public class BuildStatusResult
{
    [JsonPropertyName("build_id")]
    public int BuildId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("pipeline_url")]
    public string PipelineUrl { get; set; } = "";
}
