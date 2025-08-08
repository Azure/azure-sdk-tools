using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Class for CLI check responses with exit code and output.
/// </summary>
public class ICLICheckResponse: Response
{
    [JsonPropertyName("exit_code")]
    public int ExitCode { get; set;}
    
    [JsonPropertyName("output")]
    public string Output { get; set;}
}

/// <summary>
/// CLI check response for cookbook/documentation reference responses.
/// </summary>
public class CookbookCLICheckResponse : ICLICheckResponse
{
    [JsonPropertyName("cookbook_reference")]
    public string CookbookReference { get; set;}

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
public class SuccessCLICheckResponse : ICLICheckResponse
{
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
public class FailureCLICheckResponse : ICLICheckResponse
{
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Error { get; set;}

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