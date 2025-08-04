namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Interface for operation results with exit code and output.
/// </summary>
public interface IOperationResult
{
    int ExitCode { get; }
    string Output { get; }
}

/// <summary>
/// Result for cookbook/documentation reference responses.
/// </summary>
public class CookbookResult : IOperationResult
{
    public int ExitCode { get; }
    public string Output { get; }
    public string CookbookReference { get; }

    public CookbookResult(int exitCode, string output, string cookbookReference)
    {
        ExitCode = exitCode;
        Output = output;
        CookbookReference = cookbookReference;
    }
}

/// <summary>
/// Result for successful operations.
/// </summary>
public class SuccessResult : IOperationResult
{
    public int ExitCode { get; }
    public string Output { get; }

    public SuccessResult(int exitCode, string output)
    {
        ExitCode = exitCode;
        Output = output;
    }
}

/// <summary>
/// Result for failed operations.
/// </summary>
public class FailureResult : IOperationResult
{
    public int ExitCode { get; }
    public string Output { get; }
    public string Error { get; }

    public FailureResult(int exitCode, string output, string error = "")
    {
        ExitCode = exitCode;
        Output = output;
        Error = error;
    }
}