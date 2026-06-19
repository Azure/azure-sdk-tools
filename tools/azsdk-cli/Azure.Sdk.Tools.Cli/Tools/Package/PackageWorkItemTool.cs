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
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

[Description("Get and update Azure DevOps package work items")]
public class PackageWorkItemTool(
    IDevOpsService devOpsService,
    ILogger<PackageWorkItemTool> logger) : MCPMultiCommandTool
{
    private const string GetWorkItemCommandName = "get-work-item";
    private const string UpdateWorkItemCommandName = "update-work-item";
    private static readonly Regex PackageVersionRegex = new(@"^(?<major>\d+)(?:\.(?<minor>\d+)(?:\.\d+(?:[-+][0-9A-Za-z.-]+)?)?)?$", RegexOptions.Compiled);

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

    private readonly Option<string> packageNameOpt = new("--package-name")
    {
        Description = "SDK package name.",
        Required = false,
    };

    private readonly Option<string> packageVersionMajorMinorOpt = new("--package-version")
    {
        Description = "SDK package version. Accepts major (12), major.minor (12.30), or full SemVer (12.30.1[-suffix]).",
        Required = false,
    };

    private readonly Option<string> languageOpt = new("--language")
    {
        Description = "SDK language (for example, .NET, Java, JavaScript, Python, Go).",
        Required = false,
    };

    private readonly Option<int?> workItemIdOpt = new("--work-item-id")
    {
        Description = "Package work item ID. If supplied with package metadata, the resolved work item ID must match.",
        Required = false,
    };

    private readonly Option<string[]> fieldsOpt = new("--field")
    {
        Description = "Azure DevOps work item field patch in key=value format. Repeat to patch multiple fields.",
        Required = true,
        AllowMultipleArgumentsPerToken = false,
    };

    private readonly Option<string[]> multilineFieldsFormatOpt = new("--multiline-fields-format")
    {
        Description = "Azure DevOps multiline field format in key=value format. Repeat to set multiple field formats. Supported values: html, markdown.",
        Required = false,
        AllowMultipleArgumentsPerToken = false,
    };

    protected override List<Command> GetCommands() =>
    [
        GetWorkItemCommand(),
        GetUpdateWorkItemCommand(),
    ];

    private Command GetWorkItemCommand() =>
        new(GetWorkItemCommandName, "Get the Azure DevOps package work item")
        {
            packageNameOpt, packageVersionMajorMinorOpt, languageOpt, workItemIdOpt
        };

    private Command GetUpdateWorkItemCommand() =>
        new(UpdateWorkItemCommandName, "Update the Azure DevOps package work item")
        {
            packageNameOpt, packageVersionMajorMinorOpt, languageOpt, workItemIdOpt, fieldsOpt, multilineFieldsFormatOpt
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        try
        {
            var packageName = parseResult.GetValue(packageNameOpt);
            var packageVersionMajorMinor = parseResult.GetValue(packageVersionMajorMinorOpt);
            var language = parseResult.GetValue(languageOpt);
            var workItemId = parseResult.GetValue(workItemIdOpt);
            return parseResult.CommandResult.Command.Name switch
            {
                GetWorkItemCommandName => await GetPackageWorkItem(packageName, packageVersionMajorMinor, language, workItemId, ct),
                UpdateWorkItemCommandName => await UpdatePackageWorkItem(
                    packageName,
                    packageVersionMajorMinor,
                    language,
                    ParseFields(parseResult.GetValue(fieldsOpt)),
                    ParseMultilineFieldFormats(parseResult.GetValue(multilineFieldsFormatOpt)),
                    workItemId,
                    ct),
                _ => new DefaultCommandResponse { ResponseError = $"Unsupported package work item command '{parseResult.CommandResult.Command.Name}'." },
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle package work item command '{CommandName}'.", parseResult.CommandResult.Command.Name);
            return new DefaultCommandResponse { ResponseError = $"Failed to handle package work item command: {ex.Message}" };
        }
    }

    public async Task<RawPackageWorkItemResponse> GetPackageWorkItem(string? packageName, string? packageVersionMajorMinor, string? language, int? workItemId, CancellationToken ct = default)
    {
        try
        {
            var response = new RawPackageWorkItemResponse
            {
                Id = workItemId,
            };

            var workItemLookup = await ResolveWorkItemId(packageName, packageVersionMajorMinor, language, workItemId, ct);
            if (workItemLookup.ResponseError != null)
            {
                response.ResponseError = workItemLookup.ResponseError;
                return response;
            }

            response.Id = workItemLookup.WorkItemId;

            var workItems = await devOpsService.GetWorkItemsByIdsAsync([workItemLookup.WorkItemId], ct: ct);
            if (workItems.Count == 0)
            {
                response.ResponseError = $"Package work item '{workItemLookup.WorkItemId}' was found but could not be loaded.";
                return response;
            }

            return MapWorkItemToRawModel(workItems[0]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get package work item for package {packageName}, version {packageVersionMajorMinor}, language {language}.", packageName, packageVersionMajorMinor, language);
            var response = new RawPackageWorkItemResponse
            {
                Id = workItemId,
                ResponseError = $"Failed to get package work item: {ex.Message}"
            };
            return response;
        }
    }

    public async Task<RawPackageWorkItemResponse> UpdatePackageWorkItem(string packageName, string packageVersionMajorMinor, string language, Dictionary<string, string> fields, CancellationToken ct = default)
        => await UpdatePackageWorkItem(packageName, packageVersionMajorMinor, language, fields, multilineFieldFormats: [], workItemId: null, ct);

    public async Task<RawPackageWorkItemResponse> UpdatePackageWorkItem(string packageName, string packageVersionMajorMinor, string language, Dictionary<string, string> fields, Dictionary<string, string> multilineFieldFormats, CancellationToken ct = default)
        => await UpdatePackageWorkItem(packageName, packageVersionMajorMinor, language, fields, multilineFieldFormats, workItemId: null, ct);

    public async Task<RawPackageWorkItemResponse> UpdatePackageWorkItem(string? packageName, string? packageVersionMajorMinor, string? language, Dictionary<string, string> fields, int? workItemId, CancellationToken ct = default)
        => await UpdatePackageWorkItem(packageName, packageVersionMajorMinor, language, fields, multilineFieldFormats: [], workItemId, ct);

    public async Task<RawPackageWorkItemResponse> UpdatePackageWorkItem(string? packageName, string? packageVersionMajorMinor, string? language, Dictionary<string, string> fields, Dictionary<string, string> multilineFieldFormats, int? workItemId, CancellationToken ct = default)
    {
        var response = new RawPackageWorkItemResponse
        {
            Id = workItemId,
        };

        if (fields.Count == 0)
        {
            response.ResponseError = "At least one field patch is required.";
            return response;
        }

        try
        {
            var workItemLookup = await ResolveWorkItemId(packageName, packageVersionMajorMinor, language, workItemId, ct);
            if (workItemLookup.ResponseError != null)
            {
                response.ResponseError = workItemLookup.ResponseError;
                return response;
            }

            response.Id = workItemLookup.WorkItemId;

            var updatedWorkItem = await devOpsService.UpdateWorkItemAsync(workItemLookup.WorkItemId, fields, multilineFieldFormats, ct);
            return MapWorkItemToRawModel(updatedWorkItem);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update package work item for package {packageName}, version {packageVersionMajorMinor}, language {language}.", packageName, packageVersionMajorMinor, language);
            response.ResponseError = $"Failed to update package work item: {ex.Message}";
            return response;
        }
    }

    private async Task<PackageWorkItemLookupResponse> ResolveWorkItemId(string? packageName, string? packageVersionMajorMinor, string? language, int? workItemId, CancellationToken ct)
    {
        var hasAnyMetadata = HasAnyPackageMetadata(packageName, packageVersionMajorMinor, language);
        var hasAllMetadata = HasAllPackageMetadata(packageName, packageVersionMajorMinor, language);

        if (workItemId is null)
        {
            if (!hasAllMetadata)
            {
                return new PackageWorkItemLookupResponse
                {
                    PackageName = packageName ?? string.Empty,
                    PackageVersionMajorMinor = packageVersionMajorMinor ?? string.Empty,
                    Language = language ?? string.Empty,
                    ResponseError = "Provide either --work-item-id only, or all of --package-name, --package-version, and --language."
                };
            }

            return await FindPackageWorkItemIds(packageName!, packageVersionMajorMinor!, language!, ct);
        }

        if (!hasAnyMetadata)
        {
            return new PackageWorkItemLookupResponse
            {
                WorkItemId = workItemId.Value,
            };
        }

        if (!hasAllMetadata)
        {
            return new PackageWorkItemLookupResponse
            {
                WorkItemId = workItemId.Value,
                PackageName = packageName ?? string.Empty,
                PackageVersionMajorMinor = packageVersionMajorMinor ?? string.Empty,
                Language = language ?? string.Empty,
                ResponseError = "When using --work-item-id with package metadata, you must provide all of --package-name, --package-version, and --language."
            };
        }

        var lookupResponse = await FindPackageWorkItemIds(packageName!, packageVersionMajorMinor!, language!, ct);
        if (lookupResponse.ResponseError != null)
        {
            return lookupResponse;
        }

        if (lookupResponse.WorkItemId != workItemId.Value)
        {
            lookupResponse.ResponseError = $"Provided --work-item-id '{workItemId.Value}' does not match the resolved package work item ID '{lookupResponse.WorkItemId}' for package '{lookupResponse.PackageName}', version '{lookupResponse.PackageVersionMajorMinor}', and language '{lookupResponse.Language}'.";
            return lookupResponse;
        }

        return lookupResponse;
    }

    private static bool HasAnyPackageMetadata(string? packageName, string? packageVersionMajorMinor, string? language)
        => !string.IsNullOrWhiteSpace(packageName) || !string.IsNullOrWhiteSpace(packageVersionMajorMinor) || !string.IsNullOrWhiteSpace(language);

    private static bool HasAllPackageMetadata(string? packageName, string? packageVersionMajorMinor, string? language)
        => !string.IsNullOrWhiteSpace(packageName) && !string.IsNullOrWhiteSpace(packageVersionMajorMinor) && !string.IsNullOrWhiteSpace(language);

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
            response.ResponseError = "Package version cannot be null or empty.";
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

        logger.LogDebug("Finding package work item for package {packageName}, package version {packageVersionMajorMinor}, language {language}.", packageName, packageVersionMajorMinor, language);
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

            parsedFields[field[..separatorIndex]] = UnescapeFieldValue(field[(separatorIndex + 1)..]);
        }

        return parsedFields;
    }

    private static Dictionary<string, string> ParseMultilineFieldFormats(string[]? multilineFieldFormats)
    {
        var parsedFormats = ParseFields(multilineFieldFormats);
        if (parsedFormats.Count == 0)
        {
            return parsedFormats;
        }

        foreach (var key in parsedFormats.Keys.ToList())
        {
            var value = parsedFormats[key].Trim();
            parsedFormats[key] = value.ToLowerInvariant() switch
            {
                "markdown" => "markdown",
                "html" => "html",
                _ => throw new ArgumentException($"Invalid multiline field format '{value}' for field '{key}'. Supported values are: html, markdown."),
            };
        }

        return parsedFormats;
    }

    private static string UnescapeFieldValue(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains('\\'))
        {
            return value;
        }

        var result = new System.Text.StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current != '\\' || index == value.Length - 1)
            {
                result.Append(current);
                continue;
            }

            var next = value[index + 1];
            switch (next)
            {
                case 'n':
                    result.Append('\n');
                    index++;
                    break;
                case 'r':
                    result.Append('\r');
                    index++;
                    break;
                case 't':
                    result.Append('\t');
                    index++;
                    break;
                case '\\':
                    result.Append('\\');
                    index++;
                    break;
                default:
                    result.Append(current);
                    break;
            }
        }

        return result.ToString();
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

    private static RawPackageWorkItemResponse MapWorkItemToRawModel(WorkItem workItem)
    {
        return new RawPackageWorkItemResponse
        {
            Id = workItem.Id,
            Rev = workItem.Rev,
            Url = workItem.Url,
            Fields = workItem.Fields?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Relations = workItem.Relations,
        };
    }
}