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

    public CLICheckResponse() { }

    public CLICheckResponse(int exitCode, string output, string error = null)
    {
        ExitCode = exitCode;
        Output = output;
        if (!string.IsNullOrEmpty(error))
        {
            ResponseError = error;
        }
    }

    public override string ToString()
    {
        return ToString(Output);
    }
}

/// <summary>
/// CLI check response for cookbook/documentation reference responses.
/// </summary>
public class CookbookCLICheckResponse : CLICheckResponse
{
    [JsonPropertyName("cookbook_reference")]
    public string CookbookReference { get; set;}

    public CookbookCLICheckResponse(int exitCode, string output, string cookbookReference) : base(exitCode, output)
    {
        CookbookReference = cookbookReference;
    }

    public override string ToString()
    {
        return ToString(Output);
    }
}

