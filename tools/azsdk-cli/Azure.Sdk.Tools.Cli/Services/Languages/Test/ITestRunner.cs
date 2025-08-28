using System.ComponentModel;

namespace Azure.Sdk.Tools.Cli.Services.Languages.Test;

public enum TestMode
{
    Record,
    Live,
    Playback
}

public record TestFailure([property: Description("A unique identifier for the failing test")] string TestIdentifier, [property: Description("The failure message, verbatim, including any stack trace and error message.")] string FailureDetails);

public record TestRunResult(bool IsSuccessful, List<TestFailure> Failures);

public interface ITestRunner
{
    string SupportedLanguage { get; }

    /// <summary>
    /// Runs all tests in the specified package directory.
    /// </summary>
    /// <param name="packageDirectory">The package directory</param>
    /// <param name="testMode">The test mode</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>Result of the test run including whether the run was successful and any failures</returns>
    Task<TestRunResult> RunAllTests(string packageDirectory, TestMode testMode, CancellationToken ct = default);

    /// <summary>
    /// Extract the implementation of a specific test by its identifier.
    /// </summary>
    /// <param name="packageDirectory">The package directory</param>
    /// <param name="testIdentifier">The test identifier</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>The source code representing the implementation of the test</returns>
    Task<string> GetTestImplementation(string packageDirectory, string testIdentifier, CancellationToken ct = default);
}