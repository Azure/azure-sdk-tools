namespace Azure.Tools.GeneratorAgent.Models
{
    public class AgentErrorResponse
    {
        public List<ErrorDetail> Errors { get; set; } = new();
    }

    public class ErrorDetail
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? File { get; set; }
        public int? Line { get; set; }
    }
}
