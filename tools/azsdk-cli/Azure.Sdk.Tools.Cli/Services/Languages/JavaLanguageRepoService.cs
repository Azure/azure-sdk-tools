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
    public JavaLanguageRepoService(string packagePath, IProcessHelper processHelper) 
        : base(packagePath, processHelper)
    {
    }

    public override async Task<CLICheckResponse> AnalyzeDependenciesAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        
        // Try Maven first
        if (File.Exists(Path.Combine(_packagePath, "pom.xml")))
        {
            var result = _processHelper.RunProcess("mvn", new[] { "dependency:analyze" }, _packagePath);
            return CreateResponseFromProcessResult(result);
        }
        
        // Try Gradle
        if (File.Exists(Path.Combine(_packagePath, "build.gradle")) || 
            File.Exists(Path.Combine(_packagePath, "build.gradle.kts")))
        {
            var result = _processHelper.RunProcess("./gradlew", new[] { "dependencies" }, _packagePath);
            return CreateResponseFromProcessResult(result);
        }
        
        return new FailureCLICheckResponse(1, "No Maven (pom.xml) or Gradle (build.gradle) build file found");
    }

    public override async Task<CLICheckResponse> RunTestsAsync()
    {
        await Task.CompletedTask;
        
        // Try Maven first
        if (File.Exists(Path.Combine(_packagePath, "pom.xml")))
        {
            var result = _processHelper.RunProcess("mvn", new[] { "test" }, _packagePath);
            return CreateResponseFromProcessResult(result);
        }
        
        // Try Gradle
        if (File.Exists(Path.Combine(_packagePath, "build.gradle")) || 
            File.Exists(Path.Combine(_packagePath, "build.gradle.kts")))
        {
            var result = _processHelper.RunProcess("./gradlew", new[] { "test" }, _packagePath);
            return CreateResponseFromProcessResult(result);
        }
        
        return new FailureCLICheckResponse(1, "No Maven (pom.xml) or Gradle (build.gradle) build file found");
    }
}