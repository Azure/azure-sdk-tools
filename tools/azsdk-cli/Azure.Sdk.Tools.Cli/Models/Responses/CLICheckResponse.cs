using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Interface for CLI check responses with exit code and output.
/// </summary>
public interface ICLICheckResponse
{
    int ExitCode { get; }
    string Output { get; }
}

/// <summary>
/// CLI check response for cookbook/documentation reference responses.
/// </summary>
public class CookbookCLICheckResponse : Response, ICLICheckResponse
{
    [JsonPropertyName("exit_code")]
    public int ExitCode { get; }
    
    [JsonPropertyName("output")]
    public string Output { get; }
    
    [JsonPropertyName("cookbook_reference")]
    public string CookbookReference { get; }

    public CookbookCLICheckResponse(int exitCode, string output, string cookbookReference)
    {
        ExitCode = exitCode;
        Output = output;
        CookbookReference = cookbookReference;
    }

    public override string ToString()
    {
        return ToString(Output);
    }
}

/// <summary>
/// CLI check response for successful operations.
/// </summary>
public class SuccessCLICheckResponse : Response, ICLICheckResponse
{
    [JsonPropertyName("exit_code")]
    public int ExitCode { get; }
    
    [JsonPropertyName("output")]
    public string Output { get; }

    public SuccessCLICheckResponse(int exitCode, string output)
    {
        ExitCode = exitCode;
        Output = output;
    }

    public override string ToString()
    {
        return ToString(Output);
    }
}

/// <summary>
/// CLI check response for failed operations.
/// </summary>
public class FailureCLICheckResponse : Response, ICLICheckResponse
{
    [JsonPropertyName("exit_code")]
    public int ExitCode { get; }
    
    [JsonPropertyName("output")]
    public string Output { get; }
    
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Error { get; }

    public FailureCLICheckResponse(int exitCode, string output, string error = "")
    {
        ExitCode = exitCode;
        Output = output;
        Error = error;
    }

    public override string ToString()
    {
        return ToString(Output);
    }
}