using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Service for converting Fix objects into prompts for the AI agent by combining system instructions with specific fix details
    /// </summary>
    internal class FixPromptService
    {
        private readonly ILogger<FixPromptService> Logger;
        private readonly AppSettings AppSettings;

        public FixPromptService(ILogger<FixPromptService> logger, AppSettings appSettings)
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

            Logger.LogDebug("Creating batch prompt for {FixCount} fixes", fixes.Count);

            // Build combined fix instructions
            var combinedFixInstructions = new List<string>();
            var combinedContexts = new List<string>();

            for (int i = 0; i < fixes.Count; i++)
            {
                var fix = fixes[i];
                
                if (fix is AgentPromptFix promptFix)
                {
                    combinedFixInstructions.Add($"Fix {i + 1}: {promptFix.Prompt}");
                    
                    if (!string.IsNullOrEmpty(promptFix.Context))
                    {
                        combinedContexts.Add($"Context for Fix {i + 1}: {promptFix.Context}");
                    }
                }
                else
                {
                    string fixTypeName = fix.GetType().Name;
                    combinedFixInstructions.Add($"Fix {i + 1}: Fix Type: {fixTypeName}\nAction Required: {fix.Action}");
                    combinedContexts.Add($"Context for Fix {i + 1}: Generic fix - apply appropriate changes to resolve the compilation error.");
                }
            }

            // Combine all fix instructions
            string allFixInstructions = string.Join("\n\n", combinedFixInstructions);
            
            // Combine all contexts
            string allContexts = combinedContexts.Count > 0 
                ? string.Join("\n\n", combinedContexts)
                : "No additional context provided.";

            // Format using the template with combined instructions and contexts
            return AppSettings.AgentInstructions + string.Format(AppSettings.FixPromptTemplate, allFixInstructions, allContexts);
        }

        /// <summary>
        /// Converts a single Fix into a formatted prompt for the AI agent
        /// </summary>
        public string ConvertFixToPrompt(Fix fix)
        {
            ArgumentNullException.ThrowIfNull(fix);

            if (fix is AgentPromptFix promptFix)
            {
                return BuildAgentPromptFixMessage(promptFix);
            }
            else
            {
                return BuildGenericFixMessage(fix);
            }
        }

        /// <summary>
        /// Builds a formatted message for AgentPromptFix
        /// </summary>
        private string BuildAgentPromptFixMessage(AgentPromptFix promptFix)
        {
            string fixInstruction = promptFix.Prompt;
            string context = !string.IsNullOrEmpty(promptFix.Context) 
                ? promptFix.Context 
                : "No additional context provided.";

            return AppSettings.AgentInstructions + string.Format(AppSettings.FixPromptTemplate, fixInstruction, context);
        }

        /// <summary>
        /// Builds a formatted message for generic Fix types
        /// </summary>
        private string BuildGenericFixMessage(Fix fix)
        {
            string fixTypeName = fix switch
            {
                AgentPromptFix => nameof(AgentPromptFix),
                _ => fix.GetType().Name
            };

            string fixInstruction = $"Fix Type: {fixTypeName}\nAction Required: {fix.Action}";
            string context = "Generic fix - apply appropriate changes to resolve the compilation error.";

            return AppSettings.AgentInstructions + string.Format(AppSettings.FixPromptTemplate, fixInstruction, context);
        }
    }
}
