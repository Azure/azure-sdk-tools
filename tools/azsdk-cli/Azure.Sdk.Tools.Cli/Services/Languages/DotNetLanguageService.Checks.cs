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
            if (pathParts.Length < 1 || pathParts[0] is ".." or ".")
            {
                logger.LogError("Failed to determine service directory from package path: {PackagePath}", packagePath);
                return new PackageCheckResponse(1, "", "Failed to determine service directory from the provided package path.");
            }

            var serviceDirectory = pathParts[0];
            var scriptPath = Path.Combine(repoRoot, "eng", "scripts", "CodeChecks.ps1");
            if (!File.Exists(scriptPath))
            {
                logger.LogError("Code checks script not found at: {ScriptPath}", scriptPath);
                return new PackageCheckResponse(1, "", $"Code checks script not found at: {scriptPath}")
                {
                    NextSteps =
                    [
                        "Ensure you are running this command from within a clone of the 'azure-sdk-for-net' repository.",
                        "Verify that the repository is fully restored and that 'eng/scripts/CodeChecks.ps1' exists at the repository root.",
                        "If the script is missing, restore it by running 'git restore eng/scripts/CodeChecks.ps1' or re-sync your branch to retrieve the file."
                    ]
                };
            }

            var args = new[] {"-ServiceDirectory", serviceDirectory, "-SpellCheckPublicApiSurface", "-SkipDiffValidation" };
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

                var nextSteps = result.Output
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => line.StartsWith("error : ", StringComparison.OrdinalIgnoreCase))
                    .Select(line => line["error : ".Length..].Trim())
                    .Where(msg => !string.IsNullOrWhiteSpace(msg))
                    .ToList();

                if (nextSteps.Count == 0)
                {
                    nextSteps.Add("Review the output above for specific failures in CodeChecks.ps1.");
                }
                else
                {
                    nextSteps.Insert(0, "The CodeChecks.ps1 script output contains specific instructions for each error listed below:");
                }

                return new PackageCheckResponse(result.ExitCode, result.Output, "Generated code checks failed")
                {
                    NextSteps = nextSteps
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running generated code checks at {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error running generated code checks: {ex.Message}")
            {
                NextSteps =
                [
                    $"An unexpected error occurred: {ex.Message}",
                    "Ensure the .NET SDK is installed and the package path contains a valid .NET project.",
                    "Verify that 'eng/scripts/CodeChecks.ps1' exists in the SDK repository root."
                ]
            };
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
            if (pathParts.Length < 2 || pathParts[0] is ".." or ".")
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
            var args = new[] { "-ServiceDirectory", serviceDirectory, "-PackageName", packageName };
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
                return new PackageCheckResponse(result.ExitCode, result.Output, "AOT compatibility check failed")
                {
                    NextSteps =
                    [
                        "Review the trimming and AOT warnings in the output above and carefully follow the guidance at https://github.com/Azure/azure-sdk-for-net/blob/main/doc/dev/AotCompatibility.md to address them.",
                        "If the warnings involve complex scenarios, reach out to the Azure SDK team for guidance before considering an opt-out."
                    ]
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running AOT compatibility check at {PackagePath}", packagePath);
            return new PackageCheckResponse(1, "", $"Error running AOT compatibility check: {ex.Message}")
            {
                NextSteps =
                [
                    $"An unexpected error occurred: {ex.Message}",
                    "Ensure the .NET SDK is installed and the package path contains a valid .NET project.",
                    "Verify that 'eng/scripts/compatibility/Check-AOT-Compatibility.ps1' exists in the SDK repository root."
                ]
            };
        }
    }

    private async ValueTask<PackageCheckResponse> VerifyDotnetVersion(CancellationToken ct)
    {
        var dotnetSDKCheck = await processHelper.Run(new ProcessOptions(DotNetCommand, ["--list-sdks"]), ct);
        if (dotnetSDKCheck.ExitCode != 0)
        {
            logger.LogError(".NET SDK is not installed or not available in PATH");
            return new PackageCheckResponse(dotnetSDKCheck.ExitCode, $"dotnet --list-sdks failed with an error: {dotnetSDKCheck.Output}")
            {
                NextSteps =
                [
                    "Install the .NET SDK from https://dotnet.microsoft.com/download",
                    "Ensure 'dotnet' is available in your PATH environment variable."
                ]
            };
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
                return new PackageCheckResponse(1, "", $".NET SDK version {latestVersionNumber} is below minimum requirement of {RequiredDotNetVersion}")
                {
                    NextSteps =
                    [
                        $"Update the .NET SDK to version {RequiredDotNetVersion} or later from https://dotnet.microsoft.com/download",
                        $"Current installed version: {latestVersionNumber}"
                    ]
                };
            }
        }
        else
        {
            logger.LogError("Failed to parse .NET SDK version: {VersionString}", latestVersionNumber);
            return new PackageCheckResponse(1, "", $"Failed to parse .NET SDK version: {latestVersionNumber}")
            {
                NextSteps =
                [
                    "Verify the .NET SDK installation by running 'dotnet --list-sdks' manually.",
                    $"Ensure a .NET SDK version >= {RequiredDotNetVersion} is installed."
                ]
            };
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

    public override async Task<PackageCheckResponse> CheckSpelling(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return await commonValidationHelpers.CheckSpelling(packagePath, fixCheckErrors, cancellationToken);
    }
}
