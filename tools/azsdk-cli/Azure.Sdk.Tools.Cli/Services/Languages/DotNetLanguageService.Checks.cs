using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// .NET-specific implementation of language repository service.
/// Uses tools like dotnet CLI, MSBuild, NuGet, etc. for .NET development workflows.
/// </summary>
public partial class DotnetLanguageService : LanguageService
{
    public override async Task<PackageCheckResponse> CheckGeneratedCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Starting generated code checks for .NET project at: {PackagePath}", packagePath);

            var dotnetVersionValidation = await VerifyDotnetVersion(ct);
            if (dotnetVersionValidation.ExitCode != 0)
            {
                logger.LogError("Dotnet version validation failed for generated code checks");
                return dotnetVersionValidation;
            }

            var (repoRoot, relativePath, _) = await packageInfoHelper.ParsePackagePathAsync(packagePath, ct);
            if (string.IsNullOrEmpty(relativePath))
            {
                logger.LogError("Failed to determine service directory from package path: {PackagePath}", packagePath);
                return new PackageCheckResponse(1, "", "Failed to determine service directory from the provided package path.");
            }

            var pathParts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length < 1 || pathParts[0] == "..")
            {
                logger.LogError("Failed to determine service directory from package path: {PackagePath}", packagePath);
                return new PackageCheckResponse(1, "", "Failed to determine service directory from the provided package path.");
            }

            var serviceDirectory = pathParts[0];
            var scriptPath = Path.Combine(repoRoot, "eng", "scripts", "CodeChecks.ps1");
            if (!File.Exists(scriptPath))
            {
                logger.LogError("Code checks script not found at: {ScriptPath}", scriptPath);
                return new PackageCheckResponse(1, "", $"Code checks script not found at: {scriptPath}");
            }

            var args = new[] {"-ServiceDirectory", serviceDirectory, "-SpellCheckPublicApiSurface" };
            var options = new PowershellOptions(scriptPath, args, workingDirectory: repoRoot, timeout: CodeChecksTimeout);
            var result = await powershellHelper.Run(options, ct);

            if (result.ExitCode == 0)
            {
                logger.LogInformation("Generated code checks completed successfully");
                return new PackageCheckResponse(result.ExitCode, result.Output);
            }
            else
            {
                logger.LogWarning("Generated code checks for package at {PackagePath} failed with exit code {ExitCode}", packagePath, result.ExitCode);
                return new PackageCheckResponse(result.ExitCode, result.Output, "Generated code checks failed");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running generated code checks at {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error running generated code checks: {ex.Message}");
        }
    }

    public override async Task<PackageCheckResponse> CheckAotCompat(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Starting AOT compatibility check for .NET project at: {PackagePath}", packagePath);

            var dotnetVersionValidation = await VerifyDotnetVersion(ct);
            if (dotnetVersionValidation.ExitCode != 0)
            {
                logger.LogError("Dotnet version validation failed for AOT compatibility check");
                return dotnetVersionValidation;
            }

            var (repoRoot, relativePath, _) = await packageInfoHelper.ParsePackagePathAsync(packagePath, ct);
            if (string.IsNullOrEmpty(relativePath))
            {
                logger.LogError("Failed to determine service directory or package name from package path: {PackagePath}", packagePath);
                return new PackageCheckResponse(1, "", "Failed to determine service directory or package name from the provided package path.");
            }

            var pathParts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length < 2 || pathParts[0] == "..")
            {
                logger.LogError("Failed to determine service directory or package name from package path: {PackagePath}", packagePath);
                return new PackageCheckResponse(1, "", "Failed to determine service directory or package name from the provided package path.");
            }
            var serviceDirectory = pathParts[0];

            // Get accurate package name from MSBuild project metadata rather than folder name
            var (msbuildPackageName, _, _) = await TryGetPackageInfoAsync(packagePath, ct);
            var packageName = msbuildPackageName ?? pathParts[1];

            var isAotOptedOut = await CheckAotCompatOptOut(packagePath, packageName, ct);
            if (isAotOptedOut)
            {
                logger.LogInformation("AOT compatibility check skipped - AotCompatOptOut is set to true in project file");
                return new PackageCheckResponse(0, "AOT compatibility check skipped - AotCompatOptOut is set to true in project file");
            }

            var scriptPath = Path.Combine(repoRoot, "eng", "scripts", "compatibility", "Check-AOT-Compatibility.ps1");
            if (!File.Exists(scriptPath))
            {
                logger.LogError("AOT compatibility script not found at: {ScriptPath}", scriptPath);
                return new PackageCheckResponse(1, "", $"AOT compatibility script not found at: {scriptPath}");
            }

            var workingDirectory = Path.Combine(repoRoot, "eng", "scripts", "compatibility");
            var args = new[] { scriptPath, "-ServiceDirectory", serviceDirectory, "-PackageName", packageName };
            var timeout = AotCompatTimeout;
            var options = new PowershellOptions(scriptPath, args, workingDirectory: workingDirectory, timeout: timeout);
            var result = await powershellHelper.Run(options, ct);

            if (result.ExitCode == 0)
            {
                logger.LogInformation("AOT compatibility check completed successfully");
                return new PackageCheckResponse(result.ExitCode, result.Output);
            }
            else
            {
                logger.LogWarning("AOT compatibility check failed with exit code {ExitCode}", result.ExitCode);
                return new PackageCheckResponse(result.ExitCode, result.Output, "AOT compatibility check failed");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running AOT compatibility check at {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error running AOT compatibility check: {ex.Message}");
        }
    }

    private async ValueTask<PackageCheckResponse> VerifyDotnetVersion(CancellationToken ct)
    {
        var dotnetSDKCheck = await processHelper.Run(new ProcessOptions(DotNetCommand, ["--list-sdks"]), ct);
        if (dotnetSDKCheck.ExitCode != 0)
        {
            logger.LogError(".NET SDK is not installed or not available in PATH");
            return new PackageCheckResponse(dotnetSDKCheck.ExitCode, $"dotnet --list-sdks failed with an error: {dotnetSDKCheck.Output}");
        }

        var dotnetVersions = dotnetSDKCheck.Output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        var latestVersionNumber = dotnetVersions[dotnetVersions.Length - 1].Split('[')[0].Trim();

        if (Version.TryParse(latestVersionNumber, out var installedVersion) &&
            Version.TryParse(RequiredDotNetVersion, out var minimumVersion))
        {
            if (installedVersion >= minimumVersion)
            {
                logger.LogInformation(".NET SDK version {InstalledVersion} meets minimum requirement of {RequiredVersion}", latestVersionNumber, RequiredDotNetVersion);
                return new PackageCheckResponse(0, $".NET SDK version {latestVersionNumber} meets minimum requirement of {RequiredDotNetVersion}");
            }
            else
            {
                logger.LogError(".NET SDK version {InstalledVersion} is below minimum requirement of {RequiredVersion}", latestVersionNumber, RequiredDotNetVersion);
                return new PackageCheckResponse(1, "", $".NET SDK version {latestVersionNumber} is below minimum requirement of {RequiredDotNetVersion}");
            }
        }
        else
        {
            logger.LogError("Failed to parse .NET SDK version: {VersionString}", latestVersionNumber);
            return new PackageCheckResponse(1, "", $"Failed to parse .NET SDK version: {latestVersionNumber}");
        }
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
                logger.LogDebug("No .csproj file found in package path: {PackagePath}", packagePath);
                return false;
            }

            var projectContent = await File.ReadAllTextAsync(csprojFile, ct);

            var hasAotOptOut = projectContent.Contains("<AotCompatOptOut>true</AotCompatOptOut>", StringComparison.OrdinalIgnoreCase);

            if (hasAotOptOut)
            {
                logger.LogInformation("Found AotCompatOptOut=true in project file: {CsprojFile}", csprojFile);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to check AotCompatOptOut in project file for package: {PackageName}", packageName);
            return false;
        }
    }

    public override async Task<PackageCheckResponse> ValidateReadme(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return await commonValidationHelpers.ValidateReadme(packagePath, fixCheckErrors, cancellationToken);
    }


    public override async Task<PackageCheckResponse> ValidateChangelog(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        string? packageName = null;
        try
        {
            var (_, relativePath, _) = await packageInfoHelper.ParsePackagePathAsync(packagePath, cancellationToken);

            // Prefer MSBuild package name over folder name
            var (msbuildPackageName, _, _) = await TryGetPackageInfoAsync(packagePath, cancellationToken);
            if (msbuildPackageName != null)
            {
                packageName = msbuildPackageName;
            }
            else if (!string.IsNullOrEmpty(relativePath))
            {
                var pathParts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                packageName = pathParts.Length > 1 && pathParts[0] != ".." ? pathParts[1] : null;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to parse package path for changelog validation: {PackagePath}", packagePath);
        }

        if (string.IsNullOrEmpty(packageName))
        {
            return new PackageCheckResponse(1, "",
                "Unable to determine package name for changelog validation. " +
                "Ensure the package path follows the expected sdk/<service>/<package> layout " +
                "and the .csproj file contains a valid <PackageId> element.");
        }

        return await commonValidationHelpers.ValidateChangelog(packageName, packagePath, fixCheckErrors, cancellationToken);
    }
}
