using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// JavaScript/Node.js-specific implementation of language repository service.
/// Uses tools like npm, prettier, eslint, jest, etc. for JavaScript development workflows.
/// </summary>
public class JavaScriptLanguageRepoService : LanguageRepoService
{
    public JavaScriptLanguageRepoService(string repositoryPath) : base(repositoryPath)
    {
    }

    public override async Task<Dictionary<string, object>> AnalyzeDependenciesAsync()
    {
        try
        {
            // Run npm audit for dependency analysis
            var result = await RunCommandAsync("npm", "audit");
            
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
                "https://docs.npmjs.com/cli/v10/commands/npm-audit", 
                $"Failed to run dependency analysis. Ensure npm is installed. Error: {ex.Message}");
        }
    }

    public override async Task<Dictionary<string, object>> FormatCodeAsync()
    {
        try
        {
            // Run prettier for code formatting
            var result = await RunCommandAsync("npx", "prettier --write .");
            
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
                "https://prettier.io/docs/en/install.html", 
                $"Failed to run code formatting. Ensure prettier is installed. Error: {ex.Message}");
        }
    }

    public override async Task<Dictionary<string, object>> LintCodeAsync()
    {
        try
        {
            // Run eslint for linting
            var result = await RunCommandAsync("npx", "eslint .");
            
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
                "https://eslint.org/docs/latest/use/getting-started", 
                $"Failed to run linting. Ensure eslint is installed. Error: {ex.Message}");
        }
    }

    public override async Task<Dictionary<string, object>> RunTestsAsync()
    {
        try
        {
            // Run npm test (typically jest or another test runner)
            var result = await RunCommandAsync("npm", "test");
            
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
                "https://jestjs.io/docs/getting-started", 
                $"Failed to run tests. Ensure test runner is configured. Error: {ex.Message}");
        }
    }

    public override async Task<Dictionary<string, object>> BuildProjectAsync()
    {
        try
        {
            // Run npm run build
            var result = await RunCommandAsync("npm", "run build");
            
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
                "https://docs.npmjs.com/cli/v10/commands/npm-run-script", 
                $"Failed to build project. Ensure build script is configured in package.json. Error: {ex.Message}");
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
