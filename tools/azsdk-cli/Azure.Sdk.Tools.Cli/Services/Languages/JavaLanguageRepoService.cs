using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Java-specific implementation of language repository service.
/// Uses tools like mvn/gradle for build, dependency management, testing, and code formatting.
/// </summary>
public class JavaLanguageRepoService : LanguageRepoService
{
    public JavaLanguageRepoService(IProcessHelper processHelper, IGitHelper gitHelper) 
        : base(processHelper, gitHelper)
    {
    }

    public override async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct)
    {
        await Task.CompletedTask;
        
        // Try Maven first
        if (File.Exists(Path.Combine(packagePath, "pom.xml")))
        {
            var result = _processHelper.RunProcess("mvn", new[] { "dependency:analyze" }, packagePath);
            return CreateResponseFromProcessResult(result);
        }
        
        // Try Gradle
        if (File.Exists(Path.Combine(packagePath, "build.gradle")) || 
            File.Exists(Path.Combine(packagePath, "build.gradle.kts")))
        {
            var result = _processHelper.RunProcess("./gradlew", new[] { "dependencies" }, packagePath);
            return CreateResponseFromProcessResult(result);
        }
        
        return new FailureCLICheckResponse(1, "No Maven (pom.xml) or Gradle (build.gradle) build file found");
    }

    public override async Task<CLICheckResponse> RunTestsAsync(string packagePath)
    {
        await Task.CompletedTask;
        
        // Try Maven first
        if (File.Exists(Path.Combine(packagePath, "pom.xml")))
        {
            var result = _processHelper.RunProcess("mvn", new[] { "test" }, packagePath);
            return CreateResponseFromProcessResult(result);
        }
        
        // Try Gradle
        if (File.Exists(Path.Combine(packagePath, "build.gradle")) || 
            File.Exists(Path.Combine(packagePath, "build.gradle.kts")))
        {
            var result = _processHelper.RunProcess("./gradlew", new[] { "test" }, packagePath);
            return CreateResponseFromProcessResult(result);
        }
        
        return new FailureCLICheckResponse(1, "No Maven (pom.xml) or Gradle (build.gradle) build file found");
    }
}