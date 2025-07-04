// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text.Json;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Fetches test data from Azure Pipelines")]
public class PipelineTestsTool : MCPTool
{
    private BuildHttpClient buildClient;
    private readonly bool initialized = false;

    private IAzureService azureService;
    private IOutputService output;
    private ILogger<PipelineTestsTool> logger;

    private readonly Argument<int> buildIdArg = new("Pipeline/Build ID");

    public PipelineTestsTool(
        IAzureService azureService,
        IOutputService output,
        ILogger<PipelineTestsTool> logger
    ) : base()
    {
        this.azureService = azureService;
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
        var tokenScope = new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" };  // Azure DevOps scope
        var token = azureService.GetCredential().GetToken(new TokenRequestContext(tokenScope));
        var tokenCredential = new VssOAuthAccessTokenCredential(token.Token);
        var connection = new VssConnection(new Uri($"https://dev.azure.com/azure-sdk"), tokenCredential);
        buildClient = connection.GetClient<BuildHttpClient>();
    }

    private async Task<Dictionary<string, List<string>>> GetLlmArtifactsUnauthenticated(string project, int buildId)
    {
        var result = new Dictionary<string, List<string>>();
        using var httpClient = new HttpClient();
        var artifactsUrl = $"https://dev.azure.com/azure-sdk/{project}/_apis/build/builds/{buildId}/artifacts?api-version=7.1-preview.5";
        var artifactsResponse = await httpClient.GetAsync(artifactsUrl);
        artifactsResponse.EnsureSuccessStatusCode();
        var artifactsJson = await artifactsResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(artifactsJson);
        var artifacts = doc.RootElement.GetProperty("value").EnumerateArray();

        var seenFiles = new HashSet<string>();
        var tempDir = Path.Combine(Path.GetTempPath(), buildId.ToString());
        if (Directory.Exists(tempDir))
        {
            await Task.Factory.StartNew(() =>
            {
                Directory.Delete(tempDir, true);
            });
        }
        Directory.CreateDirectory(tempDir);

        foreach (var artifact in artifacts)
        {
            var name = artifact.GetProperty("name").GetString();
            if (name == null || name.StartsWith("LLM Artifacts", StringComparison.OrdinalIgnoreCase) == false)
            {
                continue;
            }

            var downloadUrl = artifact.GetProperty("resource").GetProperty("downloadUrl").GetString();
            if (string.IsNullOrEmpty(downloadUrl))
            {
                continue;
            }

            logger.LogDebug("Downloading artifact '{artifactName}' to '{tempDir}'", name, tempDir);

            var zipPath = Path.Combine(tempDir, "artifact.zip");

            using (var zipStream = await httpClient.GetStreamAsync(downloadUrl))
            using (var fileStream = File.Create(zipPath))
            {
                await zipStream.CopyToAsync(fileStream);
            }

            await Task.Factory.StartNew(() =>
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir);
                File.Delete(zipPath);
            });

            var files = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).ToList();
            var newFiles = files.Where(f => !seenFiles.Contains(f)).ToList();
            seenFiles.UnionWith(newFiles);

            var testPlatform = name["LLM Artifacts - ".Length..];
            result[testPlatform] = newFiles;
        }

        return result;
    }

    private async Task<Dictionary<string, List<string>>> GetLlmArtifactsAuthenticated(string project, int buildId)
    {
        var result = new Dictionary<string, List<string>>();
        var artifacts = await buildClient.GetArtifactsAsync(project, buildId, cancellationToken: default);
        foreach (var artifact in artifacts)
        {
            if (artifact.Name.StartsWith("LLM Artifacts", StringComparison.OrdinalIgnoreCase))
            {
                var tempDir = Path.Combine(Path.GetTempPath(), $"{artifact.Name}_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);

                logger.LogDebug("Downloading artifact '{artifactName}' to '{tempDir}'", artifact.Name, tempDir);

                using var stream = await buildClient.GetArtifactContentZipAsync(project, buildId, artifact.Name);
                var zipPath = Path.Combine(tempDir, "artifact.zip");
                using (var fileStream = File.Create(zipPath))
                {
                    await stream.CopyToAsync(fileStream);
                }

                await Task.Factory.StartNew(() =>
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir);
                    File.Delete(zipPath);
                });

                var files = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).ToList();
                result[artifact.Name] = files;
            }
        }
        return result;
    }

    [McpServerTool, Description("Downloads artifacts intended for LLM analysis from a pipeline run")]
    public async Task<ObjectCommandResponse> GetPipelineLlmArtifacts(int buildId)
    {
        string project = "";
        try
        {
            var build = await GetPipelineRun(buildId);
            project = build.Project.Name;
            logger.LogInformation("Fetching artifacts for build {buildId} in project {project}", buildId, project);

            Dictionary<string, List<string>> result;
            if (string.Equals(project, "public", StringComparison.OrdinalIgnoreCase))
            {
                result = await GetLlmArtifactsUnauthenticated(project, buildId);
            }
            else
            {
                result = await GetLlmArtifactsAuthenticated(project, buildId);
            }

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
            return await buildClient.GetBuildAsync("public", buildId);
        }
        catch (Exception)
        {
            return await buildClient.GetBuildAsync("internal", buildId);
        }
    }
}
