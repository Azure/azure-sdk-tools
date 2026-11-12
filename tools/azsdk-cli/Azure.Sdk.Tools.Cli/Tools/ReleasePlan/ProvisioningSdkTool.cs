// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;
using YamlDotNet.RepresentationModel;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlan;

[McpServerToolType, Description("Prepare .NET provisioning configuration and generate provisioning libraries via pipeline.")]
public class ProvisioningSdkTool(
    IGitHubService githubService,
    IDevOpsService devopsService,
    ILogger<ProvisioningSdkTool> logger) : MCPMultiCommandTool
{
    private const string Emitter = "@azure-typespec/http-client-csharp-provisioning";
    private const string ManagementEmitter = "@azure-typespec/http-client-csharp-mgmt";
    private const int PipelineDefinitionId = 8473;
    private const string PrepareToolName = "azsdk_prepare_dotnet_provisioning_config";
    private const string GenerateToolName = "azsdk_run_generate_dotnet_provisioning";

    public override CommandGroup[] CommandHierarchy { get; set; } = [new("spec-workflow", "TypeSpec SDK generation commands")];

    private readonly Option<string> projectPath = new("--typespec-project")
    {
        Description = "Local TypeSpec project directory for preparation; specification/... repository-relative path for generation",
        Required = true,
    };
    private readonly Option<string> releaseType = new("--release-type")
    {
        Description = "SDK release type: beta or stable",
        Required = true,
    };
    private readonly Option<int> specPr = new("--pr")
    {
        Description = "Existing public spec PR number; omit to use already configured main",
    };

    protected override List<Command> GetCommands() =>
    [
        new McpCommand("prepare-dotnet-provisioning", "Prepare .NET provisioning emitter options in a local TypeSpec project", PrepareToolName) { projectPath },
        new McpCommand("generate-dotnet-provisioning", "Generate a .NET provisioning library from a spec PR or configured main", GenerateToolName) { projectPath, releaseType, specPr },
    ];

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct) =>
        parseResult.CommandResult.Command.Name switch
        {
            "prepare-dotnet-provisioning" => await PrepareConfigAsync(parseResult.GetValue(projectPath)!, ct),
            "generate-dotnet-provisioning" => await GenerateAsync(parseResult.GetValue(projectPath)!, parseResult.GetValue(releaseType)!, parseResult.GetValue(specPr), ct),
            _ => new DefaultCommandResponse { ResponseError = "Unknown provisioning command." },
        };

    [McpServerTool(Name = PrepareToolName), Description("Inspect a local TypeSpec project's tspconfig.yaml and add missing .NET provisioning options from its .NET management namespace/output layout. Preserves existing configuration and comments; makes no change when already configured. Does not commit, create a PR, or generate code. Follow the provisioning branch of azsdk-common-generate-sdk-pipeline to reuse/create a spec PR and run generation.")]
    public async Task<DefaultCommandResponse> PrepareConfigAsync(string typespecProjectPath, CancellationToken ct = default)
    {
        try
        {
            var configPath = Path.Combine(typespecProjectPath, "tspconfig.yaml");
            var content = await File.ReadAllTextAsync(configPath, ct);
            var options = ReadOptions(content);
            if (IsConfigured(options))
            {
                return new DefaultCommandResponse { Message = "Provisioning configuration already exists. No changes were made." };
            }

            if (!options.Children.TryGetValue(new YamlScalarNode(ManagementEmitter), out var managementNode) ||
                managementNode is not YamlMappingNode management)
            {
                throw new InvalidOperationException($"Configure {ManagementEmitter} first; a provisioning namespace and output layout cannot be inferred safely.");
            }
            var managementNamespace = ReadString(management, "namespace");
            var outputDirectory = ReadString(management, "emitter-output-dir");
            if (!Regex.IsMatch(managementNamespace, @"^Azure\.ResourceManager\.[A-Za-z_][A-Za-z0-9_.]*$") ||
                !outputDirectory.StartsWith("{output-dir}/", StringComparison.Ordinal) ||
                (!outputDirectory.Contains("{namespace}", StringComparison.Ordinal) && !outputDirectory.Contains(managementNamespace, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("The management emitter needs an Azure.ResourceManager.* namespace and an explicit namespace-based emitter-output-dir. Review its layout before adding provisioning.");
            }
            if (options.Style == YamlDotNet.Core.Events.MappingStyle.Flow || options.Start.Column <= 1 || !options.Anchor.IsEmpty)
            {
                throw new InvalidOperationException("Use an unanchored block-style options mapping before adding provisioning configuration.");
            }

            var provisioningNamespace = managementNamespace.Replace("Azure.ResourceManager.", "Azure.Provisioning.", StringComparison.Ordinal);
            outputDirectory = outputDirectory.Replace(managementNamespace, provisioningNamespace, StringComparison.Ordinal);
            var newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var indentation = (int)options.Start.Column - 1;
            var indent = new string(' ', indentation);
            var entry = $"{indent}\"{Emitter}\":{newline}" +
                $"{indent}  namespace: \"{provisioningNamespace}\"{newline}" +
                $"{indent}  emitter-output-dir: '{outputDirectory.Replace("'", "''", StringComparison.Ordinal)}'{newline}";
            // Insert a new entry without reserializing other emitters or discarding YAML comments.
            content = content.Insert((int)options.Start.Index - indentation, entry);
            if (!IsConfigured(ReadOptions(content)))
            {
                throw new InvalidOperationException("Prepared configuration did not enable the provisioning emitter.");
            }
            await File.WriteAllTextAsync(configPath, content, ct);
            return new DefaultCommandResponse
            {
                Message = $"Added provisioning configuration to {configPath}. Commit it to an existing spec PR or open a draft spec PR, then invoke {GenerateToolName} with that PR number. Do not wait for the PR to merge.",
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prepare provisioning configuration");
            return new DefaultCommandResponse { ResponseError = $"Failed to prepare provisioning configuration: {ex.Message}" };
        }
    }

    [McpServerTool(Name = GenerateToolName), Description("Run pipeline 8473 (SDK Generation - .NET Provisioning Libraries) for a public azure-rest-api-specs TypeSpec project. Validates provisioning options at the selected ref before queueing. Pass an open spec PR number after adding configuration; merging is NOT required. Omit the PR for already configured main; do not create a no-op PR. No release plan is required or modified. For missing configuration, follow the provisioning branch of azsdk-common-generate-sdk-pipeline. Do not substitute ordinary .NET generation or generator-PR regeneration.")]
    public async Task<DefaultCommandResponse> GenerateAsync(string typespecProjectPath, string sdkReleaseType, int pullRequestNumber = 0, CancellationToken ct = default)
    {
        try
        {
            var segments = typespecProjectPath.Split('/');
            if (segments.Length < 2 || segments[0] != "specification" || segments[^1] == "tspconfig.yaml" ||
                segments.Any(s => s is "." or ".." || !Regex.IsMatch(s, @"^[A-Za-z0-9_.-]+$")))
            {
                throw new ArgumentException("Provide a repository-relative TypeSpec project directory starting with specification/, without traversal or tspconfig.yaml.");
            }
            if (sdkReleaseType is not ("beta" or "stable") || pullRequestNumber < 0)
            {
                throw new ArgumentException("Release type must be beta or stable and the spec PR number cannot be negative.");
            }

            var sourceRef = "refs/heads/main";
            string? prUrl = null;
            if (pullRequestNumber > 0)
            {
                var pr = await githubService.GetPullRequestAsync("Azure", "azure-rest-api-specs", pullRequestNumber, ct);
                if (pr == null || (!pr.Merged && pr.State == Octokit.ItemState.Closed))
                {
                    throw new InvalidOperationException("The spec PR does not exist or was closed without merging. Reopen it or choose another PR.");
                }
                if (!pr.Merged && pr.Mergeable == false)
                {
                    throw new InvalidOperationException("Resolve spec PR merge conflicts before generating from its merge ref.");
                }
                sourceRef = pr.Merged ? $"refs/heads/{pr.Base.Ref}" : $"refs/pull/{pullRequestNumber}/merge";
                prUrl = pr.HtmlUrl;
            }

            var configPath = $"{typespecProjectPath}/tspconfig.yaml";
            var config = await githubService.GetContentsSingleAsync("Azure", "azure-rest-api-specs", configPath, sourceRef, ct);
            if (config == null || string.IsNullOrWhiteSpace(config.Content))
            {
                throw new InvalidOperationException($"Could not read {configPath} at {sourceRef}. No pipeline was queued.");
            }
            if (!IsConfigured(ReadOptions(config.Content)))
            {
                throw new InvalidOperationException($"No enabled {Emitter} options at {sourceRef}. Use {PrepareToolName}, commit to a spec PR, and retry with that PR number; do not wait for merge.");
            }

            var parameters = new Dictionary<string, string>
            {
                ["ConfigType"] = "TypeSpec",
                ["ConfigPath"] = configPath,
                ["CreatePullRequest"] = "true",
                ["SdkReleaseType"] = sdkReleaseType,
            };
            ct.ThrowIfCancellationRequested();
            var run = await devopsService.RunPipelineAsync(PipelineDefinitionId, parameters, sourceRef, ct);
            return new DefaultCommandResponse
            {
                Message = "Provisioning SDK generation queued. The pipeline will create the SDK pull request; generation has not completed yet.",
                Result = new { PipelineUrl = DevOpsService.GetPipelineUrl(run.Id), SpecPullRequestUrl = prUrl, SourceRef = sourceRef },
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue provisioning SDK generation");
            return new DefaultCommandResponse { ResponseError = $"Failed to queue provisioning SDK generation: {ex.Message}" };
        }
    }

    private static YamlMappingNode ReadOptions(string content)
    {
        var yaml = new YamlStream();
        yaml.Load(new StringReader(content));
        if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root ||
            !root.Children.TryGetValue(new YamlScalarNode("options"), out var options) || options is not YamlMappingNode mapping)
        {
            throw new InvalidOperationException("tspconfig.yaml must contain one document with an options mapping.");
        }
        return mapping;
    }

    private static bool IsConfigured(YamlMappingNode options)
    {
        if (!options.Children.TryGetValue(new YamlScalarNode(Emitter), out var node))
        {
            return false;
        }
        if (node is not YamlMappingNode provisioning ||
            !Regex.IsMatch(ReadString(provisioning, "namespace"), @"^Azure\.Provisioning\.[A-Za-z_][A-Za-z0-9_.]*$") ||
            string.IsNullOrWhiteSpace(ReadString(provisioning, "emitter-output-dir")))
        {
            throw new InvalidOperationException("Existing provisioning options must include an Azure.Provisioning.* namespace and emitter-output-dir.");
        }
        return true;
    }

    private static string ReadString(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar ? scalar.Value ?? "" : "";
}
