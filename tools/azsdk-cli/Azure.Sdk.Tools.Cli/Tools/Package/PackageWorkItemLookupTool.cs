// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

[Description("Find Azure DevOps package work items")]
public class PackageWorkItemLookupTool(
    IDevOpsService devOpsService,
    ILogger<PackageWorkItemLookupTool> logger) : MCPTool
{
    private const string FindWorkItemCommandName = "find-work-item";
    private static readonly Regex PackageVersionRegex = new(@"^(?<major>\d+)(?:\.(?<minor>\d+)(?:\.\d+(?:[-+][0-9A-Za-z.-]+)?)?)?$", RegexOptions.Compiled);

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

    private readonly Option<string> packageNameOpt = new("--package-name")
    {
        Description = "SDK package name.",
        Required = true,
    };

    private readonly Option<string> packageVersionMajorMinorOpt = new("--package-version", "--package-version-major-minor")
    {
        Description = "SDK package major/minor version (for example, 12.30).",
        Required = true,
    };

    private readonly Option<string> languageOpt = new("--language")
    {
        Description = "SDK language (for example, .NET, Java, JavaScript, Python, Go).",
        Required = true,
    };

    protected override Command GetCommand() =>
        new(FindWorkItemCommandName, "Find the Azure DevOps package work item ID")
        {
            packageNameOpt, packageVersionMajorMinorOpt, languageOpt
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var packageName = parseResult.GetValue(packageNameOpt);
        var packageVersionMajorMinor = parseResult.GetValue(packageVersionMajorMinorOpt);
        var language = parseResult.GetValue(languageOpt);
        return await FindPackageWorkItem(packageName, packageVersionMajorMinor, language, ct);
    }

    public async Task<PackageWorkItemLookupResponse> FindPackageWorkItem(string packageName, string packageVersionMajorMinor, string language, CancellationToken ct = default)
    {
        try
        {
            var response = new PackageWorkItemLookupResponse
            {
                PackageName = packageName,
                PackageVersionMajorMinor = packageVersionMajorMinor,
                Language = language
            };

            if (string.IsNullOrWhiteSpace(packageName))
            {
                response.ResponseError = "Package name cannot be null or empty.";
                return response;
            }

            if (string.IsNullOrWhiteSpace(packageVersionMajorMinor))
            {
                response.ResponseError = "Package version major/minor cannot be null or empty.";
                return response;
            }

            if (string.IsNullOrWhiteSpace(language))
            {
                response.ResponseError = "Language cannot be null or empty.";
                return response;
            }

            packageVersionMajorMinor = NormalizePackageVersion(packageVersionMajorMinor);
            response.PackageVersionMajorMinor = packageVersionMajorMinor;

            logger.LogDebug("Finding package work item for package {packageName}, package version major/minor {packageVersionMajorMinor}, language {language}.", packageName, packageVersionMajorMinor, language);
            var workItemIds = await devOpsService.FindPackageWorkItemIdsAsync(packageName, language, packageVersionMajorMinor, ct);

            if (workItemIds.Count == 0)
            {
                response.ResponseError = $"No package work item found for package '{packageName}', version '{packageVersionMajorMinor}', and language '{language}'.";
                return response;
            }

            if (workItemIds.Count > 1)
            {
                response.ResponseError = $"Expected one package work item for package '{packageName}', version '{packageVersionMajorMinor}', and language '{language}', but found {workItemIds.Count}: {string.Join(", ", workItemIds)}.";
                return response;
            }

            response.WorkItemId = workItemIds[0];
            response.PackageName = packageName;
            response.Language = language;
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to find package work item for package {packageName}, version {packageVersionMajorMinor}, language {language}.", packageName, packageVersionMajorMinor, language);
            return new PackageWorkItemLookupResponse
            {
                PackageName = packageName,
                PackageVersionMajorMinor = packageVersionMajorMinor,
                Language = language,
                ResponseError = $"Failed to find package work item: {ex.Message}"
            };
        }
    }

    private static string NormalizePackageVersion(string packageVersion)
    {
        var match = PackageVersionRegex.Match(packageVersion.Trim());
        if (!match.Success)
        {
            throw new ArgumentException("Package version must be a major version, major.minor version, or full SemVer version.");
        }

        var major = match.Groups["major"].Value;
        var minor = match.Groups["minor"].Success ? match.Groups["minor"].Value : "0";
        return $"{major}.{minor}";
    }
}