using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Java-specific implementation of language repository service.
/// Uses tools like mvn/gradle for build, dependency management, testing, and code formatting.
/// </summary>
public class JavaLanguageRepoService(IProcessHelper processHelper, INpxHelper npxHelper, IGitHelper gitHelper, ILogger<JavaLanguageRepoService> logger) : LanguageRepoService(processHelper, npxHelper, gitHelper, logger)
{
    public override async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct)
    {
        await Task.CompletedTask;

        // Try Maven first
        if (File.Exists(Path.Combine(packagePath, "pom.xml")))
        {
            var result = await _processHelper.Run(new("mvn", ["dependency:analyze"], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(result);
        }

        // Try Gradle
        if (File.Exists(Path.Combine(packagePath, "build.gradle")) ||
            File.Exists(Path.Combine(packagePath, "build.gradle.kts")))
        {
            var result = await _processHelper.Run(new("./gradlew", ["dependencies"], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(result);
        }

        return new CLICheckResponse(1, "", "No Maven (pom.xml) or Gradle (build.gradle) build file found");
    }

    public override async Task<CLICheckResponse> RunTestsAsync(string packagePath, CancellationToken ct)
    {
        await Task.CompletedTask;

        // Try Maven first
        if (File.Exists(Path.Combine(packagePath, "pom.xml")))
        {
            var result = await _processHelper.Run(new("mvn", ["test"], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(result);
        }

        // Try Gradle
        if (File.Exists(Path.Combine(packagePath, "build.gradle")) ||
            File.Exists(Path.Combine(packagePath, "build.gradle.kts")))
        {
            var result = await _processHelper.Run(new("./gradlew", ["test"], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(result);
        }

        return new CLICheckResponse(1, "", "No Maven (pom.xml) or Gradle (build.gradle) build file found");
    }
}
