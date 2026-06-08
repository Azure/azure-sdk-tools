// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

[McpServerToolType, Description("Find Azure DevOps package work items")]
public class PackageWorkItemLookupTool(
    IDevOpsService devOpsService,
    ILogger<PackageWorkItemLookupTool> logger) : MCPTool
{
    private const string FindWorkItemCommandName = "find-work-item";
    private const string FindWorkItemToolName = "azsdk_package_find_work_item";

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
        new McpCommand(FindWorkItemCommandName, "Find the Azure DevOps package work item link", FindWorkItemToolName)
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

    [McpServerTool(Name = FindWorkItemToolName), Description("Finds the singular Azure DevOps Package work item matching package name, package major/minor version, and language, and returns its browser link.")]
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

            logger.LogInformation("Finding package work item for package {packageName}, package version major/minor {packageVersionMajorMinor}, language {language}.", packageName, packageVersionMajorMinor, language);
            var workItems = await devOpsService.FindPackageWorkItemsAsync(packageName, language, packageVersionMajorMinor, ct);

            if (workItems.Count == 0)
            {
                response.ResponseError = $"No package work item found for package '{packageName}', version '{packageVersionMajorMinor}', and language '{language}'.";
                return response;
            }

            if (workItems.Count > 1)
            {
                response.ResponseError = $"Expected one package work item for package '{packageName}', version '{packageVersionMajorMinor}', and language '{language}', but found {workItems.Count}.";
                response.NextSteps = workItems.Select(workItem => GetWorkItemHtmlUrl(workItem.WorkItemId, workItem.WorkItemUrl)).ToList();
                return response;
            }

            var match = workItems[0];
            response.WorkItemId = match.WorkItemId;
            response.WorkItemUrl = GetWorkItemHtmlUrl(match.WorkItemId, match.WorkItemUrl);
            response.PackageName = match.PackageName ?? packageName;
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

    private static string GetWorkItemHtmlUrl(int workItemId, string workItemUrl)
    {
        if (workItemId > 0)
        {
            return $"{Constants.AZURE_SDK_DEVOPS_BASE_URL}/{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}/_workitems/edit/{workItemId}";
        }

        return workItemUrl.Replace("_apis/wit/workItems", "_workitems/edit");
    }
}