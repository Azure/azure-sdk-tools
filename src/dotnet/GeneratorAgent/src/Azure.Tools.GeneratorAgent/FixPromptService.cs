using System.Text;
using Azure.Tools.ErrorAnalyzers;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Service for converting Fix objects into prompts for the AI agent
    /// </summary>
    internal class FixPromptService
    {
        private readonly ILogger<FixPromptService> Logger;

        private const string HeaderTemplate = "🔧 **APPLY FIX TO CURRENT CLIENT.TSP**\n\n";
        
        private const string RequirementsTemplate = """
            **CRITICAL Requirements:**
            1. Apply this fix to the current state of client.tsp (from our conversation history)
            2. {0}
            3. {1}
            4. Preserve ALL previous fixes that were applied in our conversation
            5. **MUST return the COMPLETE, FULL client.tsp file** - never partial content
            6. Include ALL imports, namespace, interfaces, models, and operations

            **MANDATORY Response Format:**
            1. Provide a brief explanation of the changes made
            2. **Return the COMPLETE client.tsp file** (ready to save directly)
            3. Validate that ALL previous fixes are still present before responding
            4. **Use ONLY actual TypeSpec code in the code block - no template comments**

            Expected format:
            ```typespec
            [Your complete client.tsp content here]
            ```
            """;

        public FixPromptService(ILogger<FixPromptService> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            Logger = logger;
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
            return BuildFixMessage(
                customContent: sb =>
                {
                    if (!string.IsNullOrEmpty(promptFix.Context))
                    {
                        sb.AppendLine("**Context:**");
                        sb.AppendLine(promptFix.Context);
                        sb.AppendLine();
                    }
                    
                    sb.AppendLine("**Fix Instructions:**");
                    sb.AppendLine(promptFix.Prompt);
                    sb.AppendLine();
                },
                toolInstruction: "Use the FileSearchTool to reference other TypeSpec files as needed",
                primaryGoal: "Ensure the fix resolves the AZC violation while maintaining compatibility"
            );
        }

        /// <summary>
        /// Builds a formatted message for generic Fix types
        /// </summary>
        private string BuildGenericFixMessage(Fix fix)
        {
            return BuildFixMessage(
                customContent: sb =>
                {
                    sb.AppendLine("**Fix Type:**");
                    string fixTypeName = fix switch
                    {
                        AgentPromptFix => nameof(AgentPromptFix),
                        _ => fix.GetType().Name
                    };
                    sb.AppendLine(fixTypeName);
                    sb.AppendLine();
                    
                    sb.AppendLine("**Action Required:**");
                    sb.AppendLine(fix.Action.ToString());
                    sb.AppendLine();
                },
                toolInstruction: "Use the FileSearchTool to analyze the TypeSpec files if needed",
                primaryGoal: "Address the compilation error while maintaining existing functionality"
            );
        }

        /// <summary>
        /// Builds a fix message using a template with customizable parts
        /// </summary>
        private string BuildFixMessage(Action<StringBuilder> customContent, string toolInstruction, string primaryGoal)
        {
            ArgumentNullException.ThrowIfNull(customContent);
            ArgumentNullException.ThrowIfNull(toolInstruction);
            ArgumentNullException.ThrowIfNull(primaryGoal);
            
            StringBuilder messageBuilder = new StringBuilder(2048);
            
            messageBuilder.Append(HeaderTemplate);
            
            customContent(messageBuilder);
            
            string requirements = string.Format(RequirementsTemplate, toolInstruction, primaryGoal);
            messageBuilder.Append(requirements);
            
            return messageBuilder.ToString();
        }
    }
}
