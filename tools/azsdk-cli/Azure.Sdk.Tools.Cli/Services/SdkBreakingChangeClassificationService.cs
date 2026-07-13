// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Azure.Sdk.Tools.Cli.Tools.Package;

namespace Azure.Sdk.Tools.Cli.Services
{   
    public interface ISdkBreakingChangeClassificationService
    {
        Task<List<SdkBreakingChange>> ClassifySdkBreakingChangesAsync(string sdkchange, string sdkBreakingPattern, string language, string? tspProjectPath, CancellationToken ct);
    }
    public class SdkBreakingChangeClassificationService: ISdkBreakingChangeClassificationService
    {
        private readonly ICopilotAgentRunner _agentRunner;
        private static readonly string _defaultCopilotAgentModel = "claude-opus-4.5";
        public const string SdkBreakingChangeClassifierModelVariable = "AZURE_SDK_BREAKING_CLASSIFIER_MODEL";

        private static readonly Regex ResultBlockRex = new(
                    @"^\[(?<id>[^\]]+)\]\s*^breaking:\s*(?<breaking>.+?)\s*^category:\s*(?<category>.+?)\s*^resolution:\s*(?<resolution>.*?)\s*^originBreaks:\s*(?<originBreaks>(?:-[^\n]*\n?)+)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);

        /// <summary>
        /// The model that this agent will use. Defaults to "claude-opus-4.5".
        /// </summary>
        public string CopilotAgentModel { get; set; } = Environment.GetEnvironmentVariable(SdkBreakingChangeClassifierModelVariable) ?? _defaultCopilotAgentModel;
        public SdkBreakingChangeClassificationService(ICopilotAgentRunner agentRunner)
        {
            _agentRunner = agentRunner;
        }

        public async Task<List<SdkBreakingChange>> ClassifySdkBreakingChangesAsync(string sdkchange, string sdkBreakingPattern, string language, string? tspProjectPath, CancellationToken ct)
        {
            var template = new SdkBreakingChangeClassificationTemplate(sdkBreakingPattern, sdkchange, language, tspProjectPath);
            var agent = new CopilotAgent<string>
            {
                Instructions = template.BuildPrompt(),
                Model = this.CopilotAgentModel
            };
            var result = await _agentRunner.RunAsync(agent, ct);
            return ParseClassifyResult(result);
        }

        private List<SdkBreakingChange> ParseClassifyResult(string result)
        {
            try
            {
                // Regex to capture structured breaking change blocks with multi-line originBreaks
                // The pattern matches:
                // [item-id]
                // breaking: <description>
                // category: <category>
                // resolution: <resolution> (optional)
                // originBreaks: (followed by multiple lines starting with "- ")
                //   - <original breaking #1>
                //   - <original breaking #2>
                //   - ...
                //
                // Uses lookahead (?=\[|\z) to stop at the next block or end of string
                var sdkBreakingChanges = new List<SdkBreakingChange>();
                foreach (Match match in ResultBlockRex.Matches(result))
                {
                    var id = match.Groups["id"].Value.Trim();
                    var breaking = match.Groups["breaking"].Value.Trim();
                    var category = match.Groups["category"].Value.Trim();
                    var resolution = match.Groups["resolution"].Value.Trim();
                    var originBreaksRaw = match.Groups["originBreaks"].Value.Trim();

                    // Parse originBreaks: split by newlines and extract lines starting with "-"
                    List<string> originBreaksList = new List<string>();
                    if (!string.IsNullOrEmpty(originBreaksRaw))
                    {
                        var lines = originBreaksRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            var trimmedLine = line.Trim();
                            // Remove the leading "- " or "-" from each line
                            if (trimmedLine.StartsWith("- "))
                            {
                                originBreaksList.Add(trimmedLine.Substring(2).Trim());
                            }
                            else if (trimmedLine.StartsWith("-"))
                            {
                                originBreaksList.Add(trimmedLine.Substring(1).Trim());
                            }
                            else if (!string.IsNullOrWhiteSpace(trimmedLine))
                            {
                                // If line doesn't start with "-", still include it (fallback)
                                originBreaksList.Add(trimmedLine);
                            }
                        }
                    }

                    SdkBreakingChange breakingChange = new SdkBreakingChange
                    {
                        BreakingChange = breaking,
                        Category = category,
                        Resolution = resolution,
                        OriginBreaks = originBreaksList
                    };
                    sdkBreakingChanges.Add(breakingChange);
                }
                return sdkBreakingChanges;
            }
            catch (Exception)
            {
                return new List<SdkBreakingChange>();
            }
        }
    }
}

