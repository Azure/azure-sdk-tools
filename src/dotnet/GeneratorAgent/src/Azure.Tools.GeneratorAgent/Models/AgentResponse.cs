namespace Azure.Tools.GeneratorAgent.Models
{
    /// <summary>
    /// Represents the parsed response from the AI agent containing updated TypeSpec content
    /// </summary>
    public class AgentResponse
    {
        /// <summary>
        /// The complete updated client.tsp content extracted from TypeSpec code blocks
        /// </summary>
        public string UpdatedFileContent { get; set; } = string.Empty;
        
        /// <summary>
        /// Indicates whether the response contained valid TypeSpec content
        /// </summary>
        public bool HasValidContent { get; set; } = false;
    }
}
