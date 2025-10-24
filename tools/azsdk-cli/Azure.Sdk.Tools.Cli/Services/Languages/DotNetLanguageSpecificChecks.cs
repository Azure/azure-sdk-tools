using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// .NET-specific implementation of language repository service.
/// Uses tools like dotnet CLI, MSBuild, NuGet, etc. for .NET development workflows.
/// </summary>
public class DotNetLanguageSpecificChecks : ILanguageSpecificChecks
{
    private readonly IProcessHelper _processHelper;
    private readonly IGitHelper _gitHelper;
    private readonly IPowershellHelper _powershellHelper;
    private readonly ILogger<DotNetLanguageSpecificChecks> _logger;
    private readonly ICommonValidationHelpers _commonValidationHelpers;
    private const string DotNetCommand = "dotnet";
    private const string RequiredDotNetVersion = "9.0.102"; // TODO - centralize this as part of env setup tool
    private static readonly TimeSpan CodeChecksTimeout = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan AotCompatTimeout = TimeSpan.FromMinutes(5);

    public DotNetLanguageSpecificChecks(
        IProcessHelper processHelper,
        IPowershellHelper powershellHelper,
        IGitHelper gitHelper,
        ILogger<DotNetLanguageSpecificChecks> logger,
        ICommonValidationHelpers commonValidationHelpers)
    {
        _processHelper = processHelper;
        _powershellHelper = powershellHelper;
        _gitHelper = gitHelper;
        _logger = logger;
        _commonValidationHelpers = commonValidationHelpers;
    }

    public async Task<PackageCheckResponse> CheckGeneratedCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Starting generated code checks for .NET project at: {PackagePath}", packagePath);

            var dotnetVersionValidation = await VerifyDotnetVersion();
            if (dotnetVersionValidation.ExitCode != 0)
            {
                _logger.LogError("Dotnet version validation failed for generated code checks");
                return dotnetVersionValidation;
            }

            var serviceDirectory = GetServiceDirectoryFromPath(packagePath);
            if (serviceDirectory == null)
            {
                _logger.LogError("Failed to determine service directory from package path: {PackagePath}", packagePath);
                return new PackageCheckResponse(1, "", "Failed to determine service directory from the provided package path.");
            }

            var repoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
            var scriptPath = Path.Combine(repoRoot, "eng", "scripts", "CodeChecks.ps1");
            if (!File.Exists(scriptPath))
            {
                _logger.LogError("Code checks script not found at: {ScriptPath}", scriptPath);
                return new PackageCheckResponse(1, "", $"Code checks script not found at: {scriptPath}");
            }

            var args = new[] { scriptPath, "-ServiceDirectory", serviceDirectory, "-SpellCheckPublicApiSurface" };
            var options = new PowershellOptions(scriptPath, args, workingDirectory: repoRoot, timeout: CodeChecksTimeout);
            var result = await _powershellHelper.Run(options, ct);

            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Generated code checks completed successfully");
                return new PackageCheckResponse(result.ExitCode, result.Output);
            }
            else
            {
                _logger.LogWarning("Generated code checks for package at {PackagePath} failed with exit code {ExitCode}", packagePath, result.ExitCode);
                return new PackageCheckResponse(result.ExitCode, result.Output, "Generated code checks failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running generated code checks at {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error running generated code checks: {ex.Message}");
        }
    }

    public async Task<PackageCheckResponse> CheckAotCompat(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Starting AOT compatibility check for .NET project at: {PackagePath}", packagePath);

            var dotnetVersionValidation = await VerifyDotnetVersion();
            if (dotnetVersionValidation.ExitCode != 0)
            {
                _logger.LogError("Dotnet version validation failed for AOT compatibility check");
                return dotnetVersionValidation;
            }

            var serviceDirectory = GetServiceDirectoryFromPath(packagePath);
            var packageName = GetPackageNameFromPath(packagePath);
            if (serviceDirectory == null || packageName == null)
            {
                _logger.LogError("Failed to determine service directory or package name from package path: {PackagePath}", packagePath);
                return new PackageCheckResponse(1, "", "Failed to determine service directory or package name from the provided package path.");
            }

            var isAotOptedOut = await CheckAotCompatOptOut(packagePath, packageName, ct);
            if (isAotOptedOut)
            {
                _logger.LogInformation("AOT compatibility check skipped - AotCompatOptOut is set to true in project file");
                return new PackageCheckResponse(0, "AOT compatibility check skipped - AotCompatOptOut is set to true in project file");
            }

            var repoRoot = _gitHelper.DiscoverRepoRoot(packagePath);
            var scriptPath = Path.Combine(repoRoot, "eng", "scripts", "compatibility", "Check-AOT-Compatibility.ps1");
            if (!File.Exists(scriptPath))
            {
                _logger.LogError("AOT compatibility script not found at: {ScriptPath}", scriptPath);
                return new PackageCheckResponse(1, "", $"AOT compatibility script not found at: {scriptPath}");
            }

            var workingDirectory = Path.Combine(repoRoot, "eng", "scripts", "compatibility");
            var args = new[] { scriptPath, "-ServiceDirectory", serviceDirectory, "-PackageName", packageName };
            var timeout = AotCompatTimeout;
            var options = new PowershellOptions(scriptPath, args, workingDirectory: workingDirectory, timeout: timeout);
            var result = await _powershellHelper.Run(options, ct);

            if (result.ExitCode == 0)
            {
                _logger.LogInformation("AOT compatibility check completed successfully");
                return new PackageCheckResponse(result.ExitCode, result.Output);
            }
            else
            {
                _logger.LogWarning("AOT compatibility check failed with exit code {ExitCode}", result.ExitCode);
                return new PackageCheckResponse(result.ExitCode, result.Output, "AOT compatibility check failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running AOT compatibility check at {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error running AOT compatibility check: {ex.Message}");
        }
    }

    private async ValueTask<PackageCheckResponse> VerifyDotnetVersion()
    {
        var dotnetSDKCheck = await _processHelper.Run(new ProcessOptions(DotNetCommand, ["--list-sdks"]), CancellationToken.None);
        if (dotnetSDKCheck.ExitCode != 0)
        {
            _logger.LogError(".NET SDK is not installed or not available in PATH");
            return new PackageCheckResponse(dotnetSDKCheck.ExitCode, $"dotnet --list-sdks failed with an error: {dotnetSDKCheck.Output}");
        }

        var dotnetVersions = dotnetSDKCheck.Output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        var latestVersionNumber = dotnetVersions[dotnetVersions.Length - 1].Split('[')[0].Trim();

        if (Version.TryParse(latestVersionNumber, out var installedVersion) &&
            Version.TryParse(RequiredDotNetVersion, out var minimumVersion))
        {
            if (installedVersion >= minimumVersion)
            {
                _logger.LogInformation(".NET SDK version {InstalledVersion} meets minimum requirement of {RequiredVersion}", latestVersionNumber, RequiredDotNetVersion);
                return new PackageCheckResponse(0, $".NET SDK version {latestVersionNumber} meets minimum requirement of {RequiredDotNetVersion}");
            }
            else
            {
                _logger.LogError(".NET SDK version {InstalledVersion} is below minimum requirement of {RequiredVersion}", latestVersionNumber, RequiredDotNetVersion);
                return new PackageCheckResponse(1, "", $".NET SDK version {latestVersionNumber} is below minimum requirement of {RequiredDotNetVersion}");
            }
        }
        else
        {
            _logger.LogError("Failed to parse .NET SDK version: {VersionString}", latestVersionNumber);
            return new PackageCheckResponse(1, "", $"Failed to parse .NET SDK version: {latestVersionNumber}");
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
                _logger.LogDebug("Extracted service directory: {ServiceDirectory}", serviceDirectory);
            }
            else
            {
                _logger.LogDebug("No segments found after /sdk/ in path");
            }
        }
        else
        {
            _logger.LogDebug("Path does not contain /sdk/ segment");
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

    private async ValueTask<bool> CheckAotCompatOptOut(string packagePath, string packageName, CancellationToken ct)
    {
        try
        {
            var csprojFiles = Directory.GetFiles(packagePath, "*.csproj", SearchOption.AllDirectories);
            var mainCsprojFile = csprojFiles.FirstOrDefault(f => 
                Path.GetFileNameWithoutExtension(f).Equals(packageName, StringComparison.OrdinalIgnoreCase));
            var csprojFile = mainCsprojFile ?? csprojFiles.FirstOrDefault();
            
            if (csprojFile == null)
            {
                _logger.LogDebug("No .csproj file found in package path: {PackagePath}", packagePath);
                return false;
            }

            var projectContent = await File.ReadAllTextAsync(csprojFile, ct);
            
            var hasAotOptOut = projectContent.Contains("<AotCompatOptOut>true</AotCompatOptOut>", StringComparison.OrdinalIgnoreCase);
            
            if (hasAotOptOut)
            {
                _logger.LogInformation("Found AotCompatOptOut=true in project file: {CsprojFile}", csprojFile);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check AotCompatOptOut in project file for package: {PackageName}", packageName);
            return false;
        }
    }

    public async Task<CLICheckResponse> ValidateReadme(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return await _commonValidationHelpers.ValidateReadmeCommon(packagePath, fixCheckErrors, cancellationToken);
    }


    public async Task<CLICheckResponse> ValidateChangelog(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        var packageName = GetPackageNameFromPath(packagePath);
        return await _commonValidationHelpers.ValidateChangelogCommon(packageName, packagePath, fixCheckErrors, cancellationToken);
    }
}
