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

[Description("Get and update Azure DevOps package work items")]
public class PackageWorkItemTool(
    IDevOpsService devOpsService,
    ILogger<PackageWorkItemTool> logger) : MCPTool
{
    private const string GetWorkItemCommandName = "get-work-item";
    private const string UpdateWorkItemCommandName = "update-work-item";
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

    private readonly Option<string[]> fieldsOpt = new("--field")
    {
        Description = "Azure DevOps work item field patch in key=value format. Repeat to patch multiple fields.",
        Required = true,
        AllowMultipleArgumentsPerToken = false,
    };

    protected override Command GetCommand() => GetWorkItemCommand();

    public override List<Command> GetCommandInstances()
    {
        var getCommand = GetWorkItemCommand();
        var updateCommand = GetUpdateWorkItemCommand();

        getCommand.SetAction((parseResult, cancellationToken) => InstrumentedCommandHandler(getCommand, parseResult, cancellationToken));
        updateCommand.SetAction((parseResult, cancellationToken) => InstrumentedCommandHandler(updateCommand, parseResult, cancellationToken));

        return [getCommand, updateCommand];
    }

    private Command GetWorkItemCommand() =>
        new(GetWorkItemCommandName, "Get the Azure DevOps package work item")
        {
            packageNameOpt, packageVersionMajorMinorOpt, languageOpt
        };

    private Command GetUpdateWorkItemCommand() =>
        new(UpdateWorkItemCommandName, "Update the Azure DevOps package work item")
        {
            packageNameOpt, packageVersionMajorMinorOpt, languageOpt, fieldsOpt
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        try
        {
            var packageName = parseResult.GetValue(packageNameOpt);
            var packageVersionMajorMinor = parseResult.GetValue(packageVersionMajorMinorOpt);
            var language = parseResult.GetValue(languageOpt);
            return parseResult.CommandResult.Command.Name switch
            {
                GetWorkItemCommandName => await GetPackageWorkItem(packageName, packageVersionMajorMinor, language, ct),
                UpdateWorkItemCommandName => await UpdatePackageWorkItem(packageName, packageVersionMajorMinor, language, ParseFields(parseResult.GetValue(fieldsOpt)), ct),
                _ => new DefaultCommandResponse { ResponseError = $"Unsupported package work item command '{parseResult.CommandResult.Command.Name}'." },
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle package work item command '{CommandName}'.", parseResult.CommandResult.Command.Name);
            return new DefaultCommandResponse { ResponseError = $"Failed to handle package work item command: {ex.Message}" };
        }
    }

    public async Task<PackageWorkitemResponse> GetPackageWorkItem(string packageName, string packageVersionMajorMinor, string language, CancellationToken ct = default)
    {
        try
        {
            var response = new PackageWorkitemResponse
            {
                PackageName = packageName,
                Version = packageVersionMajorMinor,
            };
            response.SetLanguage(language ?? string.Empty);

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
            response.Version = packageVersionMajorMinor;

            var workItemIds = await FindPackageWorkItemIds(packageName, packageVersionMajorMinor, language, ct);
            if (workItemIds.ResponseError != null)
            {
                response.ResponseError = workItemIds.ResponseError;
                return response;
            }

            var workItems = await devOpsService.GetWorkItemsByIdsAsync([workItemIds.WorkItemId], ct: ct);
            if (workItems.Count == 0)
            {
                response.ResponseError = $"Package work item '{workItemIds.WorkItemId}' was found but could not be loaded.";
                return response;
            }

            return DevOpsService.MapPackageWorkItemToModel(workItems[0]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get package work item for package {packageName}, version {packageVersionMajorMinor}, language {language}.", packageName, packageVersionMajorMinor, language);
            var response = new PackageWorkitemResponse
            {
                PackageName = packageName,
                Version = packageVersionMajorMinor,
                ResponseError = $"Failed to get package work item: {ex.Message}"
            };
            response.SetLanguage(language ?? string.Empty);
            return response;
        }
    }

    public async Task<PackageWorkitemResponse> UpdatePackageWorkItem(string packageName, string packageVersionMajorMinor, string language, Dictionary<string, string> fields, CancellationToken ct = default)
    {
        var response = new PackageWorkitemResponse
        {
            PackageName = packageName,
            Version = packageVersionMajorMinor,
        };
        response.SetLanguage(language ?? string.Empty);

        if (fields.Count == 0)
        {
            response.ResponseError = "At least one field patch is required.";
            return response;
        }

        try
        {
            var workItemIds = await FindPackageWorkItemIds(packageName, packageVersionMajorMinor, language, ct);
            if (workItemIds.ResponseError != null)
            {
                response.ResponseError = workItemIds.ResponseError;
                return response;
            }

            var updatedWorkItem = await devOpsService.UpdateWorkItemAsync(workItemIds.WorkItemId, fields, ct);
            return DevOpsService.MapPackageWorkItemToModel(updatedWorkItem);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update package work item for package {packageName}, version {packageVersionMajorMinor}, language {language}.", packageName, packageVersionMajorMinor, language);
            response.ResponseError = $"Failed to update package work item: {ex.Message}";
            return response;
        }
    }

    private async Task<PackageWorkItemLookupResponse> FindPackageWorkItemIds(string packageName, string packageVersionMajorMinor, string language, CancellationToken ct)
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

        try
        {
            packageVersionMajorMinor = NormalizePackageVersion(packageVersionMajorMinor);
        }
        catch (ArgumentException ex)
        {
            response.ResponseError = ex.Message;
            return response;
        }

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
        return response;
    }

    private static Dictionary<string, string> ParseFields(string[]? fields)
    {
        if (fields == null || fields.Length == 0)
        {
            return [];
        }

        var parsedFields = new Dictionary<string, string>();
        foreach (var field in fields)
        {
            var separatorIndex = field.IndexOf('=');
            if (separatorIndex <= 0)
            {
                throw new ArgumentException($"Invalid field patch '{field}'. Expected key=value.");
            }

            parsedFields[field[..separatorIndex]] = field[(separatorIndex + 1)..];
        }

        return parsedFields;
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