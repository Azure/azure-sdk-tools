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
