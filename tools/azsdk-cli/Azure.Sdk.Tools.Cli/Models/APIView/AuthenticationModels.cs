namespace Azure.Sdk.Tools.Cli.Models.APIView;

public class AuthenticationStatus
{
    public bool HasToken { get; set; }
    public bool IsAuthenticationWorking { get; set; }
    public string TokenSource { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string? AuthenticationError { get; set; }
    public string Guidance { get; set; } = string.Empty;
}

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

public class AuthenticationGuidance
{
    public bool IsAuthenticated { get; set; }
    public string CurrentTokenSource { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string QuickSetup { get; set; } = string.Empty;
}
