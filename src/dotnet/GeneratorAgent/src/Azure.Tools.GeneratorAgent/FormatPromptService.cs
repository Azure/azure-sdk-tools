using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Service for converting multiple Fix objects into batch prompts for the AI agent by combining system instructions with specific fix details
    /// </summary>
    internal class FormatPromptService
    {
        private readonly ILogger<FormatPromptService> Logger;
        private readonly AppSettings AppSettings;
        
        private static readonly ConcurrentDictionary<string, string> RuleIdCache = new();
        private static readonly Regex RuleIdRegex = new(@"(AZC\d{4}|FALLBACK)", RegexOptions.Compiled);

        public FormatPromptService(ILogger<FormatPromptService> logger, AppSettings appSettings)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(appSettings);
            Logger = logger;
            AppSettings = appSettings;
        }

        public string ConvertFixesToBatchPrompt(List<Fix> fixes)
        {
            ArgumentNullException.ThrowIfNull(fixes);

            if (fixes.Count == 0)
            {
                throw new ArgumentException("Fixes list cannot be empty", nameof(fixes));
            }

            var prompt = new StringBuilder(4096);
            var solutionLookup = new Dictionary<string, string>();
            var ruleGroups = new List<(string ruleId, List<AgentPromptFix> fixes)>();

            foreach (var fix in fixes)
            {
                if (fix is AgentPromptFix agentFix)
                {
                    var ruleId = ExtractRuleId(agentFix.Context);
                    
                    if (!solutionLookup.ContainsKey(ruleId))
                    {
                        solutionLookup[ruleId] = agentFix.Prompt;
                    }

                    var existingGroup = ruleGroups.FirstOrDefault(g => g.ruleId == ruleId);
                    if (existingGroup.fixes != null)
                    {
                        existingGroup.fixes.Add(agentFix);
                    }
                    else
                    {
                        ruleGroups.Add((ruleId, new List<AgentPromptFix> { agentFix }));
                    }
                }
            }

            prompt.AppendLine(AppSettings.AgentInstructions);
            prompt.AppendLine();
            prompt.AppendLine("=== ERRORS TO FIX ===");
            
            foreach (var (ruleId, fixList) in ruleGroups)
            {
                prompt.AppendLine(BuildErrorSummary(ruleId, fixList));
                prompt.AppendLine();
            }

            prompt.AppendLine("=== HOW TO FIX ===");
            foreach (var (ruleId, _) in ruleGroups)
            {
                prompt.AppendLine($"{ruleId}: {solutionLookup[ruleId]}");
                prompt.AppendLine();
            }

            prompt.AppendLine("=== WORKFLOW ===");
            prompt.AppendLine("1. Call list_typespec_files() to get ALL TypeSpec files with complete content for comprehensive analysis");
            prompt.AppendLine("2. Analyze available types and identifiers from all files to understand the complete context");
            prompt.AppendLine("3. Apply the solutions above to fix the specific errors listed using only existing identifiers");
            prompt.AppendLine("4. Return JSON patch with exact changes needed");
            prompt.AppendLine();
            prompt.AppendLine("=== CRITICAL RESTRICTION ===");
            prompt.AppendLine("ONLY modify 'client.tsp' file - DO NOT modify main.tsp, routes.tsp, models.tsp or any other files");
            prompt.AppendLine("The 'file' field in your JSON patch MUST be 'client.tsp' only");
            prompt.AppendLine("If errors appear to be in other files, create the fix in client.tsp using imports/references");
            prompt.AppendLine();
            prompt.AppendLine("=== RETURN JSON PATCH ===");
            prompt.AppendLine("Respond with ONLY the JSON patch - no explanations or markdown formatting.");

            return prompt.ToString();
        }

        private string ExtractRuleId(string? context)
        {
            if (string.IsNullOrEmpty(context)) return "UNKNOWN";
            
            return RuleIdCache.GetOrAdd(context, ctx =>
            {
                var match = RuleIdRegex.Match(ctx);
                return match.Success ? match.Value : "GENERIC";
            });
        }

        private string BuildErrorSummary(string ruleId, List<AgentPromptFix> fixList)
        {
            var summary = new StringBuilder();
            var count = fixList.Count;
            
            summary.AppendLine(count == 1 ? $"{ruleId}:" : $"{ruleId} ({count} instances):");
            
            for (int i = 0; i < count; i++)
            {
                var actualError = ExtractActualErrorMessage(fixList[i].Context);
                summary.AppendLine($"  {i + 1}. {actualError}");
            }
            
            return summary.ToString().TrimEnd();
        }

        private string ExtractActualErrorMessage(string? context)
        {
            if (string.IsNullOrEmpty(context)) return "Unknown error";

            var lines = context.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Look for the actual error message line
            var errorLine = lines.FirstOrDefault(l => l.StartsWith("ERROR:"));
            if (errorLine != null)
            {
                var message = errorLine.Replace("ERROR: ", "").Trim();
                return message == "{0}" ? "Specific error details will be provided" : message;
            }

            // Extract rule description as fallback
            var ruleLine = lines.FirstOrDefault(l => l.Contains(" - "));
            return ruleLine?.Split(" - ").LastOrDefault()?.Trim() ?? "Error requiring analysis";
        }
    }
}
