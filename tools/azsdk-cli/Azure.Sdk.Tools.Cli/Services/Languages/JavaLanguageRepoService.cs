using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Java-specific implementation of language repository service.
/// Uses tools like mvn/gradle for build, dependency management, testing, and code formatting.
/// </summary>
public class JavaLanguageRepoService : LanguageRepoService
{
    public JavaLanguageRepoService(string packagePath) : base(packagePath)
    {
    }

    public override async Task<ICLICheckResponse> AnalyzeDependenciesAsync()
    {
        try
        {
            // Try Maven first, then Gradle if Maven fails
            var mavenResult = await TryRunMavenCommand("dependency:analyze");
            if (mavenResult.ExitCode == 0)
            {
                return CreateSuccessResponse($"Dependency analysis completed successfully using Maven.\n{mavenResult.Output}");
            }

            var gradleResult = await TryRunGradleCommand("dependencies");
            if (gradleResult.ExitCode == 0)
            {
                return CreateSuccessResponse($"Dependency analysis completed successfully using Gradle.\n{gradleResult.Output}");
            }

            return CreateFailureResponse($"Dependency analysis failed with both Maven and Gradle.\nMaven: {mavenResult.Output}\nGradle: {gradleResult.Output}");
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://maven.apache.org/plugins/maven-dependency-plugin/analyze-mojo.html", 
                $"Failed to run dependency analysis. Ensure Maven or Gradle is installed. Error: {ex.Message}");
        }
    }

    public override async Task<ICLICheckResponse> FormatCodeAsync()
    {
        try
        {
            // Try Maven formatter first, then Gradle
            var mavenResult = await TryRunMavenCommand("spotless:apply");
            if (mavenResult.ExitCode == 0)
            {
                return CreateSuccessResponse($"Code formatting completed successfully using Maven Spotless.\n{mavenResult.Output}");
            }

            var gradleResult = await TryRunGradleCommand("spotlessApply");
            if (gradleResult.ExitCode == 0)
            {
                return CreateSuccessResponse($"Code formatting completed successfully using Gradle Spotless.\n{gradleResult.Output}");
            }

            return CreateFailureResponse($"Code formatting failed with both Maven and Gradle.\nMaven: {mavenResult.Output}\nGradle: {gradleResult.Output}");
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://github.com/diffplug/spotless/tree/main/plugin-maven", 
                $"Failed to run code formatting. Ensure Maven or Gradle with Spotless plugin is configured. Error: {ex.Message}");
        }
    }

    public override async Task<ICLICheckResponse> LintCodeAsync()
    {
        try
        {
            // Try Maven checkstyle first, then Gradle
            var mavenResult = await TryRunMavenCommand("checkstyle:check");
            if (mavenResult.ExitCode == 0)
            {
                return CreateSuccessResponse($"Linting completed successfully using Maven Checkstyle.\n{mavenResult.Output}");
            }

            var gradleResult = await TryRunGradleCommand("checkstyleMain");
            if (gradleResult.ExitCode == 0)
            {
                return CreateSuccessResponse($"Linting completed successfully using Gradle Checkstyle.\n{gradleResult.Output}");
            }

            return CreateFailureResponse($"Linting found issues or failed.\nMaven: {mavenResult.Output}\nGradle: {gradleResult.Output}");
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://maven.apache.org/plugins/maven-checkstyle-plugin/", 
                $"Failed to run linting. Ensure Maven or Gradle with Checkstyle plugin is configured. Error: {ex.Message}");
        }
    }

    public override async Task<ICLICheckResponse> RunTestsAsync()
    {
        try
        {
            // Try Maven test first, then Gradle
            var mavenResult = await TryRunMavenCommand("test");
            if (mavenResult.ExitCode == 0)
            {
                return CreateSuccessResponse($"Tests passed successfully using Maven.\n{mavenResult.Output}");
            }

            var gradleResult = await TryRunGradleCommand("test");
            if (gradleResult.ExitCode == 0)
            {
                return CreateSuccessResponse($"Tests passed successfully using Gradle.\n{gradleResult.Output}");
            }

            return CreateFailureResponse($"Tests failed with both Maven and Gradle.\nMaven: {mavenResult.Output}\nGradle: {gradleResult.Output}");
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://maven.apache.org/surefire/maven-surefire-plugin/", 
                $"Failed to run tests. Ensure Maven or Gradle is installed. Error: {ex.Message}");
        }
    }

    public override async Task<ICLICheckResponse> BuildProjectAsync()
    {
        try
        {
            // Try Maven compile first, then Gradle
            var mavenResult = await TryRunMavenCommand("compile");
            if (mavenResult.ExitCode == 0)
            {
                return CreateSuccessResponse($"Project build completed successfully using Maven.\n{mavenResult.Output}");
            }

            var gradleResult = await TryRunGradleCommand("build");
            if (gradleResult.ExitCode == 0)
            {
                return CreateSuccessResponse($"Project build completed successfully using Gradle.\n{gradleResult.Output}");
            }

            return CreateFailureResponse($"Project build failed with both Maven and Gradle.\nMaven: {mavenResult.Output}\nGradle: {gradleResult.Output}");
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://maven.apache.org/guides/getting-started/maven-in-five-minutes.html", 
                $"Failed to build project. Ensure Maven or Gradle is installed. Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Try to run a Maven command and return the result.
    /// </summary>
    private async Task<ICLICheckResponse> TryRunMavenCommand(string arguments)
    {
        try
        {
            return await RunCommandAsync("mvn", arguments);
        }
        catch
        {
            return new FailureCLICheckResponse(1, "Maven command failed or mvn not found", "Maven not available");
        }
    }

    /// <summary>
    /// Try to run a Gradle command and return the result.
    /// </summary>
    private async Task<ICLICheckResponse> TryRunGradleCommand(string arguments)
    {
        try
        {
            // Try gradlew first (wrapper), then gradle
            var gradlewResult = await RunCommandAsync("./gradlew", arguments);
            if (gradlewResult.ExitCode == 0)
            {
                return gradlewResult;
            }

            return await RunCommandAsync("gradle", arguments);
        }
        catch
        {
            return new FailureCLICheckResponse(1, "Gradle command failed or gradle not found", "Gradle not available");
        }
    }

    /// <summary>
    /// Helper method to run command line tools asynchronously.
    /// </summary>
    private async Task<ICLICheckResponse> RunCommandAsync(string fileName, string arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.WorkingDirectory = _packagePath;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        if (process.ExitCode == 0)
        {
            return new SuccessCLICheckResponse(process.ExitCode, output);
        }
        else
        {
            return new FailureCLICheckResponse(process.ExitCode, output, error);
        }
    }
}