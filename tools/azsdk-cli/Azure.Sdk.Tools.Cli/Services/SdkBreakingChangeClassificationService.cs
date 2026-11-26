// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Microsoft.Extensions.AI;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;

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
                    if (detectionResult?.BreakingChanges != null)
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

            if (string.IsNullOrWhiteSpace(packageName))
            {
                _logger.LogWarning("Cannot match SDK suppressions without an SDK package name. No SDK breaking changes will be suppressed");
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

            SdkSuppressionsDocument? document;
            try
            {
                document = new DeserializerBuilder()
                    .WithDuplicateKeyChecking()
                    .WithEnforceRequiredMembers()
                    .WithEnforceNullability()
                    .WithNodeDeserializer(new SuppressionStringNodeDeserializer(), location => location.OnTop())
                    .WithNodeDeserializer(inner => new SuppressionCollectionNodeDeserializer(inner),
                        location => location.InsteadOf<CollectionNodeDeserializer>())
                    .Build()
                    .Deserialize<SdkSuppressionsDocument>(content);
                if (document == null)
                {
                    throw new YamlException("Expected a document containing a 'suppressions' mapping.");
                }
            }
            catch (YamlException ex)
            {
                _logger.LogError(ex, "Failed to parse SDK suppression file '{Path}'. No SDK breaking changes will be suppressed.", path);
                return new HashSet<string>(StringComparer.Ordinal);
            }
            var packages = document.Suppressions.GetPackages(SdkLanguageHelpers.GetSdkLanguage(language));
            var breakingChanges = packages.FirstOrDefault(p => string.Equals(p.Package, packageName, StringComparison.Ordinal))?.BreakingChanges ?? [];
            return new HashSet<string>(breakingChanges, StringComparer.Ordinal);
        }

        private sealed class SdkSuppressionsDocument
        {
            [YamlMember(Alias = "suppressions")]
            public required SdkSuppressionLanguages Suppressions { get; set; }
        }

        private sealed class SdkSuppressionLanguages
        {
            [YamlMember(Alias = "azure-sdk-for-go")]
            public List<SdkPackageSuppressions> Go { get; set; } = [];

            [YamlMember(Alias = "azure-sdk-for-java")]
            public List<SdkPackageSuppressions> Java { get; set; } = [];

            [YamlMember(Alias = "azure-sdk-for-js")]
            public List<SdkPackageSuppressions> JavaScript { get; set; } = [];

            [YamlMember(Alias = "azure-sdk-for-net")]
            public List<SdkPackageSuppressions> DotNet { get; set; } = [];

            [YamlMember(Alias = "azure-sdk-for-python")]
            public List<SdkPackageSuppressions> Python { get; set; } = [];

            public IReadOnlyList<SdkPackageSuppressions> GetPackages(SdkLanguage language)
            {
                return language switch
                {
                    SdkLanguage.Go => Go,
                    SdkLanguage.Java => Java,
                    SdkLanguage.JavaScript => JavaScript,
                    SdkLanguage.DotNet => DotNet,
                    SdkLanguage.Python => Python,
                    _ => []
                };
            }
        }

        private sealed class SdkPackageSuppressions
        {
            [YamlMember(Alias = "package")]
            public required string Package { get; set; }

            [YamlMember(Alias = "breaking-changes")]
            public required List<string> BreakingChanges { get; set; }
        }

        private sealed class SuppressionStringNodeDeserializer : INodeDeserializer
        {
            private readonly IDeserializer _scalarDeserializer = new DeserializerBuilder()
                .WithAttemptingUnquotedStringTypeDeserialization()
                .Build();

            public bool Deserialize(IParser parser, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer,
                out object? value, ObjectDeserializer rootDeserializer)
            {
                value = null;
                if (expectedType != typeof(string))
                {
                    return false;
                }

                value = _scalarDeserializer.Deserialize<object>(parser);
                if (value is not string)
                {
                    throw new YamlException("Expected a non-null string in SDK suppressions.");
                }
                return true;
            }
        }

        private sealed class SuppressionCollectionNodeDeserializer : INodeDeserializer
        {
            private readonly INodeDeserializer _inner;

            public SuppressionCollectionNodeDeserializer(INodeDeserializer inner)
            {
                _inner = inner;
            }

            public bool Deserialize(IParser parser, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer,
                out object? value, ObjectDeserializer rootDeserializer)
            {
                var deserialized = _inner.Deserialize(parser, expectedType, nestedObjectDeserializer, out value, rootDeserializer);
                if (deserialized && value is System.Collections.IList items && items.Cast<object?>().Any(item => item == null))
                {
                    throw new YamlException("SDK suppression arrays must not contain null items.");
                }
                return deserialized;
            }
        }
    }
}
