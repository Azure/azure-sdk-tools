// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services.Tests;

public class JavaTestRunner(
    IProcessHelper _processHelper,
    ILogger<JavaTestRunner> _logger
) : ITestRunner
{
    // Test execution timeout
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(5);

    public async Task<bool> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting test execution for Java project at: {PackagePath}", packagePath);
        
        // Run Maven tests using consistent command pattern
        var pomPath = Path.Combine(packagePath, "pom.xml");
        var command = "mvn";
        var args = new[] { "test", "--no-transfer-progress", "-f", pomPath };

        var result = await _processHelper.Run(new(command, args, workingDirectory: packagePath, timeout: TestTimeout), ct);

        if (result.ExitCode == 0)
        {
            _logger.LogInformation("Test execution completed successfully");
            return true;
        }
        else
        {
            _logger.LogWarning("Test execution failed with exit code {ExitCode}", result.ExitCode);
            return false;
        }
        
    }
}
