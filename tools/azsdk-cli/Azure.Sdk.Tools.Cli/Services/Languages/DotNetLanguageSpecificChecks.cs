using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// .NET-specific implementation of language repository service.
/// Uses tools like dotnet CLI, MSBuild, NuGet, etc. for .NET development workflows.
/// </summary>
public class DotNetLanguageSpecificChecks : ILanguageSpecificChecks
{
    private readonly IProcessHelper _processHelper;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<DotNetLanguageSpecificChecks> _logger;
    private const string DotNetCommand = "dotnet";
    private const string PowerShellCommand = "pwsh";
    private const string RequiredDotNetVersion = "9.0.102";

    public DotNetLanguageSpecificChecks(
        IProcessHelper processHelper,
        IGitHelper gitHelper,
        ILogger<DotNetLanguageSpecificChecks> logger)
    {
        _processHelper = processHelper;
        _gitHelper = gitHelper;
        _logger = logger;
    }

    public async Task<CLICheckResponse> PackCodeAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var dotnetVersionValidation = await VerifyDotnetVersion();
            if (dotnetVersionValidation.ExitCode != 0)
            {
                return dotnetVersionValidation;
            }

            var serviceDirectory = GetServiceDirectoryFromPath(packagePath);
            if (serviceDirectory == null)
            {
                return new CLICheckResponse(1, "", "Failed to determine service directory from the provided package path.");
            }

            var repoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
            var serviceProj = Path.Combine(repoRoot, "eng", "service.proj");

            // Run dotnet pack with the same parameters as CI
            var args = new List<string>
            {
                "pack",
                serviceProj,
                "-warnaserror",
                "/p:ValidateRunApiCompat=true",
                "/p:SDKType=client",
                $"/p:ServiceDirectory={serviceDirectory}",
                "/p:IncludeTests=false",
                "/p:PublicSign=false",
                "/p:Configuration=Release",
                "/p:IncludePerf=false",
                "/p:IncludeStress=false",
                "/p:IncludeIntegrationTests=false"
            };

            var timeout = TimeSpan.FromMinutes(10);
            var result = await _processHelper.Run(new ProcessOptions(DotNetCommand, args.ToArray(), timeout: timeout, workingDirectory: repoRoot), cancellationToken);
            return new CLICheckResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(PackCodeAsync));
            return new CLICheckResponse(1, "", $"{nameof(PackCodeAsync)} failed with an exception: {ex.Message}");
        }
    }

    public async Task<CLICheckResponse> CheckAotCompatAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var dotnetVersionValidation = await VerifyDotnetVersion();
            if (dotnetVersionValidation.ExitCode != 0)
            {
                return dotnetVersionValidation;
            }

            var serviceDirectory = GetServiceDirectoryFromPath(packagePath);
            var packageName = GetPackageNameFromPath(packagePath);
            if (serviceDirectory == null || packageName == null)
            {
                return new CLICheckResponse(1, "", "Failed to determine service directory or package name from the provided package path.");
            }
            var repoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
            var scriptPath = Path.Combine(repoRoot, "eng", "scripts", "compatibility", "Check-AOT-Compatibility.ps1");
            var args = new[] { scriptPath, "-ServiceDirectory", serviceDirectory, "-PackageName", packageName };
            var timeout = TimeSpan.FromMinutes(6);
            var result = await _processHelper.Run(new(PowerShellCommand, args, timeout: timeout, workingDirectory: repoRoot), cancellationToken);

            return result.ExitCode switch
            {
                0 => new CLICheckResponse(result.ExitCode, result.Output),
                _ => new CLICheckResponse(result.ExitCode, result.Output, "Process failed"),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(CheckAotCompatAsync));
            return new CLICheckResponse(1, "", $"{nameof(CheckAotCompatAsync)} failed with an exception: {ex.Message}");
        }
    }

    public async Task<CLICheckResponse> RunGeneratedCodeChecksAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var dotnetVersionValidation = await VerifyDotnetVersion();
            if (dotnetVersionValidation.ExitCode != 0)
            {
                return dotnetVersionValidation;
            }
            var serviceDirectory = GetServiceDirectoryFromPath(packagePath);
            if (serviceDirectory == null)
            {
                return new CLICheckResponse(1, "", "Failed to determine service directory from the provided package path.");
            }
            var repoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
            var scriptPath = Path.Combine(repoRoot, "eng", "scripts", "CodeChecks.ps1");
            var args = new[] { scriptPath, "-ServiceDirectory", serviceDirectory, "-SpellCheckPublicApiSurface" };
            var timeout = TimeSpan.FromMinutes(6);
            var result = await _processHelper.Run(new(PowerShellCommand, args, timeout: timeout, workingDirectory: repoRoot), cancellationToken);

            return result.ExitCode switch
            {
                0 => new CLICheckResponse(result.ExitCode, result.Output),
                _ => new CLICheckResponse(result.ExitCode, result.Output, "Process failed"),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(RunGeneratedCodeChecksAsync));
            return new CLICheckResponse(1, "", $"{nameof(RunGeneratedCodeChecksAsync)} failed with an exception: {ex.Message}");
        }
    }

    private async ValueTask<CLICheckResponse> VerifyDotnetVersion()
    {
        var dotnetSDKCheck = await _processHelper.Run(new ProcessOptions(DotNetCommand, ["--list-sdks"]), CancellationToken.None);
        if (dotnetSDKCheck.ExitCode != 0)
        {
            return new CLICheckResponse(dotnetSDKCheck.ExitCode, $"dotnet --list-sdks failed with an error: {dotnetSDKCheck.Output}");
        }

        var dotnetVersions = dotnetSDKCheck.Output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        var latestVersionNumber = dotnetVersions[dotnetVersions.Length - 1].Split('[')[0].Trim();

        if (Version.TryParse(latestVersionNumber, out var installedVersion) &&
            Version.TryParse(RequiredDotNetVersion, out var minimumVersion))
        {
            if (installedVersion >= minimumVersion)
            {
                return new CLICheckResponse(0, $".NET SDK version {latestVersionNumber} meets minimum requirement of {RequiredDotNetVersion}");
            }
            else
            {
                return new CLICheckResponse(1, "", $".NET SDK version {latestVersionNumber} is below minimum requirement of {RequiredDotNetVersion}");
            }
        }
        else
        {
            return new CLICheckResponse(1, "", $"Failed to parse .NET SDK version: {latestVersionNumber}");
        }
    }

    private string? GetServiceDirectoryFromPath(string packagePath)
    {
        string? serviceDirectory = null;
        var normalizedPath = packagePath.Replace('\\', '/');
        var sdkIndex = normalizedPath.IndexOf("/sdk/", StringComparison.OrdinalIgnoreCase);

        if (sdkIndex >= 0)
        {
            var pathAfterSdk = normalizedPath.Substring(sdkIndex + 5); // Skip "/sdk/"
            var segments = pathAfterSdk.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0)
            {
                serviceDirectory = segments[0];
            }
        }
        return serviceDirectory;
    }

    private string? GetPackageNameFromPath(string packagePath)
    {
        string? packageName = null;
        var normalizedPath = packagePath.Replace('\\', '/');
        var sdkIndex = normalizedPath.IndexOf("/sdk/", StringComparison.OrdinalIgnoreCase);

        if (sdkIndex >= 0)
        {
            var pathAfterSdk = normalizedPath.Substring(sdkIndex + 5); // Skip "/sdk/"
            var segments = pathAfterSdk.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 1)
            {
                packageName = segments[1];
            }
        }
        return packageName;
    }
}
