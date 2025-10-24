namespace Azure.Sdk.Tools.Cli.Services.Tests;

public interface ITestRunner
{
    /// <summary>
    /// Runs all tests in the specified package.
    /// </summary>
    /// <param name="packagePath">The path to the package containing the tests.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>True if all tests pass; otherwise, false.</returns>
    public Task<bool> RunAllTests(string packagePath, CancellationToken ct = default);
}