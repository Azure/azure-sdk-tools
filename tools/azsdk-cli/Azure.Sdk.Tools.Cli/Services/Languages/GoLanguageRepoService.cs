using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Go-specific implementation of language repository service.
/// Uses tools like go build, go test, go mod, gofmt, etc. for Go development workflows.
/// </summary>
public class GoLanguageRepoService : LanguageRepoService
{
    public GoLanguageRepoService(string repositoryPath) : base(repositoryPath)
    {
    }

    public override async Task<IOperationResult> AnalyzeDependenciesAsync()
    {
        try
        {
            // Run go mod tidy for dependency analysis
            var result = await RunCommandAsync("go", "mod tidy");
            
            if (result.ExitCode == 0)
            {
                return CreateSuccessResponse($"D ependency analysis completed successfully.\n{result.Output}");
            }
            else
            {
                var errorMessage = result is FailureResult failure ? failure.Error : "";
                return CreateFailureResponse($"Dependency analysis failed with exit code {result.ExitCode}.\n{errorMessage}");
            }
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://go.dev/ref/mod#go-mod-tidy", 
                $"Failed to run dependency analysis. Ensure go is installed. Error: {ex.Message}");
        }
    }

    public override async Task<IOperationResult> FormatCodeAsync()
    {
        try
        {
            // Run gofmt for code formatting
            var result = await RunCommandAsync("gofmt", "-w .");
            
            if (result.ExitCode == 0)
            {
                return CreateSuccessResponse($"Code formatting completed successfully.\n{result.Output}");
            }
            else
            {
                var errorMessage = result is FailureResult failure ? failure.Error : "";
                return CreateFailureResponse($"Code formatting failed with exit code {result.ExitCode}.\n{errorMessage}");
            }
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://pkg.go.dev/cmd/gofmt", 
                $"Failed to run code formatting. Ensure go is installed. Error: {ex.Message}");
        }
    }

    public override async Task<IOperationResult> LintCodeAsync()
    {
        try
        {
            // Run go vet for linting
            var result = await RunCommandAsync("go", "vet ./...");
            
            if (result.ExitCode == 0)
            {
                return CreateSuccessResponse($"Linting completed successfully.\n{result.Output}");
            }
            else
            {
                return CreateFailureResponse($"Linting found issues.\n{result.Output}");
            }
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://pkg.go.dev/cmd/vet", 
                $"Failed to run linting. Ensure go is installed. Error: {ex.Message}");
        }
    }

    public override async Task<IOperationResult> RunTestsAsync()
    {
        try
        {
            // Run go test
            var result = await RunCommandAsync("go", "test ./...");
            
            if (result.ExitCode == 0)
            {
                return CreateSuccessResponse($"Tests passed successfully.\n{result.Output}");
            }
            else
            {
                return CreateFailureResponse($"Tests failed with exit code {result.ExitCode}.\n{result.Output}");
            }
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://pkg.go.dev/cmd/go#hdr-Test_packages", 
                $"Failed to run tests. Ensure go is installed. Error: {ex.Message}");
        }
    }

    public override async Task<IOperationResult> BuildProjectAsync()
    {
        try
        {
            // Run go build
            var result = await RunCommandAsync("go", "build ./...");
            
            if (result.ExitCode == 0)
            {
                return CreateSuccessResponse($"Project build completed successfully.\n{result.Output}");
            }
            else
            {
                var errorMessage = result is FailureResult failure ? failure.Error : "";
                return CreateFailureResponse($"Project build failed with exit code {result.ExitCode}.\n{errorMessage}");
            }
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://pkg.go.dev/cmd/go#hdr-Compile_packages_and_dependencies", 
                $"Failed to build project. Ensure go is installed. Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper method to run command line tools asynchronously.
    /// </summary>
    private async Task<IOperationResult> RunCommandAsync(string fileName, string arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.WorkingDirectory = _repositoryPath;
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
            return new SuccessResult(process.ExitCode, output);
        }
        else
        {
            return new FailureResult(process.ExitCode, output, error);
        }
    }
}