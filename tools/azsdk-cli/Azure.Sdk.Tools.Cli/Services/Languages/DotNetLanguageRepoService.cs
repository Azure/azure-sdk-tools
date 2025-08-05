using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// .NET/C#-specific implementation of language repository service.
/// Uses tools like dotnet build, dotnet test, dotnet format, etc. for .NET development workflows.
/// </summary>
public class DotNetLanguageRepoService : LanguageRepoService
{
    public DotNetLanguageRepoService(string repositoryPath) : base(repositoryPath)
    {
    }

    public override async Task<IOperationResult> AnalyzeDependenciesAsync()
    {
        try
        {
            // Run dotnet list package for dependency analysis
            var result = await RunCommandAsync("dotnet", "list package --vulnerable --include-transitive");
            
            if (result.ExitCode == 0)
            {
                return CreateSuccessResponse($"Dependency analysis completed successfully.\n{result.Output}");
            }
            else
            {
                return CreateFailureResponse($"Dependency analysis found vulnerabilities.\n{result.Output}");
            }
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-list-package", 
                $"Failed to run dependency analysis. Ensure dotnet CLI is installed. Error: {ex.Message}");
        }
    }

    public override async Task<IOperationResult> FormatCodeAsync()
    {
        try
        {
            // Run dotnet format for code formatting
            var result = await RunCommandAsync("dotnet", "format");
            
            if (result.ExitCode == 0)
            {
                return CreateSuccessResponse($"Code formatting completed successfully.\n{result.Output}");
            }
            else
            {
                return CreateFailureResponse($"Code formatting failed with exit code {result.ExitCode}.\n{result.Error}");
            }
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-format", 
                $"Failed to run code formatting. Ensure dotnet CLI is installed. Error: {ex.Message}");
        }
    }

    public override async Task<IOperationResult> LintCodeAsync()
    {
        try
        {
            // Run dotnet build for basic static analysis
            var result = await RunCommandAsync("dotnet", "build --verbosity quiet --no-restore");
            
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
                "https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build", 
                $"Failed to run linting. Ensure dotnet CLI is installed. Error: {ex.Message}");
        }
    }

    public override async Task<IOperationResult> RunTestsAsync()
    {
        try
        {
            // Run dotnet test
            var result = await RunCommandAsync("dotnet", "test --no-build --verbosity quiet");
            
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
                "https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test", 
                $"Failed to run tests. Ensure dotnet CLI is installed. Error: {ex.Message}");
        }
    }

    public override async Task<IOperationResult> BuildProjectAsync()
    {
        try
        {
            // Run dotnet build
            var result = await RunCommandAsync("dotnet", "build --configuration Release");
            
            if (result.ExitCode == 0)
            {
                return CreateSuccessResponse($"Project build completed successfully.\n{result.Output}");
            }
            else
            {
                return CreateFailureResponse($"Project build failed with exit code {result.ExitCode}.\n{result.Error}");
            }
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build", 
                $"Failed to build project. Ensure dotnet CLI is installed. Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper method to run command line tools asynchronously.
    /// </summary>
    private async Task<(int ExitCode, string Output, string Error)> RunCommandAsync(string fileName, string arguments)
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

        return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }
}
