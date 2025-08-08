using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Base class for CLI check responses with exit code and output.
/// </summary>
public class CLICheckResponse: Response
{
    [JsonPropertyName("exit_code")]
    public int ExitCode { get; set;}
    
    [JsonPropertyName("output")]
    public string Output { get; set;}
}

/// <summary>
/// CLI check response for cookbook/documentation reference responses.
/// </summary>
public class CookbookCLICheckResponse : CLICheckResponse
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
public class SuccessCLICheckResponse : CLICheckResponse
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
public class FailureCLICheckResponse : CLICheckResponse
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