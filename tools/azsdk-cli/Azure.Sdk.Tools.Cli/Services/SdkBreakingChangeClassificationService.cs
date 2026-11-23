// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Microsoft.Extensions.AI;
using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.Cli.Services
{   
    public interface ISdkBreakingChangeClassificationService
    {
        Task<SdkBreakingChangeDetectionResult?> ClassifySdkBreakingChangesAsync(string sdkchange, string sdkBreakingPattern, string language, string? tspProjectPath, string? packageName, CancellationToken ct);
    }
    public class SdkBreakingChangeClassificationService: ISdkBreakingChangeClassificationService
    {
        private readonly ICopilotAgentRunner _agentRunner;
        private readonly ILogger<SdkBreakingChangeClassificationService> _logger;

        public SdkBreakingChangeClassificationService(ICopilotAgentRunner agentRunner, ILogger<SdkBreakingChangeClassificationService> logger)
        {
            _agentRunner = agentRunner;
            _logger = logger;
        }

        public async Task<SdkBreakingChangeDetectionResult?> ClassifySdkBreakingChangesAsync(string sdkchange, string sdkBreakingPattern, string language, string? tspProjectPath, string? packageName, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sdkBreakingPattern))
                {
                    _logger.LogWarning("SDK breaking pattern is null or empty. The agent will classify breaking changes without using breaking patterns, which may reduce classification accuracy.");
                }
                var template = new SdkBreakingChangeClassificationTemplate(sdkBreakingPattern, sdkchange, language, tspProjectPath);
                List<AIFunction>? fileTools = null;
                if (!string.IsNullOrEmpty(tspProjectPath) && Directory.Exists(tspProjectPath))
                {
                    fileTools = new List<AIFunction>
                    {
                        FileTools.CreateReadFileTool(tspProjectPath),
                        FileTools.CreateListFilesTool(tspProjectPath),
                        FileTools.CreateGrepSearchTool(tspProjectPath)
                    };
                }
                var agent = new CopilotAgent<string>
                {
                    Instructions = template.BuildPrompt(),
                    Tools = fileTools ?? [],
                };
                
                var result = await _agentRunner.RunAsync(agent, ct);
                _logger.LogDebug("Use SdkBreakingChangeClassificationTemplate version {Version}, Classification result: {Result}", template.Version, result);
                try
                {
                    var detectionResult = JsonSerializer.Deserialize<SdkBreakingChangeDetectionResult>(result);
                    if (detectionResult?.BreakingChanges != null && !string.IsNullOrWhiteSpace(packageName))
                    {
                        var suppressions = await LoadSuppressionsAsync(tspProjectPath, language, packageName, ct);
                        foreach (var breakingChange in detectionResult.BreakingChanges)
                        {
                            var origins = breakingChange.OriginBreaks;
                            var suppressedCount = origins?.Count(origin => suppressions.Contains(origin)) ?? 0;
                            breakingChange.SuppressionStatus = suppressedCount == 0
                                ? SdkBreakingChangeSuppressionStatus.None
                                : suppressedCount == origins!.Count
                                    ? SdkBreakingChangeSuppressionStatus.Suppressed
                                    : SdkBreakingChangeSuppressionStatus.PartiallySuppressed;
                        }
                    }
                    return detectionResult;
                }
                catch (Exception ex) when (ex is JsonException or ArgumentException)
                {
                    _logger.LogError(ex, "Failed to deserialize classification result: {Result}, SdkBreakingChangeClassificationTemplate version: {Version}", result, template.Version);
                    return null;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occur when run copilot agent");
                return null;
            }
        }

        private async Task<HashSet<string>> LoadSuppressionsAsync(string? tspProjectPath, string language, string packageName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(tspProjectPath))
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var path = Path.Combine(tspProjectPath, "sdk-suppressions.yaml");
            string content;
            try
            {
                content = await File.ReadAllTextAsync(path, ct);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                _logger.LogInformation("SDK suppression file '{Path}' not found. No SDK breaking changes will be suppressed.", path);
                return new HashSet<string>(StringComparer.Ordinal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read SDK suppression file '{Path}'. No SDK breaking changes will be suppressed.", path);
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var document = new DeserializerBuilder()
                .WithDuplicateKeyChecking()
                .Build()
                .Deserialize<SdkSuppressionsDocument>(content);
            if (document?.Suppressions == null)
            {
                _logger.LogInformation("SDK suppression file '{Path}' does not contain a 'suppressions' mapping.", path);
                return new HashSet<string>(StringComparer.Ordinal);
            }
            if (string.IsNullOrWhiteSpace(packageName))
            {
                _logger.LogWarning("Cannot match SDK suppressions in {Path} without an SDK package name.", path);
                return new HashSet<string>(StringComparer.Ordinal);
            }

            return new HashSet<string>(document.GetSuppressions(SdkLanguageHelpers.GetSdkLanguage(language), packageName), StringComparer.Ordinal);
        }

        private sealed class SdkSuppressionsDocument
        {
            [YamlMember(Alias = "suppressions")]
            public Dictionary<string, List<SdkPackageSuppressions>?>? Suppressions { get; set; }

            public IReadOnlyList<string> GetSuppressions(SdkLanguage language, string packageName)
            {

                var repoName = SdkLanguageHelpers.GetRepoName(language);
                if (repoName == null || Suppressions == null || !Suppressions.TryGetValue(repoName, out var packages))
                {
                    return [];
                }

                return packages?.FirstOrDefault(p => string.Equals(p.Package, packageName, StringComparison.Ordinal))?.BreakingChanges ?? [];
            }
        }

        private sealed class SdkPackageSuppressions
        {
            [YamlMember(Alias = "package")]
            public string? Package { get; set; }

            [YamlMember(Alias = "breaking-changes")]
            public List<string>? BreakingChanges { get; set; }
        }
    }
}
