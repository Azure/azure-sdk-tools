namespace Azure.Tools.GeneratorAgent.Models
{
    public class AgentErrorResponse
    {
        public List<ErrorDetail> Errors { get; set; } = new();
    }
}
