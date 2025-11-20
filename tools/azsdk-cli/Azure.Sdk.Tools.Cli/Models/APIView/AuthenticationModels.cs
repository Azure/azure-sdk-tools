namespace Azure.Sdk.Tools.Cli.Models.APIView;


public class AuthenticationErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Guidance { get; set; } = string.Empty;
    public string? RevisionId { get; set; }
    public string? ActiveRevisionId { get; set; }
    public string? DiffRevisionId { get; set; }
    public string? LoginUrl { get; set; }
}
