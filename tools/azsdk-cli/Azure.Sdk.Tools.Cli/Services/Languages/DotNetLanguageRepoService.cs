using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// C#/.NET-specific implementation of language repository service.
/// Uses tools like dotnet CLI, NuGet, etc. for .NET development workflows.
/// </summary>
public class DotNetLanguageRepoService : LanguageRepoService
{
    public DotNetLanguageRepoService(string repositoryPath) : base(repositoryPath)
    {
    }

    public override async Task<Dictionary<string, object>> AnalyzeDependenciesAsync()
    {
        try
        {
            // Run dotnet list package --vulnerable for dependency analysis
            var result = await RunCommandAsync("dotnet", "list package --vulnerable");
            
            if (result.ExitCode == 0)
            {
                if (result.Output.Contains("no vulnerable packages"))
                {
                    return CreateSuccessResponse($"No vulnerable dependencies found.\n{result.Output}");
                }
                else
                {
                    return CreateFailureResponse($"Vulnerable dependencies found.\n{result.Output}");
                }
            }
            else
            {
                return CreateFailureResponse($"Dependency analysis failed with exit code {result.ExitCode}.\n{result.Error}");
            }
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-list-package", 
                $"Failed to run dependency analysis. Ensure .NET SDK is installed. Error: {ex.Message}");
        }
    }

    public override async Task<Dictionary<string, object>> FormatCodeAsync()
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
                $"Failed to run code formatting. Ensure .NET SDK is installed. Error: {ex.Message}");
        }
    }

    public override async Task<Dictionary<string, object>> LintCodeAsync()
    {
        try
        {
            // Run dotnet build with warnings as errors for linting
            var result = await RunCommandAsync("dotnet", "build --verbosity normal --warnaserror");
            
            if (result.ExitCode == 0)
            {
                return CreateSuccessResponse($"Linting completed successfully - no warnings found.\n{result.Output}");
            }
            else
            {
                return CreateFailureResponse($"Linting found warnings/errors.\n{result.Output}");
            }
        }
        catch (Exception ex)
        {
            return CreateCookbookResponse(
                "https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build", 
                $"Failed to run linting. Ensure .NET SDK is installed. Error: {ex.Message}");
        }
    }

    public override async Task<Dictionary<string, object>> RunTestsAsync()
    {
        try
        {
            // Run dotnet test
            var result = await RunCommandAsync("dotnet", "test --verbosity normal");
            
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
                $"Failed to run tests. Ensure .NET SDK is installed. Error: {ex.Message}");
        }
    }

    public override async Task<Dictionary<string, object>> BuildProjectAsync()
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
                $"Failed to build project. Ensure .NET SDK is installed. Error: {ex.Message}");
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
